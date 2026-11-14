using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace SirCelShading.Tests
{
    // Rien sur le joueur dans un rapport, vérifié sur un journal de jeu tel que
    // le jeu l'écrit.
    public class ReportScrubberTests : IDisposable
    {
        private readonly ReportTestKit m_kit = new ReportTestKit();

        public void Dispose()
        {
            m_kit.Dispose();
        }

        private static void AssertNoPlayerData(string text)
        {
            Assert.DoesNotContain(ReportTestKit.SteamId, text);
            Assert.DoesNotContain(ReportTestKit.PlayerName, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ReportTestKit.GridName, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ReportTestKit.ServerAddress, text);
            Assert.DoesNotContain(ReportTestKit.ServerName, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ReportTestKit.WindowsUser, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("steam://", text);
            Assert.DoesNotContain("109775243120897531", text); // le salon
            Assert.DoesNotContain("132451789012345678", text); // une entité du joueur
        }

        [Fact]
        public void RealGameLogContainsNoPlayerDataAfterScrub()
        {
            var raw = File.ReadAllText(ReportTestKit.GameLogPath);
            // Le journal porte bien tout cela avant le nettoyage.
            Assert.Contains(ReportTestKit.SteamId, raw);
            Assert.Contains(ReportTestKit.PlayerName, raw);
            Assert.Contains(ReportTestKit.GridName, raw);
            Assert.Contains(ReportTestKit.ServerAddress, raw);
            Assert.Contains(ReportTestKit.UserProfile, raw);

            // Ligne par ligne, tout le journal.
            var scrubber = ReportTestKit.Scrubber();
            var kept = scrubber.Lines(ReportScrubber.SplitLines(raw));
            var text = string.Join("\n", kept);
            AssertNoPlayerData(text);

            // Le dossier de l'utilisateur devient %USERPROFILE%, le reste de la ligne
            // reste.
            Assert.Contains(@"MyFileSystem.UserDataPath: %USERPROFILE%\AppData\Roaming\SpaceEngineers", text);
            Assert.Contains(@"%USERPROFILE%\AppData\Roaming\SpaceEngineers\Storage\sir-cel-shading\Shaders\CelShading.hlsl", text);

            // Ce qui aide à comprendre un plantage reste.
            Assert.Contains(kept, l => l.Contains("CRASH INFO"));
            Assert.Contains(kept, l => l.Contains("SharpDXException"));
            Assert.Contains(kept, l => l.Contains(ReportTestKit.Frame("VRageRender.MyRender11.Present()")));
            Assert.Contains(kept, l => l.Contains("Environment.OSVersion: Microsoft Windows NT 10.0.22631.0"));
            Assert.Contains(kept, l => l.Contains("Build version: 1.206.32.0"));

            // Même sans connaître les noms du joueur, les mots mêmes du jeu et les
            // formes des données suffisent.
            var blind = new ReportScrubber(null, null);
            var blindText = string.Join("\n", blind.Lines(ReportScrubber.SplitLines(raw)));
            Assert.DoesNotContain(ReportTestKit.SteamId, blindText);
            Assert.DoesNotContain(ReportTestKit.GridName, blindText);
            Assert.DoesNotContain(ReportTestKit.ServerAddress, blindText);
            Assert.DoesNotContain(ReportTestKit.ServerName, blindText);
            Assert.DoesNotContain(ReportTestKit.WindowsUser, blindText);
            Assert.Contains("%USERPROFILE%", blindText);

            // Les rapports eux-mêmes : le rapport de plantage construit depuis ce
            // journal, et un rapport d'erreur dont le message et la pile portent le
            // dossier.
            string previousLog = Path.Combine(m_kit.Root, "SpaceEngineers_20261027_203112734.log");
            Directory.CreateDirectory(m_kit.Root);
            File.WriteAllText(previousLog, raw.Replace("\n", "\r\n")); // comme sous Windows
            var crash = CrashWatch.Build(new CrashWatch.PreviousSession { StartUtc = ReportTestKit.SessionStart, GameLog = previousLog },
                CrashWatch.ReadLines(previousLog), DateTime.UtcNow,
                ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.PluginTag, scrubber);
            var crashJson = Encoding.UTF8.GetString(crash.ToUtf8());
            AssertNoPlayerData(crashJson);
            Assert.Contains("%USERPROFILE%", crashJson);
            Assert.Contains("CRASH INFO", crashJson);

            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            reporter.Record(@"écriture impossible de C:\Users\JeanDupont\AppData\Roaming\SpaceEngineers\Storage\sir-cel-shading\Shaders\CelShading.hlsl",
                "   at System.IO.File.WriteAllText(String path, String contents)\n   at SirCelShading.SirCelShadingPlugin.WriteShader(String path) in C:\\Users\\JeanDupont\\src\\Plugin.cs:line 9");
            reporter.Record("Joueur Kestrel_Fr : grille Kestrel Hauler Mk2 a perdu son shader", (string)null);
            reporter.Persist();
            var reports = m_kit.QueuedReports();
            Assert.Equal(2, reports.Count);
            foreach (var report in reports)
                AssertNoPlayerData(report.GetRawText());
            Assert.Contains(reports, r => r.GetProperty("message").GetString()
                == @"écriture impossible de %USERPROFILE%\AppData\Roaming\SpaceEngineers\Storage\sir-cel-shading\Shaders\CelShading.hlsl");
            Assert.Contains(reports, r => r.GetProperty("pile").GetString().Contains(@"%USERPROFILE%\src\Plugin.cs:line 9"));
            Assert.Contains(reports, r => r.GetProperty("message").GetString() == ErrorReporter.MessageRemoved);
        }
    }
}
