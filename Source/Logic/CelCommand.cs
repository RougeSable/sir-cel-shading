using System;

namespace SirCelShading
{
    public enum CelAction
    {
        // The message is not for the plugin: it is sent normally.
        None,
        Toggle,
        Enable,
        Disable,
        Status,
        ComicBook,
        AnimatedFilm,
        ClearLine,
        Unknown,
    }

    // The player's command in the chat window. It stays on the player's
    // machine: nothing goes to the server or to other players.
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
                return CelAction.None; // "/cellar" is not our command

            switch (rest.Trim().ToLowerInvariant())
            {
                case "":
                    return CelAction.Toggle;
                case "on":
                    return CelAction.Enable;
                case "off":
                    return CelAction.Disable;
                case "status":
                    return CelAction.Status;
                case "comic":
                    return CelAction.ComicBook;
                case "animated":
                    return CelAction.AnimatedFilm;
                case "clearline":
                    return CelAction.ClearLine;
                default:
                    return CelAction.Unknown;
            }
        }

        // The style a command switches to, or null for the other commands.
        public static CelStyle? StyleOf(CelAction action)
        {
            switch (action)
            {
                case CelAction.ComicBook:
                    return CelStyle.ComicBook;
                case CelAction.AnimatedFilm:
                    return CelStyle.AnimatedFilm;
                case CelAction.ClearLine:
                    return CelStyle.ClearLine;
                default:
                    return null;
            }
        }

        // What a command does to the settings. Picking a style also turns the
        // effect on: the player asked to see that style. Returns false when the
        // command changes nothing (status, help).
        public static bool Apply(CelAction action, Settings settings)
        {
            var style = StyleOf(action);
            if (style.HasValue)
            {
                settings.Style = style.Value;
                settings.Enabled = true;
                return true;
            }

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
