using System;
using System.IO;
using System.Xml.Serialization;

namespace SirCelShading
{
    // Lecture et écriture des réglages. Un fichier absent ou illisible ne
    // bloque jamais le jeu : on repart des valeurs par défaut.
    public static class FichierReglages
    {
        public const string NomDuFichier = "reglages.xml";

        private static readonly XmlSerializer Serialiseur = new XmlSerializer(typeof(Reglages));

        public static Reglages Charger(string chemin, out string probleme)
        {
            probleme = null;
            if (!File.Exists(chemin))
                return new Reglages();

            try
            {
                using (var flux = File.OpenRead(chemin))
                {
                    var lus = Serialiseur.Deserialize(flux) as Reglages;
                    if (lus == null)
                    {
                        probleme = "fichier de réglages vide";
                        return new Reglages();
                    }
                    return lus.Normalises();
                }
            }
            catch (Exception e)
            {
                probleme = "fichier de réglages illisible (" + e.Message + ")";
                return new Reglages();
            }
        }

        public static void Enregistrer(string chemin, Reglages reglages)
        {
            var dossier = Path.GetDirectoryName(chemin);
            if (!string.IsNullOrEmpty(dossier))
                Directory.CreateDirectory(dossier);

            // Écriture dans un fichier voisin puis remplacement : un arrêt brutal
            // du jeu ne laisse jamais un fichier de réglages à moitié écrit.
            var provisoire = chemin + ".tmp";
            using (var flux = File.Create(provisoire))
                Serialiseur.Serialize(flux, reglages.Normalises());

            if (File.Exists(chemin))
                File.Delete(chemin);
            File.Move(provisoire, chemin);
        }
    }
}
