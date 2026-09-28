namespace SirCelShading
{
    // La variante du passage des couleurs finales (Postprocess/Tonemapping/Main.hlsl
    // du jeu), écrite par le greffon dans le dossier du joueur puis compilée par
    // le jeu lui-même (MyShaderCompiler.Compile, puis MyComputeShaders.Create),
    // avec ses propres en-têtes.
    //
    // Pourquoi ici, dans les couleurs finales : c'est le dernier passage qui
    // voit la scène entière, avant le halo de sélection, les billboards et le
    // FXAA. L'interface (HUD, menus, textes) est dessinée bien après, par
    // RenderMainSprites : elle reste nette.
    public static class SourceDuShader
    {
        public const string NomDuFichier = "CelShading.hlsl";

        // Emplacement de la profondeur de la scène. Aucun fichier de
        // jeu-installe/Content/Shaders ne déclare t31 ; le passage du jeu
        // n'utilise que t0 à t3.
        public const int EmplacementProfondeur = 31;

        // En-têtes du jeu dont dépend la variante, relatifs à Content/Shaders.
        // Inclus entre chevrons : le compilateur du jeu les cherche alors dans
        // son propre dossier de shaders, où qu'ait été écrit ce fichier.
        public static readonly string[] EnTetesDuJeu =
        {
            "Postprocess/Tonemapping/Filters.hlsli",
            "Postprocess/Tonemapping/Defines.hlsli",
            "Random.hlsli",
        };

        public static string Texte
        {
            get
            {
                return Entete
                    + "#include <" + EnTetesDuJeu[0] + ">\n"
                    + "#include <" + EnTetesDuJeu[1] + ">\n"
                    + "#include <" + EnTetesDuJeu[2] + ">\n"
                    + "\n"
                    + "Texture2D<float> CelProfondeur : register(t" + EmplacementProfondeur + ");\n"
                    + Corps;
            }
        }

        private const string Entete =
@"// Sir Cel Shading : rendu bande dessinée (contours noirs, couleurs en aplats).
// Variante de Postprocess/Tonemapping/Main.hlsl, écrite par le greffon à chaque
// lancement du jeu et compilée par le jeu lui-même. Ne pas modifier à la main.
//
// Le corps du jeu est repris tel quel (grain, exposition, halo, courbe
// filmique, filtres de couleur) ; les aplats et les contours viennent ensuite,
// juste avant la conversion en sRGB.

";

        private const string Corps =
@"
#ifndef CEL_TEINTES
#define CEL_TEINTES 4
#endif
#ifndef CEL_EPAISSEUR
#define CEL_EPAISSEUR 1
#endif
#ifndef CEL_FORCE
#define CEL_FORCE 1.0f
#endif
#ifndef CEL_SEUIL
#define CEL_SEUIL 0.75f
#endif
#ifndef CEL_SATURATION
#define CEL_SATURATION 1.15f
#endif

// Largeur de la transition entre deux teintes, en fraction de palier : assez
// pour qu'une texture bruitée ne scintille pas, assez peu pour garder l'aplat.
#define CEL_DOUCEUR 0.06f

// Inverse de la distance : sur une surface plane, elle varie linéairement à
// l'écran. Sa dérivée seconde est nulle sur un plan, et ne s'allume qu'aux
// silhouettes et aux arêtes. Le ciel (profondeur de dégagement) vaut zéro.
float CelInverseDistance(int2 texel, int2 dernier)
{
    float hw = CelProfondeur[uint2(clamp(texel, int2(0, 0), dernier))];
    if (!IsDepthForeground(hw))
        return 0;
    return 1.0f / max(compute_depth(hw), 1e-3f);
}

// Présence d'un contour, entre 0 et 1. Tout se mesure en rapport de
// distances, jamais en mètres : la même arête se dessine pareil à un mètre ou
// à dix kilomètres.
float CelContour(uint2 texel)
{
    uint largeur, hauteur;
    CelProfondeur.GetDimensions(largeur, hauteur);
    int2 dernier = min(int2(largeur, hauteur), int2(frame_.Screen.resolution)) - 1;
    int2 p = int2(texel);
    const int r = CEL_EPAISSEUR;

    float c  = CelInverseDistance(p, dernier);
    float g  = CelInverseDistance(p + int2(-r,  0), dernier);
    float d  = CelInverseDistance(p + int2( r,  0), dernier);
    float h  = CelInverseDistance(p + int2( 0, -r), dernier);
    float b  = CelInverseDistance(p + int2( 0,  r), dernier);
    float hg = CelInverseDistance(p + int2(-r, -r), dernier);
    float bd = CelInverseDistance(p + int2( r,  r), dernier);
    float hd = CelInverseDistance(p + int2( r, -r), dernier);
    float bg = CelInverseDistance(p + int2(-r,  r), dernier);

    // Le plus proche du voisinage sert de référence : le rapport reste borné
    // de part et d'autre d'une silhouette, ciel compris.
    float proche = max(max(max(c, g), max(d, h)), max(max(b, hg), max(max(bd, hd), bg)));
    if (proche <= 0)
        return 0;

    // Dérivée seconde dans les quatre directions ; en diagonale, le pas est
    // plus long d'un facteur racine de deux.
    float courbure = max(abs(g + d - 2 * c), abs(h + b - 2 * c));
    courbure = max(courbure, 0.70710678f * max(abs(hg + bd - 2 * c), abs(hd + bg - 2 * c)));

    // Rapportée à l'angle couvert par un pixel, la mesure devient l'écart de
    // pente entre les deux faces d'une arête : indépendante de la distance,
    // de la définition de l'écran et du champ de vision.
    float anglePixel = 2.0f / (abs(frame_.Environment.projection_matrix._22) * frame_.Screen.resolution.y);
    float mesure = courbure / (proche * r * anglePixel);
    return smoothstep(CEL_SEUIL, 2 * CEL_SEUIL, mesure);
}

// Aplats : la valeur (le canal le plus fort, en sRGB) tombe sur des paliers,
// la teinte et la saturation sont gardées. Sous la moitié du premier palier,
// l'image reste celle du jeu : le noir de l'espace reste noir. La transition
// est continue à cet endroit (0,5 palier donne 0,5 palier).
float3 CelAplats(float3 couleur)
{
    float3 srgb = rgb_to_srgb(saturate(couleur));
    float valeur = max(srgb.r, max(srgb.g, srgb.b));
    float echelle = valeur * CEL_TEINTES;
    if (echelle < 0.5f)
        return couleur;

    float base = floor(echelle);
    float transition = smoothstep(0.5f - CEL_DOUCEUR, 0.5f + CEL_DOUCEUR, echelle - base);
    float palier = min((base + transition) / CEL_TEINTES, 1.0f);
    float3 aplat = srgb * (palier / valeur);

    float gris = dot(aplat, float3(0.2126f, 0.7152f, 0.0722f));
    aplat = saturate(lerp(gris.xxx, aplat, CEL_SATURATION));
    return srgb_to_rgb(aplat);
}

[numthreads(NUMTHREADS_X, NUMTHREADS_Y, 1)]
void __compute_shader(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    // Du jeu, à l'identique (Postprocess/Tonemapping/Main.hlsl).
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
    color = CelAplats(color);
    color *= 1 - CelContour(texel) * CEL_FORCE;

    // Du jeu, à l'identique.
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
