using System.Xml.Serialization;

namespace SirCelShading
{
    // Le fichier de réglages de la première version, noms d'éléments compris :
    // ce sont les noms que ce fichier contient vraiment. Seul l'interrupteur
    // est gardé ; le reste du fichier (les réglages d'un rendu qui n'existe plus)
    // est ignoré à la lecture.
    [XmlRoot("Reglages")]
    public class LegacySettings
    {
        [XmlElement("Active")]
        public bool Enabled { get; set; } = true;

        public Settings ToSettings()
        {
            return new Settings { Enabled = Enabled };
        }
    }
}
