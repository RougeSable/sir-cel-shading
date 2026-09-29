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
        public void EachStyleHasItsOwnBranch()
        {
            Assert.Contains("#define CEL_STYLE_COMIC_BOOK " + (int)CelStyle.ComicBook, ShaderSource.Text);
            Assert.Contains("#define CEL_STYLE_ANIMATED_FILM " + (int)CelStyle.AnimatedFilm, ShaderSource.Text);
            Assert.Contains("#define CEL_STYLE_CLEAR_LINE " + (int)CelStyle.ClearLine, ShaderSource.Text);
            Assert.Contains("#if CEL_STYLE == CEL_STYLE_COMIC_BOOK", ShaderSource.Text);
            Assert.Contains("#elif CEL_STYLE == CEL_STYLE_ANIMATED_FILM", ShaderSource.Text);
        }

        [Fact]
        public void ComicBookKeepsItsOriginalRendering()
        {
            // The two lines of the version before the styles, macros renamed.
            Assert.Contains("    color = CelFlatColors(color);\n    color *= 1 - CelOutline(texel) * CEL_STRENGTH;\n", ShaderSource.Text);
            Assert.Contains("#define CEL_OUTLINE_SPAN 2\n", ShaderSource.Text);
            Assert.Contains("return smoothstep(CEL_THRESHOLD, CEL_OUTLINE_SPAN * CEL_THRESHOLD, measure);", ShaderSource.Text);
            Assert.Contains("#define CEL_SOFTNESS 0.06f", ShaderSource.Text);
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
            foreach (var style in CelStyles.MenuOrder)
            {
                var s = new Settings { Style = style };

                var normal = ShaderVariants.Macros(ShaderVariant.Normal, s).Select(m => m.ToString()).ToArray();
                var alpha = ShaderVariants.Macros(ShaderVariant.AlphaLuminance, s).Select(m => m.ToString()).ToArray();
                var skip = ShaderVariants.Macros(ShaderVariant.NoTonemapping, s).Select(m => m.ToString()).ToArray();

                Assert.Equal("NUMTHREADS=8", normal[0]);
                Assert.DoesNotContain("FILL_ALPHA_LUMINANCE", normal);
                Assert.DoesNotContain("DISABLE_TONEMAPPING", normal);
                Assert.Contains("FILL_ALPHA_LUMINANCE", alpha);
                Assert.Contains("DISABLE_TONEMAPPING", skip);
                Assert.Contains("CEL_STYLE=" + (int)style, normal);
            }
        }

        [Fact]
        public void ComicBookGetsTheSameValuesAsBefore()
        {
            var macros = ShaderVariants.Macros(ShaderVariant.Normal, new Settings { Style = CelStyle.ComicBook })
                .ToDictionary(m => m.Name, m => m.Value);

            Assert.Equal("4", macros["CEL_TONES"]);
            Assert.Equal("1", macros["CEL_WIDTH"]);
            Assert.Equal("1.0f", macros["CEL_STRENGTH"]);
            Assert.Equal("0.75f", macros["CEL_THRESHOLD"]);
            Assert.Equal("1.15f", macros["CEL_SATURATION"]);
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
                    Style = CelStyle.ComicBook,
                    ComicBook = new ComicBookSettings { OutlineStrength = 85, Vibrance = 115, EdgeSensitivity = 3 },
                };
                var macros = ShaderVariants.Macros(ShaderVariant.Normal, settings).ToDictionary(m => m.Name, m => m.Value);

                Assert.Equal("0.85f", macros["CEL_STRENGTH"]);
                Assert.Equal("1.15f", macros["CEL_SATURATION"]);
                Assert.Equal("0.75f", macros["CEL_THRESHOLD"]);

                settings.Style = CelStyle.AnimatedFilm;
                settings.AnimatedFilm.Haze = 35;
                Assert.Equal("0.35f", ShaderVariants.Macros(ShaderVariant.Normal, settings).Single(m => m.Name == "CEL_HAZE").Value);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void SensitivityLowersTheThreshold()
        {
            for (var s = ComicBookSettings.EdgeSensitivityMin; s < ComicBookSettings.EdgeSensitivityMax; s++)
                Assert.True(ShaderVariants.Threshold(s + 1) < ShaderVariants.Threshold(s));
        }

        [Fact]
        public void TheSwitchNeedsNoRecompiling()
        {
            Assert.Equal(
                ShaderVariants.Signature(new Settings { Enabled = true }),
                ShaderVariants.Signature(new Settings { Enabled = false }));
        }

        [Fact]
        public void EachStyleCompilesItsOwnVariants()
        {
            var signatures = CelStyles.MenuOrder
                .Select(style => ShaderVariants.Signature(new Settings { Style = style }))
                .Distinct()
                .Count();
            Assert.Equal(3, signatures);
        }

        [Fact]
        public void OnlyAlbedoStylesReadTheGBuffer()
        {
            Assert.False(ShaderVariants.NeedsAlbedo(CelStyle.ComicBook));
            Assert.True(ShaderVariants.NeedsAlbedo(CelStyle.AnimatedFilm));
            Assert.True(ShaderVariants.NeedsAlbedo(CelStyle.ClearLine));
        }

        // Every setting of a style pushed to its maximum, then to its minimum.
        private static void PushEveryOtherStyle(Settings s, CelStyle kept, bool max)
        {
            if (kept != CelStyle.ComicBook)
                s.ComicBook = max
                    ? new ComicBookSettings { Tones = ComicBookSettings.TonesMax, OutlineWidth = ComicBookSettings.OutlineWidthMax, OutlineStrength = ComicBookSettings.OutlineStrengthMin, EdgeSensitivity = ComicBookSettings.EdgeSensitivityMax, Vibrance = ComicBookSettings.VibranceMax }
                    : new ComicBookSettings { Tones = ComicBookSettings.TonesMin, OutlineWidth = ComicBookSettings.OutlineWidthMin, OutlineStrength = ComicBookSettings.OutlineStrengthMin, EdgeSensitivity = ComicBookSettings.EdgeSensitivityMin, Vibrance = ComicBookSettings.VibranceMin };
            if (kept != CelStyle.AnimatedFilm)
                s.AnimatedFilm = max
                    ? new AnimatedFilmSettings { ShadeTones = AnimatedFilmSettings.ShadeTonesMax, OutlineStrength = AnimatedFilmSettings.OutlineStrengthMax, RimLight = AnimatedFilmSettings.RimLightMax, Haze = AnimatedFilmSettings.HazeMax }
                    : new AnimatedFilmSettings { ShadeTones = AnimatedFilmSettings.ShadeTonesMin, OutlineStrength = AnimatedFilmSettings.OutlineStrengthMin, RimLight = AnimatedFilmSettings.RimLightMin, Haze = AnimatedFilmSettings.HazeMin };
            if (kept != CelStyle.ClearLine)
                s.ClearLine = max
                    ? new ClearLineSettings { OutlineStrength = ClearLineSettings.OutlineStrengthMax, EdgeSensitivity = ClearLineSettings.EdgeSensitivityMax, Shadows = ClearLineSettings.ShadowsMax, Vibrance = ClearLineSettings.VibranceMax }
                    : new ClearLineSettings { OutlineStrength = ClearLineSettings.OutlineStrengthMin, EdgeSensitivity = ClearLineSettings.EdgeSensitivityMin, Shadows = ClearLineSettings.ShadowsMin, Vibrance = ClearLineSettings.VibranceMin };
        }

        [Theory]
        [InlineData(CelStyle.ComicBook)]
        [InlineData(CelStyle.AnimatedFilm)]
        [InlineData(CelStyle.ClearLine)]
        public void TheSettingsOfOneStyleNeverChangeAnother(CelStyle style)
        {
            var reference = new Settings { Style = style };
            var pushedUp = new Settings { Style = style };
            var pushedDown = new Settings { Style = style };
            PushEveryOtherStyle(pushedUp, style, true);
            PushEveryOtherStyle(pushedDown, style, false);

            foreach (var variant in new[] { ShaderVariant.Normal, ShaderVariant.AlphaLuminance, ShaderVariant.NoTonemapping })
            {
                var expected = ShaderVariants.Macros(variant, reference).Select(m => m.ToString()).ToArray();
                Assert.Equal(expected, ShaderVariants.Macros(variant, pushedUp).Select(m => m.ToString()).ToArray());
                Assert.Equal(expected, ShaderVariants.Macros(variant, pushedDown).Select(m => m.ToString()).ToArray());
            }
            Assert.Equal(ShaderVariants.Signature(reference), ShaderVariants.Signature(pushedUp));
            Assert.Equal(ShaderVariants.Signature(reference), ShaderVariants.Signature(pushedDown));
        }

        [Theory]
        [InlineData(CelStyle.ComicBook)]
        [InlineData(CelStyle.AnimatedFilm)]
        [InlineData(CelStyle.ClearLine)]
        public void TheSettingsOfTheSelectedStyleDoChangeIt(CelStyle style)
        {
            var reference = new Settings { Style = style };
            var changed = new Settings { Style = style };
            PushEveryOtherStyle(changed, CelStyle.ComicBook, true);
            PushEveryOtherStyle(changed, CelStyle.AnimatedFilm, true);
            PushEveryOtherStyle(changed, CelStyle.ClearLine, true);
            // PushEveryOtherStyle leaves the kept style alone; three passes with
            // a different kept style push all three.

            Assert.NotEqual(ShaderVariants.Signature(reference), ShaderVariants.Signature(changed));
        }
    }
}
