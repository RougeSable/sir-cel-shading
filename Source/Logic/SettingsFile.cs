using System;
using System.IO;
using System.Xml.Serialization;

namespace SirCelShading
{
    // Lecture et écriture des réglages. Un fichier absent ou illisible ne
    // bloque jamais le jeu : on prend les valeurs par défaut.
    public static class SettingsFile
    {
        public const string FileName = "settings.xml";

        // Le fichier écrit par la première version. Il est lu une fois, quand
        // settings.xml n'existe pas encore, pour que l'interrupteur d'un joueur
        // déjà installé soit gardé.
        public const string LegacyFileName = "reglages.xml";

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Settings));
        private static readonly XmlSerializer LegacySerializer = new XmlSerializer(typeof(LegacySettings));

        public static string PathIn(string directory)
        {
            return Path.Combine(directory, FileName);
        }

        // Quoi qu'une version antérieure ait écrit (un style, les réglages
        // d'autres styles), le résultat est le rendu film d'animation : les
        // éléments que cette version ne connaît pas sont ignorés.
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
                        problem = "fichier de réglages vide " + path;
                        return new Settings();
                    }
                    return convert(read);
                }
            }
            catch (Exception e)
            {
                problem = "fichier de réglages illisible " + path + " (" + e.Message + ")";
                return new Settings();
            }
        }

        public static void Save(string directory, Settings settings)
        {
            Directory.CreateDirectory(directory);
            var path = PathIn(directory);

            // Écrit à côté du fichier, puis mis à sa place : un plantage du jeu ne
            // laisse jamais un fichier de réglages à moitié écrit.
            var temporary = path + ".tmp";
            using (var stream = File.Create(temporary))
                Serializer.Serialize(stream, settings.Normalized());

            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }
    }
}
