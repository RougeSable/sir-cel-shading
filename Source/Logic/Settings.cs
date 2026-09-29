using System;
using System.Xml.Serialization;

namespace SirCelShading
{
    // The three rendering styles, in the order the settings screen lists them.
    // The numeric values are the CEL_STYLE macro given to the shader.
    public enum CelStyle
    {
        ComicBook = 0,
        AnimatedFilm = 1,
        ClearLine = 2,
    }

    public static class CelStyles
    {
        // A player who never picked a style gets Clear line, the most readable
        // of the three, including players who already had Sir Cel Shading.
        public const CelStyle Default = CelStyle.ClearLine;

        public static readonly CelStyle[] MenuOrder = { CelStyle.ComicBook, CelStyle.AnimatedFilm, CelStyle.ClearLine };

        public static bool IsDefined(CelStyle style)
        {
            return style == CelStyle.ComicBook || style == CelStyle.AnimatedFilm || style == CelStyle.ClearLine;
        }

        // Reads the name stored in the settings file. Anything unknown (a file
        // edited by hand, a style from a later version) falls back to the default.
        public static CelStyle Parse(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var style in MenuOrder)
                {
                    if (string.Equals(style.ToString(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                        return style;
                }
            }
            return Default;
        }
    }

    // The player's settings, saved in Storage\sir-cel-shading. No dependency on
    // the game: the class is tested as is.
    //
    // Each style keeps its own settings in its own group: a setting of one
    // style never reaches the shader of another (ShaderVariants.Macros).
    [XmlRoot("Settings")]
    public class Settings
    {
        // The first setting: the whole effect, on from installation.
        public bool Enabled { get; set; } = true;

        [XmlIgnore]
        public CelStyle Style { get; set; } = CelStyles.Default;

        // Stored by name, so that a file stays readable when styles are added.
        [XmlElement("Style")]
        public string StyleName
        {
            get { return Style.ToString(); }
            set { Style = CelStyles.Parse(value); }
        }

        public ComicBookSettings ComicBook { get; set; } = new ComicBookSettings();

        public AnimatedFilmSettings AnimatedFilm { get; set; } = new AnimatedFilmSettings();

        public ClearLineSettings ClearLine { get; set; } = new ClearLineSettings();

        public Settings Copy()
        {
            return new Settings
            {
                Enabled = Enabled,
                Style = Style,
                ComicBook = (ComicBook ?? new ComicBookSettings()).Copy(),
                AnimatedFilm = (AnimatedFilm ?? new AnimatedFilmSettings()).Copy(),
                ClearLine = (ClearLine ?? new ClearLineSettings()).Copy(),
            };
        }

        // Brings every value back within its bounds: a file edited by hand can
        // never produce an absurd shader variant.
        public Settings Normalized()
        {
            var s = Copy();
            if (!CelStyles.IsDefined(s.Style))
                s.Style = CelStyles.Default;
            s.ComicBook = s.ComicBook.Normalized();
            s.AnimatedFilm = s.AnimatedFilm.Normalized();
            s.ClearLine = s.ClearLine.Normalized();
            return s;
        }

        public static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }

    // Comic book: the original Sir Cel Shading rendering, unchanged. Black
    // outlines and flat colors.
    public class ComicBookSettings
    {
        public const int TonesMin = 3;
        public const int TonesMax = 8;
        public const int TonesDefault = 4;

        public const int OutlineWidthMin = 1;
        public const int OutlineWidthMax = 3;
        public const int OutlineWidthDefault = 1;

        public const int OutlineStrengthMin = 0;
        public const int OutlineStrengthMax = 100;
        public const int OutlineStrengthDefault = 100;

        public const int EdgeSensitivityMin = 1;
        public const int EdgeSensitivityMax = 5;
        public const int EdgeSensitivityDefault = 3;

        public const int VibranceMin = 100;
        public const int VibranceMax = 150;
        public const int VibranceDefault = 115;

        // Tones per color: the fewer, the wider the flat areas.
        public int Tones { get; set; } = TonesDefault;

        // Outline width, in pixels on each side of the edge.
        public int OutlineWidth { get; set; } = OutlineWidthDefault;

        // Outline darkness, in percent.
        public int OutlineStrength { get; set; } = OutlineStrengthDefault;

        // 1 draws only sharp silhouettes, 5 also draws soft folds.
        public int EdgeSensitivity { get; set; } = EdgeSensitivityDefault;

        // Saturation of the flat colors, in percent (100: the game's).
        public int Vibrance { get; set; } = VibranceDefault;

