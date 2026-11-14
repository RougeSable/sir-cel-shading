using System;
using System.Collections.Generic;
using System.Globalization;

namespace SirCelShading
{
    // Les trois variantes de la passe des couleurs finales du jeu, dans l'ordre
    // de leurs champs statiques de MyToneMapping : m_cs, m_csAlphaLuminance,
    // m_csSkip.
    public enum ShaderVariant
    {
        Normal = 0,
        AlphaLuminance = 1,
        NoTonemapping = 2,
    }

    // Une définition de macro, sans dépendance à SharpDX : le code côté jeu la
    // transforme en ShaderMacro. Une valeur nulle définit la macro sans valeur,
    // exactement comme le jeu le fait pour ses drapeaux.
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

        // Seuil de courbure relatif des contours : ils n'ont pas de réglage
        // propre pour cela.
        public const float EdgeThreshold = 0.75f;

        // Ce que choisit MyToneMapping.Run, reproduit à l'identique :
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
                // Les mêmes macros que le jeu pour la même variante (MyToneMapping.Init).
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

        // Deux réglages de même signature donnent les mêmes variantes : inutile
        // de recompiler. L'interrupteur n'en fait pas partie.
        public static string Signature(Settings settings)
        {
            var parts = new List<string>();
            foreach (var macro in Macros(ShaderVariant.Normal, settings))
                parts.Add(macro.ToString());
            return string.Join("|", parts.ToArray());
        }

        // Une variante que le jeu a refusé de compiler ou de créer. L'effet
        // s'arrête pour la session : une ligne au journal du jeu, une
        // notification au joueur et, par le journal, un rapport « erreur ».
        public static bool Refuse(SessionStop stop, string description, string how, Exception error)
        {
            if (stop == null)
                return false;
            return stop.Stop("variante " + description + " " + how, Texts.StopVariantRefused, error);
        }

        public const string RefusedByCompiler = "refusée par le compilateur du jeu";
        public const string RefusedWithoutDetail = "refusée par le compilateur du jeu (détails dans le journal de rendu)";
        public const string RefusedAtCreation = "refusée à la création";

        private static string Integer(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // Toujours avec un point décimal et un suffixe f : le compilateur HLSL
        // ne connaît pas la virgule décimale, quelle que soit la langue du
        // joueur.
        private static string Float(float value)
        {
            return value.ToString("0.0###", CultureInfo.InvariantCulture) + "f";
        }
    }
}
