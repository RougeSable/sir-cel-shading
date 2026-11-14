using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SirCelShading.Tests
{
    // Où les rapports attendent et quand ils partent : seulement sur nos
    // serveurs, et jamais au prix du jeu.
    public class ReportQueueTests : IDisposable
    {
        private readonly ReportTestKit m_kit = new ReportTestKit();

        public void Dispose()
        {
            m_kit.Dispose();
        }

        [Fact]
        public void ReportsWaitOffServerAndFlushOnOurServer()
        {
            var sent = new List<byte[]>();
            Func<byte[], bool> send = b => { sent.Add(b); return true; };

            // Solo : une erreur du greffon.
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            var log = new PluginLog(ReportTestKit.PluginTag, null, reporter);
            log.Error("faute sur le fil principal", ReportTestKit.Thrown("boum"));

            m_kit.Now = m_kit.Now.AddMinutes(5);
            Assert.Equal(0, reporter.Tick(false, send));
            Assert.Empty(sent);

            // Gardé dans le dossier du greffon, d'une partie à l'autre.
            reporter.Persist();
            var waiting = m_kit.Queue().Names();
            Assert.Single(waiting);
            Assert.True(File.Exists(Path.Combine(m_kit.ReportsDirectory, waiting[0])));

            // Lancement suivant, toujours hors de nos serveurs : toujours en attente.
            var later = m_kit.Reporter(ReportTestKit.SessionStart.AddHours(3));
            Assert.Equal(0, later.Tick(false, send));
            Assert.Empty(sent);
            Assert.Single(m_kit.Queue().Names());

            // En rejoignant PU ou PTU : il part, une fois, et le dossier est vide.
            Assert.Equal(1, later.Tick(true, send));
            var report = ReportTestKit.Parse(Assert.Single(sent));
            Assert.Equal("erreur", report.GetProperty("genre").GetString());
            Assert.StartsWith("faute sur le fil principal: System.InvalidOperationException: boum", report.GetProperty("message").GetString());
            Assert.Empty(m_kit.Queue().Names());
            Assert.Equal(0, later.Tick(true, send));
            Assert.Single(sent);

            // Quels serveurs sont les nôtres : un serveur dédié du studio, rejoint
            // en client. Rien d'autre.
            Assert.True(ReportChannel.IsOurServer(true, false, true, "Sirius Immersion PTU", "PTU"));
            Assert.True(ReportChannel.IsOurServer(true, false, true, "[FR] SIRIUS PU", null));
            Assert.False(ReportChannel.IsOurServer(false, true, false, null, "Sirius solo save"));      // solo
            Assert.False(ReportChannel.IsOurServer(true, true, false, null, "Sirius hosted"));          // le joueur héberge
            Assert.False(ReportChannel.IsOurServer(true, false, true, "Other Server", "Star System"));  // celui d'un autre
            Assert.False(ReportChannel.IsOurServer(true, false, false, null, "Sirius lobby game"));     // pas dédié

            // Le canal de la règle, tel que les canaux sur 16 bits du jeu le portent.
            Assert.Equal(5465479001L, ReportChannel.Number);
            Assert.Equal((ushort)38745, ReportChannel.GameChannel);
        }

        [Fact]
        public void SendFailureIsSwallowed()
        {
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            reporter.Record("faute sur le fil de rendu", ReportTestKit.Frame("A.B()"));
            m_kit.Now = m_kit.Now.AddMinutes(1);
            reporter.Persist();
            Assert.Single(m_kit.Queue().Names());

            // L'envoi lève une exception : rien n'atteint le jeu, le rapport reste.
            Assert.Equal(0, reporter.Tick(true, b => { throw new InvalidOperationException("non connecté"); }));
            Assert.Single(m_kit.Queue().Names());

            // L'envoi refuse : le rapport reste aussi.
            Assert.Equal(0, reporter.Tick(true, b => false));
            Assert.Single(m_kit.Queue().Names());

            // Et part au prochain bon essai.
            var sent = 0;
            Assert.Equal(1, reporter.Tick(true, b => { sent++; return true; }));
            Assert.Equal(1, sent);

            // L'écriture échoue (le dossier est un fichier) : avalé, rien de levé.
            var blocked = Path.Combine(m_kit.Root, "blocked");
            Directory.CreateDirectory(m_kit.Root);
            File.WriteAllText(blocked, "pas un dossier");
            var queue = new ReportQueue(blocked);
            Assert.False(queue.Put("a.json", new byte[] { 1 }));
            Assert.Empty(queue.Names());
            Assert.Empty(queue.Flush(true, n => true, b => { throw new IOException(); }));

            var broken = new ErrorReporter(ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.SessionStart,
                queue, ReportTestKit.Scrubber(), () => m_kit.Now);
            broken.Record("faute", (string)null);
            broken.Persist();
            Assert.Equal(0, broken.Tick(true, b => true));
            Assert.False(broken.Enqueue(new ErrorReport { Kind = ErrorReport.KindCrash }));

            // Un rapporteur dont l'horloge lève une exception, une file nulle :
            // toujours rien de levé.
            var faulty = new ErrorReporter(ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.SessionStart,
                null, null, () => { throw new InvalidOperationException("clock"); });
            faulty.Record("faute", (string)null);
            faulty.Persist();
            Assert.Equal(0, faulty.Tick(true, b => true));
            Assert.False(faulty.Enqueue(new ErrorReport()));

            // Le journal du jeu qui échoue n'atteint pas non plus le greffon.
            var log = new PluginLog(ReportTestKit.PluginTag, line => { throw new IOException("journal fermé"); }, reporter);
            log.Error("toujours rapporté", ReportTestKit.Thrown("x"));
            Assert.Contains(reporter.Pending(), r => r.Message.StartsWith("toujours rapporté", StringComparison.Ordinal));
        }
    }
}
