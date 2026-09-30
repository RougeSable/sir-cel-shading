namespace SirCelShading
{
    // The variant of the final colors pass (Postprocess/Tonemapping/Main.hlsl of
    // the game), written by the plugin to the player's folder then compiled by
    // the game itself (MyShaderCompiler.Compile, then MyComputeShaders.Create),
    // with its own headers.
    //
    // Why here, in the final colors: it is the last pass that sees the whole
    // scene, before the selection highlight, the billboards and FXAA. The
    // interface (HUD, menus, texts) is drawn much later, by RenderMainSprites:
    // it stays sharp.
    public static class ShaderSource
    {
        public const string FileName = "CelShading.hlsl";

        // Slot of the scene depth. No file of jeu-installe/Content/Shaders
        // declares t31; the game's pass only uses t0 to t3.
        public const int DepthSlot = 31;

        // Slot of the scene albedo (MyGBuffer.Main.GBuffer0), read by the
        // effect. No file of Content/Shaders declares t27.
        public const int AlbedoSlot = 27;

        // Game headers the variant depends on, relative to Content/Shaders.
        // Included between angle brackets: the game's compiler then looks for
        // them in its own shader folder, wherever this file was written.
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
@"// Sir Cel Shading: Animated film cel shading. Variant of
// Postprocess/Tonemapping/Main.hlsl, written by the plugin every time the game
// starts and compiled by the game itself. Do not edit by hand.
//
// The game's body is kept as is (grain, exposure, bloom, filmic curve, color
// filters); the effect comes next, right before the sRGB conversion.

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

// Distance, in pixels, between the depth reads of the outline measure.
#define CEL_WIDTH 1

// Width of the outline ramp, as a multiple of the threshold.
#define CEL_OUTLINE_SPAN 2

// Inverse of the distance: on a flat surface, it varies linearly on screen.
// Its second derivative is zero on a plane, and only lights up at silhouettes
// and edges. The sky (clear depth) is zero.
float CelInverseDistance(int2 texel, int2 last)
{
    float hw = CelDepth[uint2(clamp(texel, int2(0, 0), last))];
    if (!IsDepthForeground(hw))
        return 0;
    return 1.0f / max(compute_depth(hw), 1e-3f);
}

// Presence of an outline, between 0 and 1. Everything is measured as a ratio
// of distances, never in meters: the same edge is drawn the same at one meter
// or at ten kilometers.
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

    // The nearest of the neighborhood is the reference: the ratio stays
    // bounded on both sides of a silhouette, sky included.
    float nearest = max(max(max(c, l), max(rt, u)), max(max(d, ul), max(max(dr, ur), dl)));
    if (nearest <= 0)
        return 0;

    // Second derivative in the four directions; along the diagonals the step
    // is longer by a factor of the square root of two.
    float curvature = max(abs(l + rt - 2 * c), abs(u + d - 2 * c));
    curvature = max(curvature, 0.70710678f * max(abs(ul + dr - 2 * c), abs(ur + dl - 2 * c)));

    // Divided by the angle covered by a pixel, the measure becomes the change
    // of slope between the two faces of an edge: independent of the distance,
    // of the screen resolution and of the field of view.
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

// Saturation in sRGB.
float3 CelVivid(float3 color, float amount)
{
    float3 srgb = rgb_to_srgb(saturate(color));
    float gray = dot(srgb, CelLuma);
    return srgb_to_rgb(saturate(lerp(gray.xxx, srgb, amount)));
}

// Light received by a surface: its final color divided by the color of the
// object itself (albedo, from the GBuffer). 1: lit like its own color;
// 0: full shadow. Negative when it cannot be measured (an almost black
// object, or no albedo bound).
float CelLighting(float3 color, float3 albedo)
{
    float a = dot(albedo, CelLuma);
    if (a < 0.02f)
        return -1;
    return dot(color, CelLuma) / a;
}

// Gives the surface the light 'target' instead of 'lighting', keeping its
// hue. Where the pixel is almost black its hue means nothing: the color of
// the object takes over.
float3 CelRelight(float3 color, float3 albedo, float lighting, float target)
{
    float3 relit = color * min(target / max(lighting, 1e-4f), 8.0f);
    return lerp(albedo * target, relit, saturate(lighting * 4));
}

// Softness of the step between two light tones, as a fraction of a tone:
// wide, so that shadows blend like painted ones.
#define CEL_BAND_SOFTNESS 0.2f
// Light of the darkest tone: shadows are colored, never black.
#define CEL_SHADOW_FLOOR 0.45f
// Saturation added by the effect.
#define CEL_ANIMATED_SATURATION 1.1f
// Width of the rim light, in pixels.
#define CEL_RIM_WIDTH 3
// Haze, in octaves of distance beyond the near plane (a ratio of distances):
// it starts around 50 m and is complete around 25 km with the game's usual
// near plane. Never complete: a planet stays visible.
#define CEL_HAZE_START 10.0f
#define CEL_HAZE_END 19.0f
#define CEL_HAZE_MAX 0.85f

// Two or three soft tones of light, from the shadow floor to full light.
float CelShadeBands(float lighting)
{
    const float steps = CEL_SHADE_TONES - 1;
    float x = saturate(lighting) * steps;
    float b = min(floor(x), steps - 1);
    float f = x - b;
    float q = b + smoothstep(0.5f - CEL_BAND_SOFTNESS, 0.5f + CEL_BAND_SOFTNESS, f);
    return lerp(CEL_SHADOW_FLOOR, 1.0f, q / steps);
}

// Outlines take a darker, deeper shade of the object's own color.
float3 CelInk(float3 color)
{
    return color * color * 0.4f;
}

float CelDistanceOrSky(int2 p, int2 last)
{
    float hw = CelDepth[uint2(clamp(p, int2(0, 0), last))];
    return IsDepthForeground(hw) ? compute_depth(hw) : 1e30f;
}

// How much this pixel lies on the near side of a silhouette: something at
// least a third farther, or the sky, within a few pixels. A ratio of
// distances, never meters.
float CelSilhouette(int2 p, int2 last, float dist)
{
    const int w = CEL_RIM_WIDTH;
    float farthest = max(
        max(CelDistanceOrSky(p + int2(-w, 0), last), CelDistanceOrSky(p + int2(w, 0), last)),
        max(CelDistanceOrSky(p + int2(0, -w), last), CelDistanceOrSky(p + int2(0, w), last)));
    return saturate((farthest / dist - 1.3f) * 3);
}

// A thin edge of sunlight on silhouettes seen against the sun.
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

// The game's grading of a scene color, without grain or bloom: the sky
// samples below are HDR, the image they blend into is not.
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

// Each thread of a group reads one point of a fixed grid over the whole
// screen. Every group reads the same points: the sky color changes smoothly
// from pixel to pixel, for one read per pixel.
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

// The sky color around this pixel: the nearest samples weigh most, those
// above the pixel more than those below. Alpha is zero when no sky is seen.
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

// Aerial perspective: the farther, the more a thing fades into the sky.
float CelHazeAmount(float dist)
{
    float nearPlane = max(compute_depth(1.0f), 1e-6f);
    float octaves = log2(max(dist / nearPlane, 1.0f));
    return CEL_HAZE * CEL_HAZE_MAX * smoothstep(CEL_HAZE_START, CEL_HAZE_END, octaves);
}

[numthreads(NUMTHREADS_X, NUMTHREADS_Y, 1)]
void __compute_shader(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    // The game's, unchanged (Postprocess/Tonemapping/Main.hlsl).
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
    // Every thread of the group, before any branch: it waits for the others.
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
            // Under a black sky (space) there is no air to fade into.
            haze *= sky.a * saturate(dot(sky.rgb, CelLuma) * 4);
            color = lerp(color, sky.rgb, haze);
        }
    }

    // The game's, unchanged.
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
