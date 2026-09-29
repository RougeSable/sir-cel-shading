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
                default:
                    return CelAction.Unknown;
            }
        }

        // What a command does to the settings. Returns false when the command
        // changes nothing (status, help).
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
