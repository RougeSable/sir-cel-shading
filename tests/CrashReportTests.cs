using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SirCelShading.Tests
{
    // The "plantage" report: a marker left by a session that never ended,
    // and the game log of that session read again at the next start.
    public class CrashReportTests : IDisposable
    {
        private readonly ReportTestKit m_kit = new ReportTestKit();

        public void Dispose()
        {
            m_kit.Dispose();
        }

        // The game's folder (%AppData%\SpaceEngineers) with the log of the
        // crashed session and the log the game just opened.
        private string GameFolder(out string previousLog, out string currentLog)
        {
            var folder = Path.Combine(m_kit.Root, "SpaceEngineers");
            Directory.CreateDirectory(folder);
            previousLog = Path.Combine(folder, "SpaceEngineers_20261027_203112734.log");
            File.Copy(ReportTestKit.GameLogPath, previousLog);
            currentLog = Path.Combine(folder, "SpaceEngineers_20261027_211540118.log");
            File.WriteAllText(currentLog, "2026-10-27 21:15:40.118 - Thread:   1 ->  Log Started\n");
            return folder;
        }

        [Fact]
        public void LeftoverMarkerBuildsCrashReportFromPreviousGameLog()
        {
            string previousLog, currentLog;
            var folder = GameFolder(out previousLog, out currentLog);

            // The crashed session put its marker and never removed it.
            Assert.Null(CrashWatch.Begin(m_kit.PluginDirectory, ReportTestKit.SessionStart, previousLog));
            Assert.True(CrashWatch.HasMarker(m_kit.PluginDirectory));

            // Next start.
            var start = new DateTime(2026, 10, 27, 20, 15, 40, 118, DateTimeKind.Utc);
            var crash = CrashWatch.Start(m_kit.PluginDirectory, folder, currentLog, start,
                ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.PluginTag, ReportTestKit.Scrubber());

            Assert.NotNull(crash);
            Assert.True(CrashWatch.HasMarker(m_kit.PluginDirectory)); // this session's marker

            var reporter = m_kit.Reporter(start);
            Assert.True(reporter.Enqueue(crash));
            var report = Assert.Single(m_kit.QueuedReports());

            Assert.Equal("plantage", report.GetProperty("genre").GetString());
            Assert.Equal("Sir Cel Shading", report.GetProperty("greffon").GetString());
            Assert.Equal(1, report.GetProperty("nombre").GetInt32());

            // The session that crashed, and the time of the crash in UTC (the
            // log is in local time, one hour ahead).
            Assert.Equal(ReportTestKit.SessionStart, ReportTestKit.Utc(report, "session"));
            Assert.Equal(new DateTime(2026, 10, 27, 19, 31, 59, 823, DateTimeKind.Utc), ReportTestKit.Utc(report, "premiere"));
            Assert.Equal(ReportTestKit.Utc(report, "premiere"), ReportTestKit.Utc(report, "derniere"));

            // The exception that brought the game down, and its stack.
            var message = report.GetProperty("message").GetString();
            Assert.StartsWith("Exception occurred: SharpDX.SharpDXException: HRESULT: [0x887A0005]", message);
            Assert.Contains("DXGI_ERROR_DEVICE_REMOVED", message);
            var stack = report.GetProperty("pile").GetString().Split('\n');
            Assert.Equal("   at SharpDX.Result.CheckError()", stack[0]);
            Assert.Contains("   at VRageRender.MyRender11.Present()", stack);
            Assert.Contains(stack, l => l.Contains(@"%USERPROFILE%\source\repos\VRage\MyRenderThread.cs:line 412"));

            var lines = ReportTestKit.LinesOf(report);

            // The CRASH INFO block, whole.
            var info = lines.IndexOf(CrashWatch.CrashInfoHeading);
            var errors = lines.IndexOf(CrashWatch.GameErrorsHeading);
            var plugin = lines.IndexOf(CrashWatch.PluginLinesHeading);
            Assert.True(info >= 0 && errors > info && plugin > errors);
            var block = lines.Skip(info + 1).Take(errors - info - 1).ToList();
            Assert.Contains("CRASH INFO", block.First());
            Assert.Contains("OFNI HSARC", block.Last());
            Assert.Contains(block, l => l.EndsWith("IsGPU: True"));
            Assert.Contains(block, l => l.EndsWith("AppVersion: 1.206.32"));

            // The last 20 error lines of the game before the end.
            var gameErrors = lines.Skip(errors + 1).Take(plugin - errors - 1).ToList();
            Assert.Equal(CrashWatch.LastLines, gameErrors.Count);
            Assert.Contains("Exception occurred: SharpDX.SharpDXException", gameErrors.Last());
            Assert.Contains(gameErrors, l => l.Contains("helmet_11.mwm"));
            Assert.Contains(gameErrors, l => l.Contains("device lost"));
            Assert.DoesNotContain(gameErrors, l => l.Contains("heavy_armor_ng.dds")); // older than the last 20
            Assert.DoesNotContain(gameErrors, l => l.Contains("[sir-cel-shading]"));

            // The last 20 lines of the plugin.
            var pluginLines = lines.Skip(plugin + 1).ToList();
            Assert.Equal(CrashWatch.LastLines, pluginLines.Count);
            Assert.All(pluginLines, l => Assert.Contains("[sir-cel-shading] ", l));
            Assert.Contains("frame check 23:", pluginLines.Last());
            Assert.DoesNotContain(pluginLines, l => l.Contains("loaded, Animated film"));

            // Read once: the next start, after a clean exit, reports nothing.
            CrashWatch.End(m_kit.PluginDirectory);
            Assert.Null(CrashWatch.Start(m_kit.PluginDirectory, folder, currentLog, start.AddHours(1),
                ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.PluginTag, ReportTestKit.Scrubber()));
        }

        [Fact]
        public void CleanExitLeavesNoMarkerAndNoCrashReport()
        {
            string previousLog, currentLog;
            var folder = GameFolder(out previousLog, out currentLog);

            // A session that starts...
            Assert.Null(CrashWatch.Start(m_kit.PluginDirectory, folder, previousLog, ReportTestKit.SessionStart,
                ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.PluginTag, ReportTestKit.Scrubber()));
            Assert.True(CrashWatch.HasMarker(m_kit.PluginDirectory));

            // ... and ends cleanly.
            CrashWatch.End(m_kit.PluginDirectory);
            Assert.False(CrashWatch.HasMarker(m_kit.PluginDirectory));
            Assert.Empty(Directory.GetFiles(m_kit.PluginDirectory));

            // The next start finds no marker: no crash report, even though
            // the previous game log holds a CRASH INFO block.
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart.AddHours(1));
            var crash = CrashWatch.Start(m_kit.PluginDirectory, folder, currentLog, ReportTestKit.SessionStart.AddHours(1),
                ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.PluginTag, ReportTestKit.Scrubber());
            Assert.Null(crash);
            reporter.Tick(true, b => true);
            Assert.Empty(m_kit.Queue().Names());

            // Ending twice, or without a marker, is harmless.
            CrashWatch.End(m_kit.PluginDirectory);
            CrashWatch.End(m_kit.PluginDirectory);
            Assert.False(CrashWatch.HasMarker(m_kit.PluginDirectory));
        }
    }
}
