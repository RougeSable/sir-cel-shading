using System;

namespace SirCelShading
{
    // Where the reports go: the channel shared by every client-side plugin
    // of the studio, and the servers that listen to it.
    public static class ReportChannel
    {
        // The number fixed by the rule of the studio's client-side plugins.
        public const long Number = 5465479001L;

        // The game's network messages (MyAPIGateway.Multiplayer) take a
        // 16-bit channel: 5465479001 does not fit. The game would keep its
        // low 16 bits, so that is what is sent, written out here so that the
        // server's listener and every plugin agree on it: 38745.
        public static readonly ushort GameChannel = unchecked((ushort)(Number & 0xFFFF));

        // The studio's servers, PU and PTU, all carry this word in their
        // name, which the game shows in its server list.
        public const string ServerMark = "Sirius";

        // True on a dedicated server of the studio, joined as a client.
        // Single player, a hosted game or anyone else's server: false, and
        // nothing leaves the player's machine.
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
