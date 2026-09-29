using System;
using System.Xml.Serialization;

namespace SirCelShading
{
    // The player's settings, saved in Storage\sir-cel-shading. No dependency on
    // the game: the class is tested as is.
    //
    // The plugin has a single rendering, Animated film: the settings are the
    // on/off switch and the Animated film group. A file written by an earlier
    // version may hold more (a style, the groups of other styles): those
    // elements are ignored when the file is read, and the player gets Animated
    // film all the same.
    [XmlRoot("Settings")]
    public class Settings
    {
        // The first setting: the whole effect, on from installation.
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

        // Brings every value back within its bounds: a file edited by hand can
        // never produce an absurd shader variant.
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
