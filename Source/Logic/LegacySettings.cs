using System.Xml.Serialization;

namespace SirCelShading
{
    // The settings file of the first version, element names included: they are
    // the names that file really contains. Only the on/off switch is kept; the
    // rest of that file (the settings of a rendering that no longer exists) is
    // ignored when it is read.
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