        public ComicBookSettings Copy()
        {
            return (ComicBookSettings)MemberwiseClone();
        }

        public ComicBookSettings Normalized()
        {
            var s = Copy();
            s.Tones = Settings.Clamp(Tones, TonesMin, TonesMax);
            s.OutlineWidth = Settings.Clamp(OutlineWidth, OutlineWidthMin, OutlineWidthMax);
            s.OutlineStrength = Settings.Clamp(OutlineStrength, OutlineStrengthMin, OutlineStrengthMax);
            s.EdgeSensitivity = Settings.Clamp(EdgeSensitivity, EdgeSensitivityMin, EdgeSensitivityMax);
            s.Vibrance = Settings.Clamp(Vibrance, VibranceMin, VibranceMax);
            return s;
        }
    }

    // Animated film: shadows in two or three soft tones, thin outlines in a
    // darker shade of the object, rim light on backlit silhouettes, distant
    // things fading into the sky.
    public class AnimatedFilmSettings
    {
        public const int ShadeTonesMin = 2;
        public const int ShadeTonesMax = 3;
        public const int ShadeTonesDefault = 3;

        public const int OutlineStrengthMin = 0;
        public const int OutlineStrengthMax = 100;
        public const int OutlineStrengthDefault = 70;

        public const int RimLightMin = 0;
        public const int RimLightMax = 100;
        public const int RimLightDefault = 60;

        public const int HazeMin = 0;
        public const int HazeMax = 100;
        public const int HazeDefault = 60;

        // Number of light tones on a surface, shadow included.
        public int ShadeTones { get; set; } = ShadeTonesDefault;

        // Outline strength, in percent.
        public int OutlineStrength { get; set; } = OutlineStrengthDefault;

        // Rim light on silhouettes seen against the sun, in percent.
        public int RimLight { get; set; } = RimLightDefault;

        // How much distant things fade into the sky color, in percent.
        public int Haze { get; set; } = HazeDefault;

        public AnimatedFilmSettings Copy()
        {
            return (AnimatedFilmSettings)MemberwiseClone();
        }

        public AnimatedFilmSettings Normalized()
        {
            var s = Copy();
            s.ShadeTones = Settings.Clamp(ShadeTones, ShadeTonesMin, ShadeTonesMax);
            s.OutlineStrength = Settings.Clamp(OutlineStrength, OutlineStrengthMin, OutlineStrengthMax);
            s.RimLight = Settings.Clamp(RimLight, RimLightMin, RimLightMax);
            s.Haze = Settings.Clamp(Haze, HazeMin, HazeMax);
            return s;
        }
    }

    // Clear line: thin, even black outlines, bright plain colors, almost no
    // shadows.
    public class ClearLineSettings
    {
        public const int OutlineStrengthMin = 0;
        public const int OutlineStrengthMax = 100;
        public const int OutlineStrengthDefault = 100;

        public const int EdgeSensitivityMin = 1;
        public const int EdgeSensitivityMax = 5;
        public const int EdgeSensitivityDefault = 3;

        public const int ShadowsMin = 0;
        public const int ShadowsMax = 50;
        public const int ShadowsDefault = 15;

        public const int VibranceMin = 100;
        public const int VibranceMax = 160;
        public const int VibranceDefault = 130;

        // Outline darkness, in percent.
        public int OutlineStrength { get; set; } = OutlineStrengthDefault;

        // 1 draws only sharp silhouettes, 5 also draws soft folds.
        public int EdgeSensitivity { get; set; } = EdgeSensitivityDefault;

        // How much of the game's shadows is kept, in percent.
        public int Shadows { get; set; } = ShadowsDefault;

        // Color saturation, in percent (100: the game's).
        public int Vibrance { get; set; } = VibranceDefault;

        public ClearLineSettings Copy()
        {
            return (ClearLineSettings)MemberwiseClone();
        }

        public ClearLineSettings Normalized()
        {
            var s = Copy();
            s.OutlineStrength = Settings.Clamp(OutlineStrength, OutlineStrengthMin, OutlineStrengthMax);
            s.EdgeSensitivity = Settings.Clamp(EdgeSensitivity, EdgeSensitivityMin, EdgeSensitivityMax);
            s.Shadows = Settings.Clamp(Shadows, ShadowsMin, ShadowsMax);
            s.Vibrance = Settings.Clamp(Vibrance, VibranceMin, VibranceMax);
            return s;
        }
    }
}
