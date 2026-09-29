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

        // Relative curvature threshold of the outlines: they have no setting
        // of their own for it.
        public const float EdgeThreshold = 0.75f;

        // What MyToneMapping.Run picks, reproduced exactly:
        // (!enableTonemapping) ? m_csSkip : (needsAlphaLuminance ? m_csAlphaLuminance : m_cs)
        public static ShaderVariant Choose(bool enableTonemapping, bool needsAlphaLuminance)
        {
            if (!enableTonemapping)
                return ShaderVariant.NoTonemapping;
            return needsAlphaLuminance ? ShaderVariant.AlphaLuminance : ShaderVariant.Normal;
        }

        public static List<MacroDefinition> Macros(ShaderVariant variant, Settings settings)
        {
            var a = settings.Normalized().AnimatedFilm;
            var macros = new List<MacroDefinition>
            {
                // Same macros as the game for the same variant (MyToneMapping.Init).
                new MacroDefinition("NUMTHREADS", "8"),
            };

            if (variant == ShaderVariant.AlphaLuminance)
                macros.Add(new MacroDefinition("FILL_ALPHA_LUMINANCE", null));
            else if (variant == ShaderVariant.NoTonemapping)
                macros.Add(new MacroDefinition("DISABLE_TONEMAPPING", null));

            macros.Add(new MacroDefinition("CEL_SHADE_TONES", Integer(a.ShadeTones)));
            macros.Add(new MacroDefinition("CEL_STRENGTH", Float(a.OutlineStrength / 100f)));
            macros.Add(new MacroDefinition("CEL_THRESHOLD", Float(EdgeThreshold)));
            macros.Add(new MacroDefinition("CEL_RIM", Float(a.RimLight / 100f)));
            macros.Add(new MacroDefinition("CEL_HAZE", Float(a.Haze / 100f)));
            return macros;
        }

        // Two settings with the same signature give the same variants: no need
        // to compile again. The on/off switch is not part of it.
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
