using System;
using System.Collections.Generic;

namespace SirCelShading
{
    // Deux greffons ne se disputent jamais une même étape du jeu. Avant de
    // remplacer ce qu'utilise la passe des couleurs finales, on regarde qui s'y
    // est déjà accroché (Harmony.GetPatchInfo) : tout propriétaire autre que
    // nous, et nous cédons la place.
    public static class Coexistence
    {
        public static List<string> OtherOwners(IEnumerable<string> owners, string ourId)
        {
            var others = new List<string>();
            if (owners == null)
                return others;

            foreach (var owner in owners)
            {
                if (string.IsNullOrEmpty(owner))
                    continue;
                if (string.Equals(owner, ourId, StringComparison.Ordinal))
                    continue;
                if (!others.Contains(owner))
                    others.Add(owner);
            }
            return others;
        }
    }
}
