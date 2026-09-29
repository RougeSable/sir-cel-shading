namespace SirCelShading
{
    // Everything the player reads, in one place.
    public static class Texts
    {
        public const string DisplayName = "Sir Cel Shading";

        public const string ComicBook = "Comic book";
        public const string AnimatedFilm = "Animated film";
        public const string ClearLine = "Clear line";

        public static string StyleName(CelStyle style)
        {
            switch (style)
            {
                case CelStyle.ComicBook:
                    return ComicBook;
                case CelStyle.AnimatedFilm:
                    return AnimatedFilm;
                default:
                    return ClearLine;
            }
        }

        public const string On = "Sir Cel Shading: on, {0} style.";
        public const string Off = "Sir Cel Shading: off, back to the game's own rendering. Selected style: {0}.";
        public const string StoppedStyle = " Selected style: {0}.";
        public const string CommandHelp = "Sir Cel Shading: /cel turns the effect on or off; /cel comic, /cel animated, /cel clearline pick a style; /cel on, /cel off, /cel status.";

        public const string StopPrefix = "Sir Cel Shading is stopped for this session: ";
        public const string StopUnexpectedEngine = StopPrefix + "the game's render engine has changed. The game's rendering is kept.";
        public const string StopMissingHeader = StopPrefix + "a shader of the game is missing. The game's rendering is kept.";
        public const string StopShaderFile = StopPrefix + "its shader could not be written to the player's folder.";
        public const string StopVariantRefused = StopPrefix + "the game refused to compile its shader. The game's rendering is kept.";
        public const string StopFault = StopPrefix + "a fault occurred during rendering. The game's rendering is kept.";
        public const string StopPatch = StopPrefix + "it could not hook into the game's final colors.";
        public const string StopCoexistence = StopPrefix + "another plugin already uses the game's final colors ({0}). Sir Cel Shading steps aside, whatever the style.";

        // The answer to /cel status, and the message after every change: it
        // always names the style in use.
        public static string Status(bool enabled, CelStyle style, string stopReason)
        {
            var name = StyleName(style);
            if (stopReason != null)
                return stopReason + string.Format(StoppedStyle, name);
            return string.Format(enabled ? On : Off, name);
        }

        // Settings screen, opened from Pulsar.
        public const string ScreenTitle = "Sir Cel Shading";
        public const string EnableCheckbox = "Enable plugin";
        public const string EnableCheckboxHelp = "Cel shading of the game view. Unchecked, the game gets exactly its own rendering back.";
        public const string StyleLabel = "Style";
        public const string StyleHelp = "Comic book: black outlines, flat colors. Animated film: soft shadows, colored outlines, rim light, distant haze. Clear line: thin even outlines, bright colors, almost no shadows.";

        public const string ComicTones = "Tones per color";
        public const string ComicTonesHelp = "Fewer tones, wider flat areas.";
        public const string ComicOutlineWidth = "Outline width";
        public const string ComicOutlineWidthHelp = "In pixels, on each side of the edge.";
        public const string ComicOutlineStrength = "Outline darkness";
        public const string ComicOutlineStrengthHelp = "0 %: no outline; 100 %: black outlines.";
        public const string EdgeSensitivity = "Edge sensitivity";
        public const string EdgeSensitivityHelp = "1: silhouettes only; 5: soft folds as well.";
        public const string ColorVibrance = "Color vibrance";
        public const string ColorVibranceHelp = "100 %: the game's saturation.";

        public const string AnimatedShadeTones = "Shadow tones";
        public const string AnimatedShadeTonesHelp = "Number of soft light tones on a surface, shadow included.";
        public const string AnimatedOutlineStrength = "Outline strength";
        public const string AnimatedOutlineStrengthHelp = "Outlines take a darker shade of the object. 0 %: no outline.";
        public const string AnimatedRimLight = "Rim light";
        public const string AnimatedRimLightHelp = "Light edge on silhouettes seen against the sun.";
        public const string AnimatedHaze = "Distance haze";
        public const string AnimatedHazeHelp = "How much distant things fade into the sky color. No effect under a black sky.";

        public const string ClearLineOutlineStrength = "Outline darkness";
        public const string ClearLineOutlineStrengthHelp = "0 %: no outline; 100 %: black outlines.";
        public const string ClearLineShadows = "Shadows kept";
        public const string ClearLineShadowsHelp = "0 %: no shadows at all; 50 %: half of the game's shadows.";

        public const string DefaultsButton = "Style defaults";
        public const string DefaultsButtonHelp = "Resets the settings of the selected style only.";
        public const string CloseButton = "Close";
        public const string ScreenStopped = "Stopped for this session, see the game log.";
    }
}
