using System.Linq;
using System.Reflection;
using Xunit;

namespace SirCelShading.Tests
{
    public class CelCommandTests
    {
        [Theory]
        [InlineData("/cel", CelAction.Toggle)]
        [InlineData("  /CEL  ", CelAction.Toggle)]
        [InlineData("/cel on", CelAction.Enable)]
        [InlineData("/cel off", CelAction.Disable)]
        [InlineData("/cel status", CelAction.Status)]
        [InlineData("/cel STATUS", CelAction.Status)]
        [InlineData("/cel whatever", CelAction.Unknown)]
        [InlineData("/cellar", CelAction.None)]
        [InlineData("hello", CelAction.None)]
        [InlineData("", CelAction.None)]
        [InlineData(null, CelAction.None)]
        public void Parses(string text, CelAction expected)
        {
            Assert.Equal(expected, CelCommand.Parse(text));
        }

        [Theory]
        [InlineData("/cel comic")]
        [InlineData("/cel animated")]
        [InlineData("/cel clearline")]
        public void ThereAreNoStyleCommands(string text)
        {
            var settings = new Settings { Enabled = false };

            Assert.Equal(CelAction.Unknown, CelCommand.Parse(text));
            Assert.False(CelCommand.Apply(CelCommand.Parse(text), settings));
            Assert.False(settings.Enabled);
        }

        [Fact]
        public void StatusChangesNothing()
        {
            var settings = new Settings { Enabled = true };

            Assert.False(CelCommand.Apply(CelAction.Status, settings));

            Assert.True(settings.Enabled);
        }

        [Fact]
        public void BareCelTurnsTheEffectOffThenOn()
        {
            var settings = new Settings { Enabled = true };

            Assert.True(CelCommand.Apply(CelCommand.Parse("/cel"), settings));
            Assert.False(settings.Enabled);
            Assert.True(CelCommand.Apply(CelCommand.Parse("/cel"), settings));
            Assert.True(settings.Enabled);
        }

        [Fact]
        public void OnAndOffSetTheSwitch()
        {
            var settings = new Settings { Enabled = true };

            CelCommand.Apply(CelAction.Disable, settings);
            Assert.False(settings.Enabled);
            CelCommand.Apply(CelAction.Disable, settings);
            Assert.False(settings.Enabled);
            CelCommand.Apply(CelAction.Enable, settings);
            Assert.True(settings.Enabled);
        }

        [Fact]
        public void StatusSaysWhetherTheEffectIsOn()
        {
            Assert.Equal(Texts.On, Texts.Status(true, null));
            Assert.Equal(Texts.Off, Texts.Status(false, null));
            Assert.NotEqual(Texts.Status(true, null), Texts.Status(false, null));
            Assert.StartsWith(Texts.StopPrefix, Texts.Status(true, Texts.StopFault));
        }

        [Fact]
        public void TheHelpListsEveryCommand()
        {
            foreach (var command in new[] { "/cel on", "/cel off", "/cel status" })
                Assert.Contains(command, Texts.CommandHelp);
            Assert.DoesNotContain("comic", Texts.CommandHelp);
            Assert.DoesNotContain("clearline", Texts.CommandHelp);
        }

        [Fact]
        public void EverythingThePlayerReadsIsPlainEnglishText()
        {
            var texts = typeof(Texts).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                .ToArray();

            Assert.NotEmpty(texts);
            foreach (var text in texts)
                Assert.True(text.All(ch => ch < 128), "not plain ASCII: " + text);
        }

        [Fact]
        public void NoPlayerTextMentionsAStyleThatNoLongerExists()
        {
            var texts = typeof(Texts).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue())
                .ToArray();

            foreach (var text in texts)
            {
                Assert.DoesNotContain("comic", text, System.StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("clear line", text, System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
