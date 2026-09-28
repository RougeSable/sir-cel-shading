using System;
using System.Collections.Generic;

namespace SirCelShading
{
    // Deux greffons ne se disputent jamais une même étape du jeu. Avant de
    // remplacer ce qu'utilise le passage des couleurs finales, on regarde qui
    // s'y est déjà accroché (Harmony.GetPatchInfo) : un propriétaire autre que
    // nous, et on cède la place.
    public static class Cohabitation
    {
        public static List<string> AutresProprietaires(IEnumerable<string> proprietaires, string monIdentifiant)
        {
            var autres = new List<string>();
            if (proprietaires == null)
                return autres;

            foreach (var proprietaire in proprietaires)
            {
                if (string.IsNullOrEmpty(proprietaire))
                    continue;
                if (string.Equals(proprietaire, monIdentifiant, StringComparison.Ordinal))
                    continue;
                if (!autres.Contains(proprietaire))
                    autres.Add(proprietaire);
            }
            return autres;
        }
    }
}
