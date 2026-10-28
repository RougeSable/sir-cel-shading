using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace SirCelShading.Tests
{
    // Nothing about the player in a report, checked on a game log as the
    // game writes it.
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
            Assert.DoesNotContain("109775243120897531", text); // the lobby
            Assert.DoesNotContain("132451789012345678", text); // an entity of the player
        }

        [Fact]
        public void RealGameLogContainsNoPlayerDataAfterScrub()
        {
            var raw = File.ReadAllText(ReportTestKit.GameLogPath);
            // The log does carry all of it before scrubbing.
            Assert.Contains(ReportTestKit.SteamId, raw);
            Assert.Contains(ReportTestKit.PlayerName, raw);
            Assert.Contains(ReportTestKit.GridName, raw);
            Assert.Contains(ReportTestKit.ServerAddress, raw);
            Assert.Contains(ReportTestKit.UserProfile, raw);

            // Line by line, the whole log.
            var scrubber = ReportTestKit.Scrubber();
            var kept = scrubber.Lines(ReportScrubber.SplitLines(raw));
            var text = string.Join("\n", kept);
            AssertNoPlayerData(text);

            // The user folder becomes %USERPROFILE%, the rest of the line stays.
            Assert.Contains(@"MyFileSystem.UserDataPath: %USERPROFILE%\AppData\Roaming\SpaceEngineers", text);
            Assert.Contains(@"%USERPROFILE%\AppData\Roaming\SpaceEngineers\Storage\sir-cel-shading\Shaders\CelShading.hlsl", text);

            // What helps understand a crash stays.
            Assert.Contains(kept, l => l.Contains("CRASH INFO"));
            Assert.Contains(kept, l => l.Contains("SharpDXException"));
            Assert.Contains(kept, l => l.Contains("at VRageRender.MyRender11.Present()"));
            Assert.Contains(kept, l => l.Contains("Environment.OSVersion: Microsoft Windows NT 10.0.22631.0"));
            Assert.Contains(kept, l => l.Contains("Build version: 1.206.32.0"));

            // Even without knowing the player's names, the game's own wording
            // and the shapes of the data are enough.
            var blind = new ReportScrubber(null, null);
            var blindText = string.Join("\n", blind.Lines(ReportScrubber.SplitLines(raw)));
            Assert.DoesNotContain(ReportTestKit.SteamId, blindText);
            Assert.DoesNotContain(ReportTestKit.GridName, blindText);
            Assert.DoesNotContain(ReportTestKit.ServerAddress, blindText);
            Assert.DoesNotContain(ReportTestKit.ServerName, blindText);
            Assert.DoesNotContain(ReportTestKit.WindowsUser, blindText);
            Assert.Contains("%USERPROFILE%", blindText);

            // The reports themselves: the crash report built from that log,
            // and an error report whose message and stack carry the folder.
            string previousLog = Path.Combine(m_kit.Root, "SpaceEngineers_20261027_203112734.log");
            Directory.CreateDirectory(m_kit.Root);
            File.WriteAllText(previousLog, raw.Replace("\n", "\r\n")); // as on Windows
            var crash = CrashWatch.Build(new CrashWatch.PreviousSession { StartUtc = ReportTestKit.SessionStart, GameLog = previousLog },
                CrashWatch.ReadLines(previousLog), DateTime.UtcNow,
                ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.PluginTag, scrubber);
            var crashJson = Encoding.UTF8.GetString(crash.ToUtf8());
            AssertNoPlayerData(crashJson);
            Assert.Contains("%USERPROFILE%", crashJson);
            Assert.Contains("CRASH INFO", crashJson);

            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            reporter.Record(@"could not write C:\Users\JeanDupont\AppData\Roaming\SpaceEngineers\Storage\sir-cel-shading\Shaders\CelShading.hlsl",
                "   at System.IO.File.WriteAllText(String path, String contents)\n   at SirCelShading.SirCelShadingPlugin.WriteShader(String path) in C:\\Users\\JeanDupont\\src\\Plugin.cs:line 9");
            reporter.Record("Player Kestrel_Fr: grid Kestrel Hauler Mk2 lost its shader", (string)null);
            reporter.Persist();
            var reports = m_kit.QueuedReports();
            Assert.Equal(2, reports.Count);
            foreach (var report in reports)
                AssertNoPlayerData(report.GetRawText());
            Assert.Contains(reports, r => r.GetProperty("message").GetString()
                == @"could not write %USERPROFILE%\AppData\Roaming\SpaceEngineers\Storage\sir-cel-shading\Shaders\CelShading.hlsl");
            Assert.Contains(reports, r => r.GetProperty("pile").GetString().Contains(@"%USERPROFILE%\src\Plugin.cs:line 9"));
            Assert.Contains(reports, r => r.GetProperty("message").GetString() == ErrorReporter.MessageRemoved);
        }
    }
}
