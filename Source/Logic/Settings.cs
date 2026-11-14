using System;
using System.Xml.Serialization;

namespace SirCelShading
{
    // Les réglages du joueur, enregistrés dans Storage\sir-cel-shading. Aucune
    // dépendance au jeu : la classe est testée telle quelle.
    //
    // Le greffon n'a qu'un seul rendu, le film d'animation : les réglages sont
    // l'interrupteur et le groupe AnimatedFilm. Un fichier écrit par une version
    // antérieure peut en contenir davantage (un style, les groupes d'autres
    // styles) : ces éléments sont ignorés à la lecture, et le joueur a quand
    // même le film d'animation.
    [XmlRoot("Settings")]
    public class Settings
    {
        // Le premier réglage : tout l'effet, activé dès l'installation.
        public bool Enabled { get; set; } = true;

        public AnimatedFilmSettings AnimatedFilm { get; set; } = new AnimatedFilmSettings();

        public Settings Copy()
        {
            return new Settings
            {
                Enabled = Enabled,
                AnimatedFilm = (AnimatedFilm ?? new AnimatedFilmSettings()).Copy(),
            };
        }

        // Ramène chaque valeur dans ses bornes : un fichier modifié à la main ne
        // peut jamais produire une variante de shader absurde.
        public Settings Normalized()
        {
            var s = Copy();
            s.AnimatedFilm = s.AnimatedFilm.Normalized();
            return s;
        }

        public static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
