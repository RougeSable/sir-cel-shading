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
        [InlineData("/cel comic", CelAction.ComicBook)]
        [InlineData("/cel animated", CelAction.AnimatedFilm)]
        [InlineData("/cel clearline", CelAction.ClearLine)]
        [InlineData("/cel  Clearline ", CelAction.ClearLine)]
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
        [InlineData("/cel comic", CelStyle.ComicBook)]
        [InlineData("/cel animated", CelStyle.AnimatedFilm)]
        [InlineData("/cel clearline", CelStyle.ClearLine)]
        public void AStyleCommandSwitchesToThatStyleAndTurnsTheEffectOn(string text, CelStyle expected)
        {
            var settings = new Settings { Enabled = false, Style = CelStyle.ComicBook };
            if (expected == CelStyle.ComicBook)
                settings.Style = CelStyle.ClearLine;

            Assert.True(CelCommand.Apply(CelCommand.Parse(text), settings));

            Assert.Equal(expected, settings.Style);
            Assert.True(settings.Enabled);
        }

        [Fact]
        public void StatusChangesNothing()
        {
            var settings = new Settings { Enabled = true, Style = CelStyle.AnimatedFilm };

            Assert.False(CelCommand.Apply(CelAction.Status, settings));

            Assert.True(settings.Enabled);
            Assert.Equal(CelStyle.AnimatedFilm, settings.Style);
        }

        [Fact]
        public void BareCelStillTurnsTheEffectOnAndOffWithoutChangingTheStyle()
        {
            var settings = new Settings { Enabled = true, Style = CelStyle.AnimatedFilm };

            CelCommand.Apply(CelCommand.Parse("/cel"), settings);
            Assert.False(settings.Enabled);
            CelCommand.Apply(CelCommand.Parse("/cel"), settings);
            Assert.True(settings.Enabled);
            Assert.Equal(CelStyle.AnimatedFilm, settings.Style);
        }

        [Theory]
        [InlineData(CelStyle.ComicBook, "Comic book")]
        [InlineData(CelStyle.AnimatedFilm, "Animated film")]
        [InlineData(CelStyle.ClearLine, "Clear line")]
        public void StatusNamesTheStyleInUse(CelStyle style, string name)
        {
            Assert.Equal(name, Texts.StyleName(style));
            Assert.Contains(name, Texts.Status(true, style, null));
            Assert.Contains(name, Texts.Status(false, style, null));
            Assert.Contains(name, Texts.Status(true, style, Texts.StopFault));
        }

        [Fact]
        public void TheHelpListsEveryCommand()
        {
            foreach (var command in new[] { "/cel comic", "/cel animated", "/cel clearline", "/cel status", "/cel on", "/cel off" })
                Assert.Contains(command, Texts.CommandHelp);
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
    }
}
