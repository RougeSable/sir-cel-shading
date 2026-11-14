using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.ModAPI;
using VRage.FileSystem;
using VRage.GameServices;
using VRage.Utils;

namespace SirCelShading
{
    // Le seul échange du greffon avec un serveur : ses rapports d'erreurs et de
    // plantages, envoyés sur le canal de rapports du studio tant que le joueur
    // est sur PU ou PTU. Ailleurs ils attendent dans
    // Storage\sir-cel-shading\Reports.
    //
    // Rien ici ne doit casser le jeu : toute faute est avalée.
    internal sealed class ErrorReporting
    {
        // Mises à jour entre deux tours (60 par seconde) : environ une fois par
        // seconde.
        private const int TickInterval = 60;

        private readonly string m_directory;
        private readonly ErrorReporter m_reporter;
        private int m_counter;
        private bool m_playerNameKnown;

        public ErrorReporter Reporter
        {
            get { return m_reporter; }
        }

        private ErrorReporting(string directory, ErrorReporter reporter)
        {
            m_directory = directory;
            m_reporter = reporter;
        }

        // Au démarrage du jeu : le rapporteur, puis le rapport de plantage de la
        // session précédente si elle ne s'est jamais terminée. Nul si même cela
        // échoue.
        public static ErrorReporting Start(string directory, string pluginId)
        {
            try
            {
                var version = typeof(ErrorReporting).Assembly.GetName().Version.ToString();
                var session = SessionStart();
                var scrubber = new ReportScrubber(UserProfile(), KnownNames());
                var queue = new ReportQueue(Path.Combine(directory, "Reports"));
                var reporter = new ErrorReporter(Texts.DisplayName, version, session, queue, scrubber, () => DateTime.UtcNow);
                var reporting = new ErrorReporting(directory, reporter);

                var currentLog = CurrentGameLog();
                var crash = CrashWatch.Start(directory, MyFileSystem.UserDataPath, currentLog, session,
                    Texts.DisplayName, version, pluginId, scrubber);
                if (crash != null)
                {
                    reporter.Enqueue(crash);
                    MyLog.Default.WriteLine("[" + pluginId + "] la session de jeu précédente ne s'est pas terminée proprement : rapport de plantage mis en file");
                }
                return reporting;
            }
            catch (Exception e)
            {
                try { MyLog.Default.WriteLine("[" + pluginId + "] rapports d'erreurs indisponibles : " + e.Message); }
                catch (Exception) { }
                return null;
            }
        }

        // Fil principal, à chaque mise à jour.
        public void Update()
        {
            if (++m_counter < TickInterval)
                return;
            m_counter = 0;

            try
            {
                LearnPlayerName();
                m_reporter.Tick(OnOurServer(), Send);
            }
            catch (Exception)
            {
            }
        }

        // La sortie propre du jeu : les rapports attendent sur le disque, la
        // marque disparaît.
        public void End()
        {
            try { m_reporter.Persist(); }
            catch (Exception) { }
            CrashWatch.End(m_directory);
        }

        private static bool Send(byte[] report)
        {
            try
            {
                var multiplayer = MyAPIGateway.Multiplayer;
                if (multiplayer == null)
                    return false;
                multiplayer.SendMessageToServer(ReportChannel.GameChannel, report, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool OnOurServer()
        {
            var session = MyAPIGateway.Session;
            var multiplayer = MyAPIGateway.Multiplayer;
            if (session == null || multiplayer == null)
                return false;

            var server = DedicatedServerOf(MyMultiplayer.Static);
            return ReportChannel.IsOurServer(
                multiplayer.MultiplayerActive,
                multiplayer.IsServer,
                server != null,
                server != null ? server.Name : null,
                session.Name);
        }

        // Le client d'un serveur dédié garde l'élément de serveur qu'il a rejoint
        // dans une classe interne : trouvé par son type, jamais par un nom qui
        // pourrait changer (comme le fait Sir Holomap).
        private static MyGameServerItem DedicatedServerOf(object game)
        {
            if (game == null)
                return null;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var property in game.GetType().GetProperties(flags))
                {
                    if (property.PropertyType == typeof(MyGameServerItem) && property.GetIndexParameters().Length == 0)
                        return property.GetValue(game, null) as MyGameServerItem;
                }
                foreach (var field in game.GetType().GetFields(flags))
                {
                    if (field.FieldType == typeof(MyGameServerItem))
                        return field.GetValue(game) as MyGameServerItem;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        // Le début du processus du jeu, en UTC : la session des rapports.
        private static DateTime SessionStart()
        {
            try
            {
                return Process.GetCurrentProcess().StartTime.ToUniversalTime();
            }
            catch (Exception)
            {
                return DateTime.UtcNow;
            }
        }

        private static string CurrentGameLog()
        {
            try
            {
                return MyLog.Default.GetFilePath();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string UserProfile()
        {
            try
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Ce qui identifie le joueur sur cette machine : jamais dans un rapport.
        private static IEnumerable<string> KnownNames()
        {
            var names = new List<string>();
            Add(names, () => Environment.UserName);
            Add(names, () => MyGameService.UserName);
            Add(names, () => MyGameService.OnlineName);
            Add(names, () => MyGameService.UserId == 0 ? null : MyGameService.UserId.ToString());
            Add(names, () => MyGameService.OnlineUserId == 0 ? null : MyGameService.OnlineUserId.ToString());
            return names;
        }

        private void LearnPlayerName()
        {
            if (m_playerNameKnown)
                return;
            var session = MyAPIGateway.Session;
            var player = session == null ? null : session.Player;
            if (player == null)
                return;
            m_reporter.Scrubber.AddName(player.DisplayName);
            if (player.SteamUserId != 0)
                m_reporter.Scrubber.AddName(player.SteamUserId.ToString());
            m_playerNameKnown = true;
        }

        private static void Add(List<string> names, Func<string> read)
        {
            try
            {
                var name = read();
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
            catch (Exception)
            {
            }
        }
    }
}
