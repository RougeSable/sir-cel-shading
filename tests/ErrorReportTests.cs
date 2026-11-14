using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SirCelShading.Tests
{
    // Les rapports « erreur » : leur forme, un par erreur et par session, et ce
    // qui les produit.
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
            // Du texte non ASCII et des caractères que JSON doit échapper.
            var accented = "caf" + (char)0xE9;
            var message = "shader \"CelShading\" refusé : " + accented + " " + (char)0x2192 + " \\ end";
            var report = new ErrorReport
            {
                Plugin = ReportTestKit.Plugin,
                Version = ReportTestKit.Version,
                Kind = ErrorReport.KindError,
                Message = message,
                Stack = ReportTestKit.Frame("A.B()") + "\n" + ReportTestKit.Frame("C.D()"),
                First = first,
                Last = last,
                Count = 7,
                Session = ReportTestKit.SessionStart,
                Lines = new List<string> { "ligne un", "tab\tici" },
            };

            var bytes = report.ToUtf8();

            // UTF-8 sans marque d'ordre des octets, strictement valide.
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
            Assert.Equal(ReportTestKit.Frame("A.B()") + "\n" + ReportTestKit.Frame("C.D()"), json.GetProperty("pile").GetString());
            Assert.Equal(JsonValueKind.Number, json.GetProperty("nombre").ValueKind);
            Assert.Equal(7, json.GetProperty("nombre").GetInt32());
            Assert.Equal(new[] { "ligne un", "tab\tici" }, ReportTestKit.LinesOf(json));

            // Heures ISO 8601, en UTC.
            Assert.Equal("2026-10-27T20:01:02.345Z", json.GetProperty("premiere").GetString());
            Assert.Equal("2026-10-27T20:04:02.345Z", json.GetProperty("derniere").GetString());
            Assert.Equal("2026-10-27T19:31:12.734Z", json.GetProperty("session").GetString());
            Assert.Equal(first, ReportTestKit.Utc(json, "premiere"));

            // Le genre plantage a la même forme.
            report.Kind = ErrorReport.KindCrash;
            Assert.Equal("plantage", ReportTestKit.Parse(report.ToUtf8()).GetProperty("genre").GetString());

            // Un rapport énorme est raccourci, jamais refusé.
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
            var stack = ReportTestKit.Frame("SirCelShading.FinalColorsPass.Prefix(Object[] __args)")
                + "\n" + ReportTestKit.Frame("VRageRender.MyToneMapping.Run()");

            reporter.Record("faute sur le fil de rendu", stack);
            m_kit.Now = start.AddSeconds(5);
            reporter.Record("faute sur le fil de rendu", stack);
            m_kit.Now = start.AddSeconds(12);
            reporter.Record("faute sur le fil de rendu", stack);

            // Même message, autre haut de pile : une autre erreur.
            reporter.Record("faute sur le fil de rendu", ReportTestKit.Frame("SirCelShading.FinalColorsPass.Postfix()"));

            // Hors de nos serveurs : écrit dans la file, rien d'envoyé.
            var sent = new List<byte[]>();
            Assert.Equal(0, reporter.Tick(false, b => { sent.Add(b); return true; }));
            var queued = m_kit.QueuedReports();
            Assert.Equal(2, queued.Count);

            var repeated = queued.Single(r => r.GetProperty("nombre").GetInt32() == 3);
            Assert.Equal("erreur", repeated.GetProperty("genre").GetString());
            Assert.Equal("faute sur le fil de rendu", repeated.GetProperty("message").GetString());
            Assert.Equal(start, ReportTestKit.Utc(repeated, "premiere"));
            Assert.Equal(start.AddSeconds(12), ReportTestKit.Utc(repeated, "derniere"));
            Assert.Equal(ReportTestKit.SessionStart, ReportTestKit.Utc(repeated, "session"));
            Assert.Single(queued, r => r.GetProperty("nombre").GetInt32() == 1);

            // Sur notre serveur, une fois le délai écoulé : les deux partent, une fois.
            m_kit.Now = start.AddSeconds(45);
            Assert.Equal(2, reporter.Tick(true, b => { sent.Add(b); return true; }));
            Assert.Equal(2, sent.Count);
            Assert.Empty(m_kit.Queue().Names());

            // La même erreur dans cette session : comptée, jamais renvoyée.
            m_kit.Now = start.AddSeconds(60);
            reporter.Record("faute sur le fil de rendu", stack);
            Assert.Equal(0, reporter.Tick(true, b => { sent.Add(b); return true; }));
            reporter.Persist();
            Assert.Equal(2, sent.Count);
            Assert.Empty(m_kit.Queue().Names());

            // La session suivante la rapporte de nouveau.
            var next = m_kit.Reporter(ReportTestKit.SessionStart.AddHours(2));
            next.Record("faute sur le fil de rendu", stack);
            next.Persist();
            Assert.Single(m_kit.Queue().Names());
        }

        [Fact]
        public void CaughtExceptionAndLoggedErrorBothProduceErrorReport()
        {
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            var gameLog = new List<string>();
            var log = new PluginLog(ReportTestKit.PluginTag, gameLog.Add, reporter);

            log.Info("Sir Cel Shading chargé, film d'animation, activé");
            log.Error("fichier de réglages illisible settings.xml ; valeurs par défaut utilisées");
            var caught = ReportTestKit.Thrown("dossier de réglages en lecture seule");
            log.Error("réglages non enregistrés", caught);

            // Les deux sont au journal du jeu, comme lignes d'erreur du greffon.
            Assert.Equal(3, gameLog.Count);
            Assert.StartsWith("[sir-cel-shading] ERREUR : fichier de réglages illisible", gameLog[1]);
            Assert.StartsWith("[sir-cel-shading] ERREUR : réglages non enregistrés: System.InvalidOperationException: dossier de réglages en lecture seule", gameLog[2]);

            // Les deux donnent un rapport « erreur » ; la ligne simple ne donne que
            // du contexte.
            reporter.Persist();
            var reports = m_kit.QueuedReports();
            Assert.Equal(2, reports.Count);
            Assert.All(reports, r => Assert.Equal("erreur", r.GetProperty("genre").GetString()));

            var logged = reports.Single(r => r.GetProperty("message").GetString().StartsWith("fichier de réglages illisible", StringComparison.Ordinal));
            Assert.Equal("", logged.GetProperty("pile").GetString());
            Assert.Contains("[sir-cel-shading] Sir Cel Shading chargé, film d'animation, activé", ReportTestKit.LinesOf(logged));

            var exception = reports.Single(r => r.GetProperty("message").GetString().StartsWith("réglages non enregistrés", StringComparison.Ordinal));
            Assert.Equal("réglages non enregistrés: System.InvalidOperationException: dossier de réglages en lecture seule",
                exception.GetProperty("message").GetString());
            Assert.Contains("ReportTestKit.Thrown", exception.GetProperty("pile").GetString());

            // Un arrêt de l'effet est aussi une ligne d'erreur (SessionStop câblé
            // comme dans le greffon), et son exception est gardée à part.
            var stop = SessionStop.Logging((detail, error) => log.Error(detail, error));
            stop.Stop("faute sur le fil principal", Texts.StopFault, ReportTestKit.Thrown("boum"));
            reporter.Persist();
            Assert.Contains(m_kit.QueuedReports(), r => r.GetProperty("message").GetString()
                == "faute sur le fil principal: System.InvalidOperationException: boum");
        }

        [Fact]
        public void ShaderCompileFailureProducesErrorReport()
        {
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            var gameLog = new List<string>();
            var log = new PluginLog(ReportTestKit.PluginTag, gameLog.Add, reporter);
            var stop = SessionStop.Logging((detail, error) => log.Error(detail, error));

            var description = "Normal (NUMTHREADS=8 CEL_SHADE_TONES=3)";
            var compiler = ReportTestKit.Thrown(@"CelShading.hlsl(212,9): erreur X3004 : identifiant non déclaré 'cel_rim'");
            Assert.True(ShaderVariants.Refuse(stop, description, ShaderVariants.RefusedByCompiler, compiler));

            // Déjà dit : une ligne au journal du jeu, une notification.
            Assert.Single(gameLog);
            Assert.Contains("variante Normal (NUMTHREADS=8 CEL_SHADE_TONES=3) refusée par le compilateur du jeu", gameLog[0]);
            Assert.Contains("X3004", gameLog[0]);
            var seen = new List<string>();
            stop.Deliver(true, seen.Add);
            Assert.Equal(new[] { Texts.StopVariantRefused }, seen);
            Assert.True(stop.IsStopped);

            // Et rapporté.
            reporter.Persist();
            var report = Assert.Single(m_kit.QueuedReports());
            Assert.Equal("erreur", report.GetProperty("genre").GetString());
            Assert.StartsWith("variante Normal (NUMTHREADS=8 CEL_SHADE_TONES=3) refusée par le compilateur du jeu: System.InvalidOperationException: ",
                report.GetProperty("message").GetString());
            Assert.Contains("X3004", report.GetProperty("message").GetString());
            Assert.Equal(1, report.GetProperty("nombre").GetInt32());

            // Refusée sans détail par le compilateur : rapportée aussi.
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
