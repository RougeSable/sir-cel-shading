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

        // The file written by the first version. It is read once, when
        // settings.xml does not exist yet, so that the on/off switch of an
        // existing player is kept.
        public const string LegacyFileName = "reglages.xml";

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Settings));
        private static readonly XmlSerializer LegacySerializer = new XmlSerializer(typeof(LegacySettings));

        public static string PathIn(string directory)
        {
            return Path.Combine(directory, FileName);
        }

        // Whatever an earlier version wrote (a style, the settings of other
        // styles), the result is the Animated film rendering: elements this
        // version does not know are ignored.
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
}
