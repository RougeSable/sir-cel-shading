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

            Assert.True(stop.Stop("technical detail", Texts.StopVariantRefused));

            Assert.True(stop.IsStopped);
            Assert.Equal(new[] { "technical detail" }, log);
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

            stop.Stop("first", "first message");
            Assert.False(stop.Stop("second", "second message"));

            Assert.True(stop.IsStopped);
            Assert.Single(log);
            Assert.Equal("first message", stop.Reason);

            var seen = new List<string>();
            stop.Deliver(true, seen.Add);
            Assert.Equal(new[] { "first message" }, seen);
        }

        [Fact]
        public void InTheMenuNotificationsWaitForAGame()
        {
            var stop = new SessionStop(null);
            stop.Stop("detail", "message");

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
            var message = Texts.Status(true, CelStyle.ClearLine, null);
            stop.Inform(message);

            Assert.False(stop.IsStopped);
            var seen = new List<string>();
            stop.Deliver(true, seen.Add);
            Assert.Equal(new[] { message }, seen);
        }

        [Fact]
        public void SteppingAsideTellsThePlayerWhateverTheStyle()
        {
            var stop = new SessionStop(null);
            stop.Stop("patched by other.plugin", string.Format(Texts.StopCoexistence, "other.plugin"));

            foreach (var style in CelStyles.MenuOrder)
            {
                var status = Texts.Status(true, style, stop.Reason);
                Assert.StartsWith(Texts.StopPrefix, status);
                Assert.Contains("other.plugin", status);
                Assert.Contains("steps aside", status);
                Assert.Contains(Texts.StyleName(style), status);
            }
        }
    }
}
