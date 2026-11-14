namespace SirCelShading
{
    // La variante de la passe des couleurs finales (Postprocess/Tonemapping/Main.hlsl
    // du jeu), écrite par le greffon dans le dossier du joueur puis compilée par
    // le jeu lui-même (MyShaderCompiler.Compile, puis MyComputeShaders.Create),
    // avec ses propres en-têtes.
    //
    // Pourquoi ici, dans les couleurs finales : c'est la dernière passe qui voit
    // toute la scène, avant la surbrillance de sélection, les billboards et le
    // FXAA. L'interface (HUD, menus, textes) est dessinée bien plus tard, par
    // RenderMainSprites : elle reste nette.
    //
    // Le shader est écrit en ASCII pur (les commentaires HLSL sont en français
    // sans accents) : le compilateur du jeu lit le fichier tel quel.
    public static class ShaderSource
    {
        public const string FileName = "CelShading.hlsl";

        // Emplacement de la profondeur de la scène. Aucun fichier de
        // jeu-installe/Content/Shaders ne déclare t31 ; la passe du jeu n'utilise
        // que t0 à t3.
        public const int DepthSlot = 31;

        // Emplacement de l'albédo de la scène (MyGBuffer.Main.GBuffer0), lu par
        // l'effet. Aucun fichier de Content/Shaders ne déclare t27.
        public const int AlbedoSlot = 27;

        // Les en-têtes du jeu dont dépend la variante, relatifs à Content/Shaders.
        // Inclus entre chevrons : le compilateur du jeu les cherche alors dans son
        // propre dossier de shaders, où que ce fichier ait été écrit.
        public static readonly string[] GameHeaders =
        {
            "Postprocess/Tonemapping/Filters.hlsli",
            "Postprocess/Tonemapping/Defines.hlsli",
            "Random.hlsli",
        };

        public static string Text
        {
            get
            {
                return Header
                    + "#include <" + GameHeaders[0] + ">\n"
                    + "#include <" + GameHeaders[1] + ">\n"
                    + "#include <" + GameHeaders[2] + ">\n"
                    + "\n"
                    + "Texture2D<float> CelDepth : register(t" + DepthSlot + ");\n"
                    + "Texture2D<float4> CelAlbedo : register(t" + AlbedoSlot + ");\n"
                    + Body;
            }
        }

        private const string Header =
@"// Sir Cel Shading : cel shading facon film d'animation. Variante de
// Postprocess/Tonemapping/Main.hlsl, ecrite par le greffon a chaque demarrage
// du jeu et compilee par le jeu lui-meme. Ne pas modifier a la main.
//
// Le corps du jeu est garde tel quel (grain, exposition, halo, courbe
// filmique, filtres de couleur) ; l'effet vient ensuite, juste avant la
// conversion sRGB.

";

        private const string Body =
@"
#ifndef CEL_STRENGTH
#define CEL_STRENGTH 0.7f
#endif
#ifndef CEL_THRESHOLD
#define CEL_THRESHOLD 0.75f
#endif
#ifndef CEL_SHADE_TONES
#define CEL_SHADE_TONES 3
#endif
#ifndef CEL_RIM
#define CEL_RIM 0.2f
#endif
#ifndef CEL_HAZE
#define CEL_HAZE 0.6f
#endif

// Distance, en pixels, entre les lectures de profondeur de la mesure des contours.
#define CEL_WIDTH 1

// Largeur de la rampe des contours, en multiple du seuil.
#define CEL_OUTLINE_SPAN 2

// Inverse de la distance : sur une surface plane, il varie lineairement a
// l'ecran. Sa derivee seconde est nulle sur un plan, et ne s'allume qu'aux
// silhouettes et aux aretes. Le ciel (profondeur vide) vaut zero.
float CelInverseDistance(int2 texel, int2 last)
{
    float hw = CelDepth[uint2(clamp(texel, int2(0, 0), last))];
    if (!IsDepthForeground(hw))
        return 0;
    return 1.0f / max(compute_depth(hw), 1e-3f);
}

// Presence d'un contour, entre 0 et 1. Tout est mesure en rapport de
// distances, jamais en metres : une meme arete est dessinee pareil a un metre
// ou a dix kilometres.
float CelOutline(uint2 texel)
{
    uint width, height;
    CelDepth.GetDimensions(width, height);
    int2 last = min(int2(width, height), int2(frame_.Screen.resolution)) - 1;
    int2 p = int2(texel);
    const int r = CEL_WIDTH;

    float c  = CelInverseDistance(p, last);
    float l  = CelInverseDistance(p + int2(-r,  0), last);
    float rt = CelInverseDistance(p + int2( r,  0), last);
    float u  = CelInverseDistance(p + int2( 0, -r), last);
    float d  = CelInverseDistance(p + int2( 0,  r), last);
    float ul = CelInverseDistance(p + int2(-r, -r), last);
    float dr = CelInverseDistance(p + int2( r,  r), last);
    float ur = CelInverseDistance(p + int2( r, -r), last);
    float dl = CelInverseDistance(p + int2(-r,  r), last);

    // Le plus proche du voisinage sert de reference : le rapport reste borne
    // de part et d'autre d'une silhouette, ciel compris.
    float nearest = max(max(max(c, l), max(rt, u)), max(max(d, ul), max(max(dr, ur), dl)));
    if (nearest <= 0)
        return 0;

    // Derivee seconde dans les quatre directions ; sur les diagonales le pas
    // est plus long d'un facteur racine de deux.
    float curvature = max(abs(l + rt - 2 * c), abs(u + d - 2 * c));
    curvature = max(curvature, 0.70710678f * max(abs(ul + dr - 2 * c), abs(ur + dl - 2 * c)));

    // Divisee par l'angle couvert par un pixel, la mesure devient le changement
    // de pente entre les deux faces d'une arete : independant de la distance,
    // de la resolution de l'ecran et du champ de vision.
    float pixelAngle = 2.0f / (abs(frame_.Environment.projection_matrix._22) * frame_.Screen.resolution.y);
    float measure = curvature / (nearest * r * pixelAngle);
    return smoothstep(CEL_THRESHOLD, CEL_OUTLINE_SPAN * CEL_THRESHOLD, measure);
}

static const float3 CelLuma = float3(0.2126f, 0.7152f, 0.0722f);

int2 CelLastTexel()
{
    uint width, height;
    CelDepth.GetDimensions(width, height);
    return min(int2(width, height), int2(frame_.Screen.resolution)) - 1;
}

// Saturation en sRGB.
float3 CelVivid(float3 color, float amount)
{
    float3 srgb = rgb_to_srgb(saturate(color));
    float gray = dot(srgb, CelLuma);
    return srgb_to_rgb(saturate(lerp(gray.xxx, srgb, amount)));
}

// Lumiere recue par une surface : sa couleur finale divisee par la couleur
// de l'objet lui-meme (albedo, issu du GBuffer). 1 : eclairee comme sa propre
// couleur ; 0 : pleine ombre. Negative quand elle ne peut pas etre mesuree
// (objet presque noir, ou aucun albedo lie).
float CelLighting(float3 color, float3 albedo)
{
    float a = dot(albedo, CelLuma);
    if (a < 0.02f)
        return -1;
    return dot(color, CelLuma) / a;
}

// Donne a la surface la lumiere 'target' au lieu de 'lighting', en gardant sa
// teinte. Quand le pixel est presque noir sa teinte ne veut rien dire : la
// couleur de l'objet prend le relais.
float3 CelRelight(float3 color, float3 albedo, float lighting, float target)
{
    float3 relit = color * min(target / max(lighting, 1e-4f), 8.0f);
    return lerp(albedo * target, relit, saturate(lighting * 4));
}

// Douceur du passage entre deux tons de lumiere, en fraction de ton :
// large, pour que les ombres se fondent comme des ombres peintes.
#define CEL_BAND_SOFTNESS 0.2f
// Lumiere du ton le plus sombre : les ombres sont colorees, jamais noires.
#define CEL_SHADOW_FLOOR 0.45f
// Saturation ajoutee par l'effet.
#define CEL_ANIMATED_SATURATION 1.1f
// Largeur de la lumiere de contour, en pixels.
#define CEL_RIM_WIDTH 3
// Brume, en octaves de distance au-dela du plan proche (un rapport de
// distances) : elle commence vers 50 m et est complete vers 25 km avec le plan
// proche habituel du jeu. Jamais complete : une planete reste visible.
#define CEL_HAZE_START 10.0f
#define CEL_HAZE_END 19.0f
#define CEL_HAZE_MAX 0.85f

// Deux ou trois tons de lumiere doux, du plancher d'ombre a la pleine lumiere.
float CelShadeBands(float lighting)
{
    const float steps = CEL_SHADE_TONES - 1;
    float x = saturate(lighting) * steps;
    float b = min(floor(x), steps - 1);
    float f = x - b;
    float q = b + smoothstep(0.5f - CEL_BAND_SOFTNESS, 0.5f + CEL_BAND_SOFTNESS, f);
    return lerp(CEL_SHADOW_FLOOR, 1.0f, q / steps);
}

// Les contours prennent une teinte plus sombre et plus profonde de la couleur
// propre de l'objet.
float3 CelInk(float3 color)
{
    return color * color * 0.4f;
}

float CelDistanceOrSky(int2 p, int2 last)
{
    float hw = CelDepth[uint2(clamp(p, int2(0, 0), last))];
    return IsDepthForeground(hw) ? compute_depth(hw) : 1e30f;
}

// A quel point ce pixel est du cote proche d'une silhouette : quelque chose
// au moins un tiers plus loin, ou le ciel, a quelques pixels. Un rapport de
// distances, jamais des metres.
float CelSilhouette(int2 p, int2 last, float dist)
{
    const int w = CEL_RIM_WIDTH;
    float farthest = max(
        max(CelDistanceOrSky(p + int2(-w, 0), last), CelDistanceOrSky(p + int2(w, 0), last)),
        max(CelDistanceOrSky(p + int2(0, -w), last), CelDistanceOrSky(p + int2(0, w), last)));
    return saturate((farthest / dist - 1.3f) * 3);
}

// Un fin lisere de lumiere solaire sur les silhouettes vues a contre-jour.
float3 CelRimLight(float3 color, int2 p, int2 last, float dist, float2 uv)
{
    float3 ray = normalize(view_to_world(compute_screen_ray(uv)));
    float3 toSun = -frame_.Light.directionalLightVec;
    float backlight = saturate(dot(ray, toSun) / max(length(toSun), 1e-4f));
    backlight *= backlight;
    if (backlight <= 0)
        return color;

    float3 sun = max(frame_.Light.directionalLightColor, 0);
    float3 tint = sun / max(max(sun.r, max(sun.g, sun.b)), 1e-3f);
    float rim = CEL_RIM * backlight * CelSilhouette(p, last, dist);
    return saturate(color + tint * rim * 0.8f);
}

// L'etalonnage du jeu pour une couleur de scene, sans grain ni halo : les
// echantillons de ciel ci-dessous sont en HDR, l'image dans laquelle ils se
// fondent ne l'est pas.
float3 CelGrade(float3 hdr)
{
    float3 color = hdr;
#ifndef DISABLE_TONEMAPPING
    color = ToneMapFilmic_Hable(ExposedColor(color, 0), frame_.Post.WhitePoint);
#endif
#ifndef DISABLE_COLOR_FILTERS
    color = ApplyBasicFilters(color);
    color = VibranceFilter(color);
    color = SepiaFilter(color);
#endif
    return saturate(color);
}

// Chaque fil d'un groupe lit un point d'une grille fixe sur tout l'ecran.
// Tous les groupes lisent les memes points : la couleur du ciel change
// doucement d'un pixel a l'autre, pour une lecture par pixel.
groupshared float4 CelSkySamples[NUMTHREADS_X * NUMTHREADS_Y];

void CelGatherSky(uint2 groupThread, int2 last)
{
    float2 cell = (float2(groupThread) + 0.5f) / float2(NUMTHREADS_X, NUMTHREADS_Y);
    int2 p = min(int2(cell * frame_.Screen.resolution), last);
    float4 sky = 0;
    if (!IsDepthForeground(CelDepth[uint2(p)]))
        sky = float4(Source[uint2(p)].xyz, 1);
    CelSkySamples[groupThread.y * NUMTHREADS_X + groupThread.x] = sky;
    GroupMemoryBarrierWithGroupSync();
}

// La couleur du ciel autour de ce pixel : les echantillons les plus proches
// pesent le plus, ceux au-dessus du pixel plus que ceux en dessous. L'alpha
// vaut zero quand aucun ciel n'est vu.
float4 CelSkyColor(float2 uv)
{
    float3 sum = 0;
    float weight = 0;
    [loop]
    for (uint i = 0; i < NUMTHREADS_X * NUMTHREADS_Y; i++)
    {
        float4 sky = CelSkySamples[i];
        float2 cell = (float2(i % NUMTHREADS_X, i / NUMTHREADS_X) + 0.5f) / float2(NUMTHREADS_X, NUMTHREADS_Y);
        float2 d = cell - uv;
        float w = sky.a * (cell.y <= uv.y ? 1.0f : 0.35f) / (0.02f + dot(d, d));
        sum += sky.rgb * w;
        weight += w;
    }
    if (weight <= 0)
        return 0;
    return float4(CelGrade(sum / weight), 1);
}

// Perspective aerienne : plus une chose est loin, plus elle se fond dans le ciel.
float CelHazeAmount(float dist)
{
    float nearPlane = max(compute_depth(1.0f), 1e-6f);
    float octaves = log2(max(dist / nearPlane, 1.0f));
    return CEL_HAZE * CEL_HAZE_MAX * smoothstep(CEL_HAZE_START, CEL_HAZE_END, octaves);
}

[numthreads(NUMTHREADS_X, NUMTHREADS_Y, 1)]
void __compute_shader(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    // Celui du jeu, inchange (Postprocess/Tonemapping/Main.hlsl).
    uint2 texel = dispatchThreadID.xy;
    float2 uv = (texel + 0.5f) / frame_.Screen.resolution;

    float3 sourceSample = Source[texel].xyz;

    if (frame_.Post.GrainStrength > 0)
    {
        RandomGenerator random;
        float grainRounding = 1;
        if (frame_.Post.GrainSize > 0)
        {
            int gs = frame_.Post.GrainSize * 2 + 1;
            float2 grainDist = (float2)(texel % gs) - frame_.Post.GrainSize;
            grainRounding = 1 - dot(grainDist, grainDist) / (frame_.Post.GrainSize * frame_.Post.GrainSize * 2.0f);
            random.SetSeed(((texel.x + gs) / gs)*((texel.y + gs) / gs)*int(frame_.frameTime*1000));
        }
        else random.SetSeed(texel.x * texel.y * int(frame_.frameTime*1000));
        sourceSample -= saturate(frame_.Post.GrainAmount - random.GetFloat()) * grainRounding * frame_.Post.GrainStrength;
    }

    float3 color = sourceSample;

#ifndef DISABLE_TONEMAPPING
    float3 exposed_color = ExposedColor(sourceSample, 0);
    float dirt = Dirt.SampleLevel(BilinearSampler, uv, 0) * frame_.Post.BloomDirtRatio + (1 - frame_.Post.BloomDirtRatio);
    color = exposed_color + Bloom.SampleLevel(BilinearSampler, uv, 0).xyz * frame_.Post.BloomMult * dirt;
    color = ToneMapFilmic_Hable(color, frame_.Post.WhitePoint);
#endif

#ifndef DISABLE_COLOR_FILTERS
    color = ApplyBasicFilters(color);
    color = VibranceFilter(color);
    color = SepiaFilter(color);
#endif

    color = saturate(color);

    // Sir Cel Shading.
    int2 last = CelLastTexel();
    int2 p = min(int2(texel), last);
    // Chaque fil du groupe, avant tout branchement : il attend les autres.
    CelGatherSky(texel % uint2(NUMTHREADS_X, NUMTHREADS_Y), last);

    float hw = CelDepth[uint2(p)];
    bool foreground = IsDepthForeground(hw);
    if (foreground)
    {
        float3 albedo = CelAlbedo[uint2(p)].rgb;
        float lighting = CelLighting(color, albedo);
        if (lighting >= 0 && lighting < 1)
            color = CelRelight(color, albedo, lighting, CelShadeBands(lighting));
    }
    color = CelVivid(color, CEL_ANIMATED_SATURATION);
    color = lerp(color, CelInk(color), CelOutline(texel) * CEL_STRENGTH);
    if (foreground)
    {
        float dist = compute_depth(hw);
        color = CelRimLight(color, p, last, dist, uv);
        float haze = CelHazeAmount(dist);
        if (haze > 0.004f)
        {
            float4 sky = CelSkyColor(uv);
            // Sous un ciel noir (l'espace) il n'y a pas d'air ou se fondre.
            haze *= sky.a * saturate(dot(sky.rgb, CelLuma) * 4);
            color = lerp(color, sky.rgb, haze);
        }
    }

    // Celui du jeu, inchange.
    color = rgb_to_srgb(color);
#ifdef FILL_ALPHA_LUMINANCE
    float alpha = GetRelativeLuminance(color);
    Destination[texel] = float4(color, alpha);
#else
    Destination[texel] = float4(color, 1);
#endif
}
";
    }
}
