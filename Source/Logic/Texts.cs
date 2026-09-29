namespace SirCelShading
{
    // Everything the player reads, in one place.
    public static class Texts
    {
        public const string DisplayName = "Sir Cel Shading";

        public const string On = "Sir Cel Shading: on.";
        public const string Off = "Sir Cel Shading: off, back to the game's own rendering.";
        public const string CommandHelp = "Sir Cel Shading: /cel turns the effect on or off; /cel on, /cel off, /cel status.";

        public const string StopPrefix = "Sir Cel Shading is stopped for this session: ";
        public const string StopUnexpectedEngine = StopPrefix + "the game's render engine has changed. The game's rendering is kept.";
        public const string StopMissingHeader = StopPrefix + "a shader of the game is missing. The game's rendering is kept.";
        public const string StopShaderFile = StopPrefix + "its shader could not be written to the player's folder.";
        public const string StopVariantRefused = StopPrefix + "the game refused to compile its shader. The game's rendering is kept.";
        public const string StopFault = StopPrefix + "a fault occurred during rendering. The game's rendering is kept.";
        public const string StopPatch = StopPrefix + "it could not hook into the game's final colors.";
        public const string StopCoexistence = StopPrefix + "another plugin already uses the game's final colors ({0}). Sir Cel Shading steps aside.";

        // The answer to /cel status, and the message after every change: it
        // always says whether the effect is on.
        public static string Status(bool enabled, string stopReason)
        {
            if (stopReason != null)
                return stopReason;
            return enabled ? On : Off;
        }

        // Settings screen, opened from Pulsar.
        public const string ScreenTitle = "Sir Cel Shading";
        public const string EnableCheckbox = "Enable plugin";
        public const string EnableCheckboxHelp = "Animated film cel shading of the game view. Unchecked, the game gets exactly its own rendering back.";

        public const string ShadeTones = "Shadow tones";
        public const string ShadeTonesHelp = "Number of soft light tones on a surface, shadow included.";
        public const string OutlineStrength = "Outline strength";
        public const string OutlineStrengthHelp = "Outlines take a darker shade of the object. 0 %: no outline.";
        public const string RimLight = "Rim light";
        public const string RimLightHelp = "Light edge on silhouettes seen against the sun.";
        public const string Haze = "Distance haze";
        public const string HazeHelp = "How much distant things fade into the sky color. No effect under a black sky.";

        public const string DefaultsButton = "Defaults";
        public const string DefaultsButtonHelp = "Resets the rendering settings to their defaults.";
        public const string CloseButton = "Close";
        public const string ScreenStopped = "Stopped for this session, see the game log.";
    }
}
