using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SirCelShading.Tests
{
    // Where reports wait and when they leave: only on our servers, and never
    // at the cost of the game.
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

            // Single player: an error of the plugin.
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            var log = new PluginLog(ReportTestKit.PluginTag, null, reporter);
            log.Error("fault on the main thread", ReportTestKit.Thrown("boom"));

            m_kit.Now = m_kit.Now.AddMinutes(5);
            Assert.Equal(0, reporter.Tick(false, send));
            Assert.Empty(sent);

            // Kept in the plugin's folder, across the end of the game.
            reporter.Persist();
            var waiting = m_kit.Queue().Names();
            Assert.Single(waiting);
            Assert.True(File.Exists(Path.Combine(m_kit.ReportsDirectory, waiting[0])));

            // Next launch, still off our servers: still waiting.
            var later = m_kit.Reporter(ReportTestKit.SessionStart.AddHours(3));
            Assert.Equal(0, later.Tick(false, send));
            Assert.Empty(sent);
            Assert.Single(m_kit.Queue().Names());

            // Joining PU or PTU: it leaves, once, and the folder is empty.
            Assert.Equal(1, later.Tick(true, send));
            var report = ReportTestKit.Parse(Assert.Single(sent));
            Assert.Equal("erreur", report.GetProperty("genre").GetString());
            Assert.StartsWith("fault on the main thread: System.InvalidOperationException: boom", report.GetProperty("message").GetString());
            Assert.Empty(m_kit.Queue().Names());
            Assert.Equal(0, later.Tick(true, send));
            Assert.Single(sent);

            // Which servers are ours: a dedicated server of the studio,
            // joined as a client. Nothing else.
            Assert.True(ReportChannel.IsOurServer(true, false, true, "Sirius Immersion PTU", "PTU"));
            Assert.True(ReportChannel.IsOurServer(true, false, true, "[FR] SIRIUS PU", null));
            Assert.False(ReportChannel.IsOurServer(false, true, false, null, "Sirius solo save"));      // single player
            Assert.False(ReportChannel.IsOurServer(true, true, false, null, "Sirius hosted"));          // the player hosts
            Assert.False(ReportChannel.IsOurServer(true, false, true, "Other Server", "Star System"));  // someone else's
            Assert.False(ReportChannel.IsOurServer(true, false, false, null, "Sirius lobby game"));     // not dedicated

            // The channel of the rule, as the game's 16-bit channels carry it.
            Assert.Equal(5465479001L, ReportChannel.Number);
            Assert.Equal((ushort)38745, ReportChannel.GameChannel);
        }

        [Fact]
        public void SendFailureIsSwallowed()
        {
            var reporter = m_kit.Reporter(ReportTestKit.SessionStart);
            reporter.Record("fault on the render thread", "   at A.B()");
            m_kit.Now = m_kit.Now.AddMinutes(1);
            reporter.Persist();
            Assert.Single(m_kit.Queue().Names());

            // The send throws: nothing reaches the game, the report stays.
            Assert.Equal(0, reporter.Tick(true, b => { throw new InvalidOperationException("not connected"); }));
            Assert.Single(m_kit.Queue().Names());

            // The send refuses: the report stays too.
            Assert.Equal(0, reporter.Tick(true, b => false));
            Assert.Single(m_kit.Queue().Names());

            // And leaves on the next good try.
            var sent = 0;
            Assert.Equal(1, reporter.Tick(true, b => { sent++; return true; }));
            Assert.Equal(1, sent);

            // Writing fails (the folder is a file): swallowed, nothing thrown.
            var blocked = Path.Combine(m_kit.Root, "blocked");
            Directory.CreateDirectory(m_kit.Root);
            File.WriteAllText(blocked, "not a folder");
            var queue = new ReportQueue(blocked);
            Assert.False(queue.Put("a.json", new byte[] { 1 }));
            Assert.Empty(queue.Names());
            Assert.Empty(queue.Flush(true, n => true, b => { throw new IOException(); }));

            var broken = new ErrorReporter(ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.SessionStart,
                queue, ReportTestKit.Scrubber(), () => m_kit.Now);
            broken.Record("fault", (string)null);
            broken.Persist();
            Assert.Equal(0, broken.Tick(true, b => true));
            Assert.False(broken.Enqueue(new ErrorReport { Kind = ErrorReport.KindCrash }));

            // A reporter whose clock throws, a null queue: still nothing thrown.
            var faulty = new ErrorReporter(ReportTestKit.Plugin, ReportTestKit.Version, ReportTestKit.SessionStart,
                null, null, () => { throw new InvalidOperationException("clock"); });
            faulty.Record("fault", (string)null);
            faulty.Persist();
            Assert.Equal(0, faulty.Tick(true, b => true));
            Assert.False(faulty.Enqueue(new ErrorReport()));

            // The game log itself failing does not reach the plugin either.
            var log = new PluginLog(ReportTestKit.PluginTag, line => { throw new IOException("log closed"); }, reporter);
            log.Error("still reported", ReportTestKit.Thrown("x"));
            Assert.Contains(reporter.Pending(), r => r.Message.StartsWith("still reported", StringComparison.Ordinal));
        }
    }
}
