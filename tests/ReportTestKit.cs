using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SirCelShading.Tests
{
    // Ce que les tests de rapports partagent : un dossier temporaire, une
    // horloge que le test fait avancer, le journal de jeu d'une session plantée,
    // et la relecture des rapports.
    internal sealed class ReportTestKit : IDisposable
    {
        public const string Plugin = "Sir Cel Shading";
        public const string Version = "1.2.0.0";
        public const string PluginTag = "sir-cel-shading";

        // Le joueur du journal de jeu de Data : jamais dans un rapport.
        public const string SteamId = "76561198034125587";
        public const string PlayerName = "Kestrel_Fr";
        public const string GridName = "Kestrel Hauler Mk2";
        public const string ServerAddress = "203.0.113.42";
        public const string ServerName = "Sirius Immersion PTU";
        public const string WindowsUser = "JeanDupont";
        public const string UserProfile = @"C:\Users\JeanDupont";

        public static readonly DateTime SessionStart = new DateTime(2026, 10, 27, 19, 31, 12, 734, DateTimeKind.Utc);

        public readonly string Root = Path.Combine(Path.GetTempPath(), "sir-cel-shading-reports-" + Guid.NewGuid().ToString("N"));
        public DateTime Now = new DateTime(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc);

        public string PluginDirectory
        {
            get { return Path.Combine(Root, "Storage", PluginTag); }
        }

        public string ReportsDirectory
        {
            get { return Path.Combine(PluginDirectory, "Reports"); }
        }

        public static string GameLogPath
        {
            get { return Path.Combine(AppContext.BaseDirectory, "Data", "SpaceEngineers_20261027_203112734.log"); }
        }

        public static ReportScrubber Scrubber()
        {
            // Ce que le greffon apprend sur la machine du joueur.
            return new ReportScrubber(UserProfile, new[] { WindowsUser, PlayerName, SteamId });
        }

        public ReportQueue Queue()
        {
            return new ReportQueue(ReportsDirectory);
        }

        public ErrorReporter Reporter(DateTime session)
        {
            return new ErrorReporter(Plugin, Version, session, Queue(), Scrubber(), () => Now);
        }

        public List<JsonElement> QueuedReports()
        {
            var queue = Queue();
            return queue.Names().Select(name => Parse(queue.Read(name))).ToList();
        }

        public static JsonElement Parse(byte[] bytes)
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            using (var document = JsonDocument.Parse(text))
                return document.RootElement.Clone();
        }

        public static List<string> LinesOf(JsonElement report)
        {
            return report.GetProperty("lignes").EnumerateArray().Select(l => l.GetString()).ToList();
        }

        public static DateTime Utc(JsonElement report, string field)
        {
            var text = report.GetProperty(field).GetString();
            return DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        }

        // Une ligne de pile d'appels, comme .NET l'écrit : trois espaces, « at »,
        // puis l'appel.
        public static string Frame(string call)
        {
            return "   at " + call;
        }

        public static Exception Thrown(string message)
        {
            try
            {
                throw new InvalidOperationException(message);
            }
            catch (Exception e)
            {
                return e;
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, true);
            }
            catch (Exception)
            {
            }
        }
    }
}
