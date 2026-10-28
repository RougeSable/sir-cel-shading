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
    // The plugin's only exchange with a server: its error and crash reports,
    // sent on the studio's report channel while the player is on PU or PTU.
    // Elsewhere they wait in Storage\sir-cel-shading\Reports.
    //
    // Nothing here may break the game: every fault is swallowed.
    internal sealed class ErrorReporting
    {
        // Updates between two rounds (60 per second): about once a second.
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

        // At the start of the game: the reporter, then the crash report of the
        // previous session if it never ended. Null if even that fails.
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
                    MyLog.Default.WriteLine("[" + pluginId + "] the previous game session did not end cleanly: crash report queued");
                }
                return reporting;
            }
            catch (Exception e)
            {
                try { MyLog.Default.WriteLine("[" + pluginId + "] error reports unavailable: " + e.Message); }
                catch (Exception) { }
                return null;
            }
        }

        // Main thread, every update.
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

        // The clean exit of the game: the reports wait on disk, the marker
        // goes.
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

        // The client of a dedicated server keeps the server item it joined in
        // an internal class: found by its type, never by a name that could
        // change (as Sir Holomap does).
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

        // The start of the game process, in UTC: the session of the reports.
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

        // What identifies the player on this machine: never in a report.
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
