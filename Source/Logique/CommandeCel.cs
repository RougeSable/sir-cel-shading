using System;

namespace SirCelShading
{
    public enum ActionCel
    {
        // Le message ne concerne pas le greffon : il part normalement.
        Aucune,
        Basculer,
        Activer,
        Couper,
        Etat,
        Inconnue,
    }

    // La commande du joueur dans la fenêtre de discussion. Elle reste sur sa
    // machine : rien ne part vers le serveur ni vers les autres joueurs.
    public static class CommandeCel
    {
        public const string Prefixe = "/cel";

        public static ActionCel Interpreter(string texte)
        {
            if (texte == null)
                return ActionCel.Aucune;

            var t = texte.Trim();
            if (!t.StartsWith(Prefixe, StringComparison.OrdinalIgnoreCase))
                return ActionCel.Aucune;

            var reste = t.Substring(Prefixe.Length);
            if (reste.Length > 0 && !char.IsWhiteSpace(reste[0]))
                return ActionCel.Aucune; // « /cellule » n'est pas notre commande

            var argument = reste.Trim().ToLowerInvariant();
            switch (argument)
            {
                case "":
                    return ActionCel.Basculer;
                case "activer":
                case "oui":
                    return ActionCel.Activer;
                case "couper":
                case "non":
                    return ActionCel.Couper;
                case "etat":
                case "état":
                    return ActionCel.Etat;
                default:
                    return ActionCel.Inconnue;
            }
        }
    }
}
