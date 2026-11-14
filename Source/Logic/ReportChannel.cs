using System;

namespace SirCelShading
{
    // Où vont les rapports : le canal commun à tous les greffons côté joueur
    // du studio, et les serveurs qui l'écoutent.
    public static class ReportChannel
    {
        // Le numéro fixé par la règle des greffons côté joueur du studio.
        public const long Number = 5465479001L;

        // Les messages réseau du jeu (MyAPIGateway.Multiplayer) prennent un
        // canal sur 16 bits : 5465479001 n'y tient pas. Le jeu en garderait les
        // 16 bits de poids faible, et c'est donc ce qu'on envoie, écrit ici
        // pour que l'écouteur du serveur et chaque greffon s'accordent : 38745.
        public static readonly ushort GameChannel = unchecked((ushort)(Number & 0xFFFF));

        // Les serveurs du studio, PU et PTU, portent tous ce mot dans leur
        // nom, que le jeu montre dans sa liste de serveurs.
        public const string ServerMark = "Sirius";

        // Vrai sur un serveur dédié du studio, rejoint en client. Solo, partie
        // hébergée ou serveur d'un autre : faux, et rien ne quitte la machine
        // du joueur.
        public static bool IsOurServer(bool multiplayer, bool isServer, bool dedicated, string serverName, string worldName)
        {
            if (!multiplayer || isServer || !dedicated)
                return false;
            return Carries(serverName) || Carries(worldName);
        }

        private static bool Carries(string name)
        {
            return !string.IsNullOrEmpty(name) && name.IndexOf(ServerMark, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
