using System.Collections.Generic;
using System.Globalization;

namespace SirCelShading
{
    // The three variants of the game's final colors pass, in the order of their
    // static fields in MyToneMapping: m_cs, m_csAlphaLuminance, m_csSkip.
    public enum ShaderVariant
    {
        Normal = 0,
        AlphaLuminance = 1,
        NoTonemapping = 2,
    }

    // A macro definition, with no dependency on SharpDX: the game-side code
    // turns it into a ShaderMacro. A null value defines the macro without a
    // value, exactly like the game does for its flags.
    public struct MacroDefinition
    {
        public readonly string Name;
        public readonly string Value;

        public MacroDefinition(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString()
        {
            return Value == null ? Name : Name + "=" + Value;
        }
    }

    public static class ShaderVariants
    {
        public static readonly string[] GameFields = { "m_cs", "m_csAlphaLuminance", "m_csSkip" };

        public const int VariantCount = 3;

        // Edge sensitivity used by the Animated film outlines, which have no
        // setting of their own for it.
        public const int AnimatedFilmEdgeSensitivity = 3;

        // What MyToneMapping.Run picks, reproduced exactly:
        // (!enableTonemapping) ? m_csSkip : (needsAlphaLuminance ? m_csAlphaLuminance : m_cs)
        public static ShaderVariant Choose(bool enableTonemapping, bool needsAlphaLuminance)
        {
            if (!enableTonemapping)
                return ShaderVariant.NoTonemapping;
            return needsAlphaLuminance ? ShaderVariant.AlphaLuminance : ShaderVariant.Normal;
        }

        // Relative curvature threshold, per sensitivity level (1 to 5).
        private static readonly float[] Thresholds = { 1.6f, 1.1f, 0.75f, 0.5f, 0.33f };

        public static float Threshold(int sensitivity)
        {
            var i = Settings.Clamp(sensitivity, ComicBookSettings.EdgeSensitivityMin, ComicBookSettings.EdgeSensitivityMax)
                - ComicBookSettings.EdgeSensitivityMin;
            return Thresholds[i];
        }

        // Animated film and Clear line split the game's lighting from the
        // colors of the objects: they read the albedo of the scene.
        public static bool NeedsAlbedo(CelStyle style)
        {
            return style == CelStyle.AnimatedFilm || style == CelStyle.ClearLine;
        }

        // Only the settings of the selected style reach the shader: a setting
        // of another style cannot change this one.
        public static List<MacroDefinition> Macros(ShaderVariant variant, Settings settings)
        {
            var s = settings.Normalized();
            var macros = new List<MacroDefinition>
            {
                // Same macros as the game for the same variant (MyToneMapping.Init).
                new MacroDefinition("NUMTHREADS", "8"),
            };

            if (variant == ShaderVariant.AlphaLuminance)
                macros.Add(new MacroDefinition("FILL_ALPHA_LUMINANCE", null));
            else if (variant == ShaderVariant.NoTonemapping)
                macros.Add(new MacroDefinition("DISABLE_TONEMAPPING", null));

            macros.Add(new MacroDefinition("CEL_STYLE", Integer((int)s.Style)));
            macros.AddRange(StyleMacros(s));
            return macros;
        }

        private static IEnumerable<MacroDefinition> StyleMacros(Settings s)
        {
            switch (s.Style)
            {
                case CelStyle.ComicBook:
                {
                    var c = s.ComicBook;
                    return new[]
                    {
                        new MacroDefinition("CEL_TONES", Integer(c.Tones)),
                        new MacroDefinition("CEL_WIDTH", Integer(c.OutlineWidth)),
                        new MacroDefinition("CEL_STRENGTH", Float(c.OutlineStrength / 100f)),
                        new MacroDefinition("CEL_THRESHOLD", Float(Threshold(c.EdgeSensitivity))),
                        new MacroDefinition("CEL_SATURATION", Float(c.Vibrance / 100f)),
                    };
                }
                case CelStyle.AnimatedFilm:
                {
                    var a = s.AnimatedFilm;
                    return new[]
                    {
                        new MacroDefinition("CEL_SHADE_TONES", Integer(a.ShadeTones)),
                        new MacroDefinition("CEL_STRENGTH", Float(a.OutlineStrength / 100f)),
                        new MacroDefinition("CEL_THRESHOLD", Float(Threshold(AnimatedFilmEdgeSensitivity))),
                        new MacroDefinition("CEL_RIM", Float(a.RimLight / 100f)),
                        new MacroDefinition("CEL_HAZE", Float(a.Haze / 100f)),
                    };
                }
                default:
                {
                    var l = s.ClearLine;
                    return new[]
                    {
                        new MacroDefinition("CEL_STRENGTH", Float(l.OutlineStrength / 100f)),
                        new MacroDefinition("CEL_THRESHOLD", Float(Threshold(l.EdgeSensitivity))),
                        new MacroDefinition("CEL_SHADOWS", Float(l.Shadows / 100f)),
                        new MacroDefinition("CEL_SATURATION", Float(l.Vibrance / 100f)),
                    };
                }
            }
        }

        // Two settings with the same signature give the same variants: no need
        // to compile again. The on/off switch is not part of it, and neither
        // are the settings of the styles not in use.
        public static string Signature(Settings settings)
        {
            var parts = new List<string>();
            foreach (var macro in Macros(ShaderVariant.Normal, settings))
                parts.Add(macro.ToString());
            return string.Join("|", parts.ToArray());
        }

        private static string Integer(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // Always with a decimal point and an f suffix: the HLSL compiler knows
        // no decimal comma, whatever the player's language.
        private static string Float(float value)
        {
            return value.ToString("0.0###", CultureInfo.InvariantCulture) + "f";
        }
    }
}
