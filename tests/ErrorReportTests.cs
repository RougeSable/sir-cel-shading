using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SirCelShading.Tests
{
    // The "erreur" reports: their form, one per error and per session, and
    // what produces them.
    public class ErrorReportTests : IDisposable
    {
        private readonly ReportTestKit m_kit = new ReportTestKit();

        public void Dispose()
        {
            m_kit.Dispose();
        }

        [Fact]
        public void SerializesAllRequiredFieldsAsUtf8Json()
        {
            var first = new DateTime(2026, 10, 27, 20, 1, 2, 345, DateTimeKind.Utc);
            var last = first.AddMinutes(3);
            // Non-ASCII text and characters JSON must escape.
            var accented = "caf" + (char)0xE9;
            var message = "shader \"CelShading\" refused: " + accented + " " + (char)0x2192 + " \\ end";
            var report = new ErrorReport
            {
                Plugin = ReportTestKit.Plugin,
                Version = ReportTestKit.Version,
                Kind = ErrorReport.KindError,
                Message = message,
                Stack = "   at A.B()\n   at C.D()",
                First = first,
                Last = last,
                Count = 7,
                Session = ReportTestKit.SessionStart,
                Lines = new List<string> { "line one", "tab\there" },
            };

            var bytes = report.ToUtf8();

            // UTF-8 without byte order mark, strictly valid.
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            var text = new UTF8Encoding(false, true).GetString(bytes);
            Assert.Contains(accented, text);

            var json = ReportTestKit.Parse(bytes);
            var names = json.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.Equal(new[] { "greffon", "version", "genre", "message", "pile", "premiere", "derniere", "nombre", "session", "lignes" }, names);

            Assert.Equal("Sir Cel Shading", json.GetProperty("greffon").GetString());
            Assert.Equal("1.2.0.0", json.GetProperty("version").GetString());
            Assert.Equal("erreur", json.GetProperty("genre").GetString());
            Assert.Equal(message, json.GetProperty("message").GetString());
            Assert.Equal("   at A.B()\n   at C.D()", json.GetProperty("pile").GetString());
            Assert.Equal(JsonValueKind.Number, json.GetProperty("nombre").ValueKind);
            Assert.Equal(7, json.GetProperty("nombre").GetInt32());
            Assert.Equal(new[] { "line one", "tab\there" }, ReportTestKit.LinesOf(json));

            // ISO 8601 times, in UTC.
            Assert.Equal("2026-10-27T20:01:02.345Z", json.GetProperty("premiere").GetString());
            Assert.Equal("2026-10-27T20:04:02.345Z", json.GetProperty("derniere").GetString());
            Assert.Equal("2026-10-27T19:31:12.734Z", json.GetProperty("session").GetString());
            Assert.Equal(first, ReportTestKit.Utc(json, "premiere"));

            // The crash kind uses the same form.
            report.Kind = ErrorReport.KindCrash;
            Assert.Equal("plantage", ReportTestKit.Parse(report.ToUtf8()).GetProperty("genre").GetString());

            // A huge report is shortened, never refused.
            for (var i = 0; i < 2000; i++)
                report.Lines.Add(new string('x', 200));
            var big = report.ToUtf8();
            Assert.True(big.Length <= ErrorReport.MaxBytes);
            Assert.Equal("plantage", ReportTestKit.Parse(big).GetProperty("genre").GetString());
        }

        [Fact]
        public void SameErrorIsReportedOncePerSessionWithCount()
        {
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            var start = m_kit.Now;
            var stack = "   at SirCelShading.FinalColorsPass.Prefix(Object[] __args)\n   at VRageRender.MyToneMapping.Run()";

            reporter.Record("fault on the render thread", stack);
            m_kit.Now = start.AddSeconds(5);
            reporter.Record("fault on the render thread", stack);
            m_kit.Now = start.AddSeconds(12);
            reporter.Record("fault on the render thread", stack);

            // Same message, another top of stack: another error.
            reporter.Record("fault on the render thread", "   at SirCelShading.FinalColorsPass.Postfix()");

            // Off our servers: written to the queue, nothing sent.
            var sent = new List<byte[]>();
            Assert.Equal(0, reporter.Tick(false, b => { sent.Add(b); return true; }));
            var queued = m_kit.QueuedReports();
            Assert.Equal(2, queued.Count);

            var repeated = queued.Single(r => r.GetProperty("nombre").GetInt32() == 3);
            Assert.Equal("erreur", repeated.GetProperty("genre").GetString());
            Assert.Equal("fault on the render thread", repeated.GetProperty("message").GetString());
            Assert.Equal(start, ReportTestKit.Utc(repeated, "premiere"));
            Assert.Equal(start.AddSeconds(12), ReportTestKit.Utc(repeated, "derniere"));
            Assert.Equal(ReportTestKit.SessionStart, ReportTestKit.Utc(repeated, "session"));
            Assert.Single(queued, r => r.GetProperty("nombre").GetInt32() == 1);

            // On our server, once settled: both leave, once.
            m_kit.Now = start.AddSeconds(45);
            Assert.Equal(2, reporter.Tick(true, b => { sent.Add(b); return true; }));
            Assert.Equal(2, sent.Count);
            Assert.Empty(m_kit.Queue().Names());

            // The same error again in this session: counted, never sent again.
            m_kit.Now = start.AddSeconds(60);
            reporter.Record("fault on the render thread", stack);
            Assert.Equal(0, reporter.Tick(true, b => { sent.Add(b); return true; }));
            reporter.Persist();
            Assert.Equal(2, sent.Count);
            Assert.Empty(m_kit.Queue().Names());

            // The next session reports it again.
            var next = m_kit.Reporter(ReportTestKit.SessionStart.AddHours(2));
            next.Record("fault on the render thread", stack);
            next.Persist();
            Assert.Single(m_kit.Queue().Names());
        }

        [Fact]
        public void CaughtExceptionAndLoggedErrorBothProduceErrorReport()
        {
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            var gameLog = new List<string>();
            var log = new PluginLog(ReportTestKit.PluginTag, gameLog.Add, reporter);

            log.Info("loaded, Animated film, on");
            log.Error("unreadable settings file settings.xml; using the defaults");
            var caught = ReportTestKit.Thrown("settings folder is read-only");
            log.Error("settings not saved", caught);

            // Both are in the game log, as error lines of the plugin.
            Assert.Equal(3, gameLog.Count);
            Assert.StartsWith("[sir-cel-shading] ERROR: unreadable settings file", gameLog[1]);
            Assert.StartsWith("[sir-cel-shading] ERROR: settings not saved: System.InvalidOperationException: settings folder is read-only", gameLog[2]);

            // Both give an "erreur" report; the plain line only gives context.
            reporter.Persist();
            var reports = m_kit.QueuedReports();
            Assert.Equal(2, reports.Count);
            Assert.All(reports, r => Assert.Equal("erreur", r.GetProperty("genre").GetString()));

            var logged = reports.Single(r => r.GetProperty("message").GetString().StartsWith("unreadable settings file", StringComparison.Ordinal));
            Assert.Equal("", logged.GetProperty("pile").GetString());
            Assert.Contains("[sir-cel-shading] loaded, Animated film, on", ReportTestKit.LinesOf(logged));

            var exception = reports.Single(r => r.GetProperty("message").GetString().StartsWith("settings not saved", StringComparison.Ordinal));
            Assert.Equal("settings not saved: System.InvalidOperationException: settings folder is read-only",
                exception.GetProperty("message").GetString());
            Assert.Contains("ReportTestKit.Thrown", exception.GetProperty("pile").GetString());

            // A stop of the effect is an error line too (SessionStop wired as
            // in the plugin), and its exception is kept apart.
            var stop = SessionStop.Logging((detail, error) => log.Error(detail, error));
            stop.Stop("fault on the main thread", Texts.StopFault, ReportTestKit.Thrown("boom"));
            reporter.Persist();
            Assert.Contains(m_kit.QueuedReports(), r => r.GetProperty("message").GetString()
                == "fault on the main thread: System.InvalidOperationException: boom");
        }

        [Fact]
        public void ShaderCompileFailureProducesErrorReport()
        {
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            var gameLog = new List<string>();
            var log = new PluginLog(ReportTestKit.PluginTag, gameLog.Add, reporter);
            var stop = SessionStop.Logging((detail, error) => log.Error(detail, error));

            var description = "Normal (NUMTHREADS=8 CEL_SHADE_TONES=3)";
            var compiler = ReportTestKit.Thrown(@"CelShading.hlsl(212,9): error X3004: undeclared identifier 'cel_rim'");
            Assert.True(ShaderVariants.Refuse(stop, description, ShaderVariants.RefusedByCompiler, compiler));

            // Already told: one line in the game log, one notification.
            Assert.Single(gameLog);
            Assert.Contains("variant Normal (NUMTHREADS=8 CEL_SHADE_TONES=3) refused by the game's compiler", gameLog[0]);
            Assert.Contains("X3004", gameLog[0]);
            var seen = new List<string>();
            stop.Deliver(true, seen.Add);
            Assert.Equal(new[] { Texts.StopVariantRefused }, seen);
            Assert.True(stop.IsStopped);

            // And reported.
            reporter.Persist();
            var report = Assert.Single(m_kit.QueuedReports());
            Assert.Equal("erreur", report.GetProperty("genre").GetString());
            Assert.StartsWith("variant Normal (NUMTHREADS=8 CEL_SHADE_TONES=3) refused by the game's compiler: System.InvalidOperationException: ",
                report.GetProperty("message").GetString());
            Assert.Contains("X3004", report.GetProperty("message").GetString());
            Assert.Equal(1, report.GetProperty("nombre").GetInt32());

            // Refused without detail from the compiler: reported as well.
            var other = m_kit.Reporter(ReportTestKit.SessionStart.AddHours(1));
            var otherLog = new PluginLog(ReportTestKit.PluginTag, null, other);
            var otherStop = SessionStop.Logging((detail, error) => otherLog.Error(detail, error));
            ShaderVariants.Refuse(otherStop, description, ShaderVariants.RefusedWithoutDetail, null);
            var pending = Assert.Single(other.Pending());
            Assert.Equal(ErrorReport.KindError, pending.Kind);
            Assert.Contains(ShaderVariants.RefusedWithoutDetail, pending.Message);
        }
    }
}
