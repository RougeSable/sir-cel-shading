using System;
using System.IO;
using System.Xml.Serialization;

namespace SirCelShading
{
    // Reading and writing the settings. A missing or unreadable file never
    // blocks the game: the defaults are used instead.
    public static class SettingsFile
    {
        public const string FileName = "settings.xml";

        // The file written by versions before the choice of style. It is read
        // once, when settings.xml does not exist yet, so that the Comic book
        // settings and the on/off switch of an existing player are kept.
        public const string LegacyFileName = "reglages.xml";

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Settings));
        private static readonly XmlSerializer LegacySerializer = new XmlSerializer(typeof(LegacySettings));

        public static string PathIn(string directory)
        {
            return Path.Combine(directory, FileName);
        }

        public static Settings Load(string directory, out string problem)
        {
            problem = null;
            var path = PathIn(directory);
            if (File.Exists(path))
                return Read(path, Serializer, out problem, x => ((Settings)x).Normalized());

            var legacyPath = Path.Combine(directory, LegacyFileName);
            if (File.Exists(legacyPath))
                return Read(legacyPath, LegacySerializer, out problem, x => ((LegacySettings)x).ToSettings());

            return new Settings();
        }

        private static Settings Read(string path, XmlSerializer serializer, out string problem, Func<object, Settings> convert)
        {
            problem = null;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var read = serializer.Deserialize(stream);
                    if (read == null)
                    {
                        problem = "empty settings file " + path;
                        return new Settings();
                    }
                    return convert(read);
                }
            }
            catch (Exception e)
            {
                problem = "unreadable settings file " + path + " (" + e.Message + ")";
                return new Settings();
            }
        }

        public static void Save(string directory, Settings settings)
        {
            Directory.CreateDirectory(directory);
            var path = PathIn(directory);

            // Written next to the file, then swapped in: a crash of the game
            // never leaves a half-written settings file.
            var temporary = path + ".tmp";
            using (var stream = File.Create(temporary))
                Serializer.Serialize(stream, settings.Normalized());

            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }
    }

    // The settings file of versions before the choice of style, element names
    // included: they are the names that file really contains. It never held a
    // style, so an imported player gets the default style, like a new player.
    [XmlRoot("Reglages")]
    public class LegacySettings
    {
        [XmlElement("Active")]
        public bool Enabled { get; set; } = true;

        [XmlElement("Teintes")]
        public int Tones { get; set; } = ComicBookSettings.TonesDefault;

        [XmlElement("Epaisseur")]
        public int OutlineWidth { get; set; } = ComicBookSettings.OutlineWidthDefault;

        [XmlElement("Force")]
        public int OutlineStrength { get; set; } = ComicBookSettings.OutlineStrengthDefault;

        [XmlElement("Sensibilite")]
        public int EdgeSensitivity { get; set; } = ComicBookSettings.EdgeSensitivityDefault;

        [XmlElement("Vivacite")]
        public int Vibrance { get; set; } = ComicBookSettings.VibranceDefault;

        public Settings ToSettings()
        {
            var settings = new Settings
            {
                Enabled = Enabled,
                Style = CelStyles.Default,
                ComicBook = new ComicBookSettings
                {
                    Tones = Tones,
                    OutlineWidth = OutlineWidth,
                    OutlineStrength = OutlineStrength,
                    EdgeSensitivity = EdgeSensitivity,
                    Vibrance = Vibrance,
                },
            };
            return settings.Normalized();
        }
    }
}
