using System.Collections.Generic;
using System.Globalization;

namespace SirCelShading
{
    // Les trois variantes du passage des couleurs finales du jeu, dans l'ordre
    // de leurs champs statiques dans MyToneMapping : m_cs, m_csAlphaLuminance,
    // m_csSkip.
    public enum Variante
    {
        Normale = 0,
        LuminanceAlpha = 1,
        SansTonemapping = 2,
    }

    // Une définition de macro, sans dépendance à SharpDX : c'est le code côté
    // jeu qui la traduit en ShaderMacro. Une valeur nulle définit la macro sans
    // valeur, exactement comme le jeu le fait pour ses drapeaux.
    public struct DefinitionDeMacro
    {
        public readonly string Nom;
        public readonly string Valeur;

        public DefinitionDeMacro(string nom, string valeur)
        {
            Nom = nom;
            Valeur = valeur;
        }

        public override string ToString()
        {
            return Valeur == null ? Nom : Nom + "=" + Valeur;
        }
    }

    public static class VariantesDuShader
    {
        public static readonly string[] ChampsDuJeu = { "m_cs", "m_csAlphaLuminance", "m_csSkip" };

        public const int NombreDeVariantes = 3;

        // Ce que MyToneMapping.Run choisit, reproduit à l'identique :
        // (!enableTonemapping) ? m_csSkip : (needsAlphaLuminance ? m_csAlphaLuminance : m_cs)
        public static Variante Choisir(bool enableTonemapping, bool needsAlphaLuminance)
        {
            if (!enableTonemapping)
                return Variante.SansTonemapping;
            return needsAlphaLuminance ? Variante.LuminanceAlpha : Variante.Normale;
        }

        // Seuil de courbure relative, par niveau de sensibilité (1 à 5).
        private static readonly float[] Seuils = { 1.6f, 1.1f, 0.75f, 0.5f, 0.33f };

        public static float Seuil(int sensibilite)
        {
            var i = Reglages.Borner(sensibilite, Reglages.SensibiliteMin, Reglages.SensibiliteMax) - Reglages.SensibiliteMin;
            return Seuils[i];
        }

        public static List<DefinitionDeMacro> Macros(Variante variante, Reglages reglages)
        {
            var r = reglages.Normalises();
            var macros = new List<DefinitionDeMacro>
            {
                // Mêmes macros que le jeu pour la même variante (MyToneMapping.Init).
                new DefinitionDeMacro("NUMTHREADS", "8"),
            };

            if (variante == Variante.LuminanceAlpha)
                macros.Add(new DefinitionDeMacro("FILL_ALPHA_LUMINANCE", null));
            else if (variante == Variante.SansTonemapping)
                macros.Add(new DefinitionDeMacro("DISABLE_TONEMAPPING", null));

            macros.Add(new DefinitionDeMacro("CEL_TEINTES", Nombre(r.Teintes)));
            macros.Add(new DefinitionDeMacro("CEL_EPAISSEUR", Nombre(r.Epaisseur)));
            macros.Add(new DefinitionDeMacro("CEL_FORCE", Flottant(r.Force / 100f)));
            macros.Add(new DefinitionDeMacro("CEL_SEUIL", Flottant(Seuil(r.Sensibilite))));
            macros.Add(new DefinitionDeMacro("CEL_SATURATION", Flottant(r.Vivacite / 100f)));
            return macros;
        }

        // Deux réglages de même signature donnent les mêmes variantes : inutile
        // de recompiler. L'interrupteur n'en fait pas partie.
        public static string Signature(Reglages reglages)
        {
            var r = reglages.Normalises();
            return string.Join("|", new[]
            {
                Nombre(r.Teintes),
                Nombre(r.Epaisseur),
                Nombre(r.Force),
                Nombre(r.Sensibilite),
                Nombre(r.Vivacite),
            });
        }

        private static string Nombre(int valeur)
        {
            return valeur.ToString(CultureInfo.InvariantCulture);
        }

        // Toujours avec un point décimal et un suffixe f : le compilateur HLSL
        // ne connaît pas la virgule, quelle que soit la langue du joueur.
        private static string Flottant(float valeur)
        {
            return valeur.ToString("0.0###", CultureInfo.InvariantCulture) + "f";
        }
    }
}
