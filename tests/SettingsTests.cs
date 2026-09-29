using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SirCelShading.Tests
{
    public class SettingsTests : IDisposable
    {
        private readonly string m_directory = Path.Combine(Path.GetTempPath(), "sir-cel-shading-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(m_directory))
                Directory.Delete(m_directory, true);
        }

        private string Write(string fileName, string content)
        {
            Directory.CreateDirectory(m_directory);
            var path = Path.Combine(m_directory, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void OnFromInstallation()
        {
            var s = new Settings();
            Assert.True(s.Enabled);
            Assert.Equal(AnimatedFilmSettings.ShadeTonesDefault, s.AnimatedFilm.ShadeTones);
            Assert.Equal(AnimatedFilmSettings.OutlineStrengthDefault, s.AnimatedFilm.OutlineStrength);
            Assert.Equal(AnimatedFilmSettings.RimLightDefault, s.AnimatedFilm.RimLight);
            Assert.Equal(AnimatedFilmSettings.HazeDefault, s.AnimatedFilm.Haze);
        }

        [Fact]
        public void TheSettingsHoldNoStyleChoice()
        {
            var names = typeof(Settings).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "AnimatedFilm", "Enabled" }, names);
            Assert.Null(typeof(Settings).Assembly.GetType("SirCelShading.CelStyle"));
        }

        [Fact]
        public void OutOfBoundsValuesAreBroughtBack()
        {
            var s = new Settings
            {
                AnimatedFilm = new AnimatedFilmSettings { ShadeTones = 9, OutlineStrength = 300, RimLight = -1, Haze = 101 },
            }.Normalized();

            Assert.Equal(AnimatedFilmSettings.ShadeTonesMax, s.AnimatedFilm.ShadeTones);
            Assert.Equal(AnimatedFilmSettings.OutlineStrengthMax, s.AnimatedFilm.OutlineStrength);
            Assert.Equal(AnimatedFilmSettings.RimLightMin, s.AnimatedFilm.RimLight);
            Assert.Equal(AnimatedFilmSettings.HazeMax, s.AnimatedFilm.Haze);
        }

        [Fact]
        public void ACopyIsIndependent()
        {
            var original = new Settings();
            var copy = original.Copy();
            copy.AnimatedFilm.Haze = 0;

            Assert.Equal(AnimatedFilmSettings.HazeDefault, original.AnimatedFilm.Haze);
        }

        [Fact]
        public void ANewPlayerGetsTheDefaults()
        {
            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.True(s.Enabled);
            Assert.Equal(AnimatedFilmSettings.HazeDefault, s.AnimatedFilm.Haze);
        }

        [Fact]
        public void APlayerOfTheFirstVersionKeepsTheSwitch()
        {
            // The file of the first version, as it wrote it.
            Write(SettingsFile.LegacyFileName,
                "<?xml version=\"1.0\"?>\n<Reglages xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n"
                + "  <Active>false</Active>\n  <Teintes>6</Teintes>\n  <Epaisseur>2</Epaisseur>\n  <Force>70</Force>\n"
                + "  <Sensibilite>4</Sensibilite>\n  <Vivacite>130</Vivacite>\n</Reglages>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.False(s.Enabled);
            Assert.Equal(AnimatedFilmSettings.ShadeTonesDefault, s.AnimatedFilm.ShadeTones);
        }

        [Theory]
        [InlineData("ComicBook")]
        [InlineData("ClearLine")]
        [InlineData("AnimatedFilm")]
        [InlineData("Watercolor")]
        [InlineData("")]
        public void AFileThatHeldAStyleLoadsWithoutErrorAndKeepsTheSwitchAndTheAnimatedFilmSettings(string stored)
        {
            // What the previous version wrote: a style, and one group per style.
            Write(SettingsFile.FileName,
                "<?xml version=\"1.0\"?>\n<Settings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n"
                + "  <Enabled>false</Enabled>\n  <Style>" + stored + "</Style>\n"
                + "  <ComicBook><Tones>6</Tones><OutlineWidth>2</OutlineWidth><OutlineStrength>70</OutlineStrength><EdgeSensitivity>4</EdgeSensitivity><Vibrance>130</Vibrance></ComicBook>\n"
                + "  <AnimatedFilm><ShadeTones>2</ShadeTones><OutlineStrength>40</OutlineStrength><RimLight>90</RimLight><Haze>20</Haze></AnimatedFilm>\n"
                + "  <ClearLine><OutlineStrength>80</OutlineStrength><EdgeSensitivity>2</EdgeSensitivity><Shadows>30</Shadows><Vibrance>150</Vibrance></ClearLine>\n"
                + "</Settings>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.False(s.Enabled);
            Assert.Equal(2, s.AnimatedFilm.ShadeTones);
            Assert.Equal(40, s.AnimatedFilm.OutlineStrength);
            Assert.Equal(90, s.AnimatedFilm.RimLight);
            Assert.Equal(20, s.AnimatedFilm.Haze);
        }

        [Fact]
        public void AFileWithoutAStyleLoadsWithoutError()
        {
            Write(SettingsFile.FileName, "<?xml version=\"1.0\"?><Settings><Enabled>true</Enabled></Settings>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.True(s.Enabled);
            Assert.Equal(AnimatedFilmSettings.HazeDefault, s.AnimatedFilm.Haze);
        }

        [Fact]
        public void TheNewFileWinsOverTheLegacyOne()
        {
            Write(SettingsFile.LegacyFileName, "<?xml version=\"1.0\"?><Reglages><Active>false</Active></Reglages>");
            SettingsFile.Save(m_directory, new Settings());

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.True(s.Enabled);
        }

        [Fact]
        public void TheSwitchAndEverySettingSurviveARoundTrip()
        {
            var saved = new Settings
            {
                Enabled = false,
                AnimatedFilm = new AnimatedFilmSettings { ShadeTones = 2, OutlineStrength = 40, RimLight = 90, Haze = 20 },
            };
            SettingsFile.Save(Path.Combine(m_directory, "sub-folder"), saved);

            string problem;
            var s = SettingsFile.Load(Path.Combine(m_directory, "sub-folder"), out problem);

            Assert.Null(problem);
            Assert.False(s.Enabled);
            Assert.Equal(2, s.AnimatedFilm.ShadeTones);
            Assert.Equal(40, s.AnimatedFilm.OutlineStrength);
            Assert.Equal(90, s.AnimatedFilm.RimLight);
            Assert.Equal(20, s.AnimatedFilm.Haze);
            Assert.False(File.Exists(SettingsFile.PathIn(Path.Combine(m_directory, "sub-folder")) + ".tmp"));
        }

        [Fact]
        public void TheSavedFileHoldsNoStyle()
        {
            SettingsFile.Save(m_directory, new Settings());

            var text = File.ReadAllText(SettingsFile.PathIn(m_directory));
            Assert.DoesNotContain("Style", text);
            Assert.DoesNotContain("ComicBook", text);
            Assert.DoesNotContain("ClearLine", text);
        }

        [Fact]
        public void AnUnreadableFileDoesNotBlockTheGame()
        {
            Write(SettingsFile.FileName, "<not xml");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.NotNull(problem);
            Assert.True(s.Enabled);
        }

        [Fact]
        public void AFileEditedByHandIsBroughtBack()
        {
            Write(SettingsFile.FileName, "<?xml version=\"1.0\"?><Settings><Enabled>true</Enabled><AnimatedFilm><Haze>500</Haze></AnimatedFilm></Settings>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.Equal(AnimatedFilmSettings.HazeMax, s.AnimatedFilm.Haze);
            Assert.Equal(AnimatedFilmSettings.RimLightDefault, s.AnimatedFilm.RimLight);
        }
    }
}
