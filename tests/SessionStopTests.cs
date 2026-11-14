using System.Collections.Generic;
using Xunit;

namespace SirCelShading.Tests
{
    public class SessionStopTests
    {
        [Fact]
        public void AStopWritesToTheLogAndTellsThePlayer()
        {
            var log = new List<string>();
            var stop = new SessionStop(log.Add);

            Assert.True(stop.Stop("détail technique", Texts.StopVariantRefused));

            Assert.True(stop.IsStopped);
            Assert.Equal(new[] { "détail technique" }, log);
            Assert.Equal(Texts.StopVariantRefused, stop.Reason);

            var seen = new List<string>();
            stop.Deliver(true, seen.Add);
            Assert.Equal(new[] { Texts.StopVariantRefused }, seen);
        }

        [Fact]
        public void AStopLastsForTheSession()
        {
            var log = new List<string>();
            var stop = new SessionStop(log.Add);

            stop.Stop("premier", "premier message");
            Assert.False(stop.Stop("second", "second message"));

            Assert.True(stop.IsStopped);
            Assert.Single(log);
            Assert.Equal("premier message", stop.Reason);

            var seen = new List<string>();
            stop.Deliver(true, seen.Add);
            Assert.Equal(new[] { "premier message" }, seen);
        }

        [Fact]
        public void InTheMenuNotificationsWaitForAGame()
        {
            var stop = new SessionStop(null);
            stop.Stop("détail", "message");

            var seen = new List<string>();
            Assert.Equal(0, stop.Deliver(false, seen.Add));
            Assert.Empty(seen);

            Assert.Equal(1, stop.Deliver(true, seen.Add));
            Assert.Equal(new[] { "message" }, seen);

            Assert.Equal(0, stop.Deliver(true, seen.Add));
        }

        [Fact]
        public void APlainMessageStopsNothing()
        {
            var stop = new SessionStop(null);
            var message = Texts.Status(true, null);
            stop.Inform(message);

            Assert.False(stop.IsStopped);
            var seen = new List<string>();
            stop.Deliver(true, seen.Add);
            Assert.Equal(new[] { message }, seen);
        }

        [Fact]
        public void SteppingAsideTellsThePlayer()
        {
            var stop = new SessionStop(null);
            stop.Stop("modifié par autre.greffon", string.Format(Texts.StopCoexistence, "autre.greffon"));

            var status = Texts.Status(true, stop.Reason);
            Assert.StartsWith(Texts.StopPrefix, status);
            Assert.Contains("autre.greffon", status);
            Assert.Contains("s'efface", status);
        }
    }
}
