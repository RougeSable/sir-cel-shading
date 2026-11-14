namespace SirCelShading
{
    // Film d'animation : ombres en deux ou trois tons doux, contours fins d'une
    // teinte plus sombre de l'objet, lumière de contour sur les silhouettes à
    // contre-jour, choses lointaines qui se fondent dans le ciel.
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

        // Nombre de tons de lumière sur une surface, ombre comprise.
        public int ShadeTones { get; set; } = ShadeTonesDefault;

        // Force des contours, en pourcentage.
        public int OutlineStrength { get; set; } = OutlineStrengthDefault;

        // Lumière de contour sur les silhouettes vues contre le soleil, en pourcentage.
        public int RimLight { get; set; } = RimLightDefault;

        // À quel point les choses lointaines se fondent dans la couleur du ciel, en pourcentage.
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
