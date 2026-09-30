using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SirCelShading.Tests
{
    public class ShaderTests
    {
        [Fact]
        public void DepthIsBoundInT31AndAlbedoInT27()
        {
            Assert.Equal(31, ShaderSource.DepthSlot);
            Assert.Equal(27, ShaderSource.AlbedoSlot);
            Assert.Contains("Texture2D<float> CelDepth : register(t31)", ShaderSource.Text);
            Assert.Contains("Texture2D<float4> CelAlbedo : register(t27)", ShaderSource.Text);
        }

        [Fact]
        public void NoSlotOfTheGamePassIsDeclaredAgain()
        {
            // The game's pass binds t0 to t3, u0 and s0: our variant declares
            // no other slot than t31 and t27.
            var slots = Regex.Matches(ShaderSource.Text, @"register\s*\(\s*(\w+)\s*\)")
                .Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(s => s).ToArray();
            Assert.Equal(new[] { "t27", "t31" }, slots);
        }

        [Fact]
        public void GameHeadersAreIncludedBetweenAngleBrackets()
        {
            // Between quotes, the game's compiler would look for them next to
            // our file, in the player's folder, and fail.
            foreach (var header in ShaderSource.GameHeaders)
                Assert.Contains("#include <" + header + ">", ShaderSource.Text);
            Assert.DoesNotContain("#include \"", ShaderSource.Text);
        }

        [Fact]
        public void TheEntryPointIsTheOneTheGameExpects()
        {
            Assert.Contains("void __compute_shader(uint3 dispatchThreadID : SV_DispatchThreadID)", ShaderSource.Text);
            Assert.Contains("[numthreads(NUMTHREADS_X, NUMTHREADS_Y, 1)]", ShaderSource.Text);
        }

        [Fact]
        public void TheThreeGameFlagsAreHonored()
        {
            Assert.Contains("#ifndef DISABLE_TONEMAPPING", ShaderSource.Text);
            Assert.Contains("#ifdef FILL_ALPHA_LUMINANCE", ShaderSource.Text);
            Assert.Contains("#ifndef DISABLE_COLOR_FILTERS", ShaderSource.Text);
        }

        [Fact]
        public void OnlyTheAnimatedFilmRenderingIsInTheShader()
        {
            Assert.DoesNotContain("CEL_STYLE", ShaderSource.Text);
            Assert.DoesNotContain("CelFlatColors", ShaderSource.Text);
            Assert.DoesNotContain("CEL_SHADOWS", ShaderSource.Text);
            Assert.DoesNotContain("CEL_SATURATION", ShaderSource.Text);
            Assert.DoesNotContain("Comic", ShaderSource.Text);
            Assert.DoesNotContain("Clear line", ShaderSource.Text);
        }

        [Fact]
        public void TheAnimatedFilmRenderingKeepsItsParts()
        {
            // Soft shadow tones, darker outlines, rim light, distance haze.
            Assert.Contains("CelRelight(color, albedo, lighting, CelShadeBands(lighting))", ShaderSource.Text);
            Assert.Contains("color = lerp(color, CelInk(color), CelOutline(texel) * CEL_STRENGTH);", ShaderSource.Text);
            Assert.Contains("color = CelRimLight(color, p, last, dist, uv);", ShaderSource.Text);
            Assert.Contains("color = lerp(color, sky.rgb, haze);", ShaderSource.Text);
            Assert.Contains("#define CEL_OUTLINE_SPAN 2\n", ShaderSource.Text);
            Assert.Contains("return smoothstep(CEL_THRESHOLD, CEL_OUTLINE_SPAN * CEL_THRESHOLD, measure);", ShaderSource.Text);
            Assert.Contains("#define CEL_BAND_SOFTNESS 0.2f", ShaderSource.Text);
            Assert.Contains("#define CEL_SHADOW_FLOOR 0.45f", ShaderSource.Text);
            Assert.Contains("#define CEL_ANIMATED_SATURATION 1.1f", ShaderSource.Text);
            Assert.Contains("return color * color * 0.4f;", ShaderSource.Text);
        }

        [Fact]
        public void EveryPreprocessorBlockIsClosed()
        {
            var opened = Regex.Matches(ShaderSource.Text, @"^\s*#\s*if(n?def)?\b", RegexOptions.Multiline).Count;
            var closed = Regex.Matches(ShaderSource.Text, @"^\s*#\s*endif\b", RegexOptions.Multiline).Count;
            Assert.Equal(opened, closed);
        }

        [Fact]
        public void TheFileIsWrittenWithUnixLineEndingsInPlainAscii()
        {
            Assert.DoesNotContain("\r", ShaderSource.Text);
            Assert.True(ShaderSource.Text.All(c => c < 128));
        }
    }

    public class ShaderVariantsTests
    {
        [Theory]
        [InlineData(true, false, ShaderVariant.Normal)]
        [InlineData(true, true, ShaderVariant.AlphaLuminance)]
        [InlineData(false, false, ShaderVariant.NoTonemapping)]
        [InlineData(false, true, ShaderVariant.NoTonemapping)]
        public void ChoosesLikeTheGame(bool tonemapping, bool alphaLuminance, ShaderVariant expected)
        {
            Assert.Equal(expected, ShaderVariants.Choose(tonemapping, alphaLuminance));
        }

        [Fact]
        public void FieldsFollowTheOrderOfTheVariants()
        {
            Assert.Equal("m_cs", ShaderVariants.GameFields[(int)ShaderVariant.Normal]);
            Assert.Equal("m_csAlphaLuminance", ShaderVariants.GameFields[(int)ShaderVariant.AlphaLuminance]);
            Assert.Equal("m_csSkip", ShaderVariants.GameFields[(int)ShaderVariant.NoTonemapping]);
        }

        [Fact]
        public void EachVariantCarriesTheGameMacros()
        {
            var s = new Settings();

            var normal = ShaderVariants.Macros(ShaderVariant.Normal, s).Select(m => m.ToString()).ToArray();
            var alpha = ShaderVariants.Macros(ShaderVariant.AlphaLuminance, s).Select(m => m.ToString()).ToArray();
            var skip = ShaderVariants.Macros(ShaderVariant.NoTonemapping, s).Select(m => m.ToString()).ToArray();

            Assert.Equal("NUMTHREADS=8", normal[0]);
            Assert.DoesNotContain("FILL_ALPHA_LUMINANCE", normal);
            Assert.DoesNotContain("DISABLE_TONEMAPPING", normal);
            Assert.Contains("FILL_ALPHA_LUMINANCE", alpha);
            Assert.Contains("DISABLE_TONEMAPPING", skip);
        }

        [Fact]
        public void TheDefaultsGiveTheAcceptedValues()
        {
            var macros = ShaderVariants.Macros(ShaderVariant.Normal, new Settings())
                .ToDictionary(m => m.Name, m => m.Value);

            Assert.Equal("3", macros["CEL_SHADE_TONES"]);
            Assert.Equal("0.7f", macros["CEL_STRENGTH"]);
            Assert.Equal("0.75f", macros["CEL_THRESHOLD"]);
            Assert.Equal("0.2f", macros["CEL_RIM"]);
            Assert.Equal("0.6f", macros["CEL_HAZE"]);
            Assert.Equal(5, macros.Count - 1); // the five macros of the rendering, plus NUMTHREADS
        }

        [Fact]
        public void NumbersAreWrittenWithADotWhateverTheLanguage()
        {
            var previous = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                var settings = new Settings
                {
                    AnimatedFilm = new AnimatedFilmSettings { OutlineStrength = 85, Haze = 35 },
                };
                var macros = ShaderVariants.Macros(ShaderVariant.Normal, settings).ToDictionary(m => m.Name, m => m.Value);

                Assert.Equal("0.85f", macros["CEL_STRENGTH"]);
                Assert.Equal("0.35f", macros["CEL_HAZE"]);
                Assert.Equal("0.75f", macros["CEL_THRESHOLD"]);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void TheSwitchNeedsNoRecompiling()
        {
            Assert.Equal(
                ShaderVariants.Signature(new Settings { Enabled = true }),
                ShaderVariants.Signature(new Settings { Enabled = false }));
        }

        [Fact]
        public void EverySettingChangesTheVariants()
        {
            var reference = ShaderVariants.Signature(new Settings());

            Assert.NotEqual(reference, ShaderVariants.Signature(new Settings { AnimatedFilm = new AnimatedFilmSettings { ShadeTones = 2 } }));
            Assert.NotEqual(reference, ShaderVariants.Signature(new Settings { AnimatedFilm = new AnimatedFilmSettings { OutlineStrength = 10 } }));
            Assert.NotEqual(reference, ShaderVariants.Signature(new Settings { AnimatedFilm = new AnimatedFilmSettings { RimLight = 10 } }));
            Assert.NotEqual(reference, ShaderVariants.Signature(new Settings { AnimatedFilm = new AnimatedFilmSettings { Haze = 10 } }));
        }
    }
}
