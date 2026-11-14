using System;

namespace SirCelShading
{
    public enum CelAction
    {
        // Le message n'est pas pour le greffon : il part normalement.
        None,
        Toggle,
        Enable,
        Disable,
        Status,
        Help,
        Unknown,
    }

    // La commande du joueur dans la fenêtre de tchat. Elle reste sur la
    // machine du joueur : rien ne part vers le serveur ni vers les autres
    // joueurs.
    public static class CelCommand
    {
        public const string Prefix = "/cel";

        public static CelAction Parse(string text)
        {
            if (text == null)
                return CelAction.None;

            var t = text.Trim();
            if (!t.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return CelAction.None;

            var rest = t.Substring(Prefix.Length);
            if (rest.Length > 0 && !char.IsWhiteSpace(rest[0]))
                return CelAction.None; // « /cellar » n'est pas notre commande

            // Les options sont en français ; les anciennes graphies (on, off,
            // status, help) restent acceptées, sans être annoncées.
            switch (rest.Trim().ToLowerInvariant())
            {
                case "":
                    return CelAction.Toggle;
                case "activer":
                case "on":
                    return CelAction.Enable;
                case "couper":
                case "off":
                    return CelAction.Disable;
                case "etat":
                case "état":
                case "status":
                    return CelAction.Status;
                case "aide":
                case "help":
                    return CelAction.Help;
                default:
                    return CelAction.Unknown;
            }
        }

        // Ce qu'une commande fait aux réglages. Rend faux quand la commande ne
        // change rien (état, aide).
        public static bool Apply(CelAction action, Settings settings)
        {
            switch (action)
            {
                case CelAction.Toggle:
                    settings.Enabled = !settings.Enabled;
                    return true;
                case CelAction.Enable:
                    settings.Enabled = true;
                    return true;
                case CelAction.Disable:
                    settings.Enabled = false;
                    return true;
                default:
                    return false;
            }
        }
    }
}
