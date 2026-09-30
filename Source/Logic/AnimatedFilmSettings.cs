namespace SirCelShading
{
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
        public const int RimLightDefault = 20;

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
}
