using System;
using System.IO;
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
        public void OnFromInstallationInClearLine()
        {
            var s = new Settings();
            Assert.True(s.Enabled);
            Assert.Equal(CelStyle.ClearLine, s.Style);
            Assert.Equal(CelStyle.ClearLine, CelStyles.Default);
        }

        [Fact]
        public void TheMenuListsTheThreeStylesInOrder()
        {
            Assert.Equal(new[] { CelStyle.ComicBook, CelStyle.AnimatedFilm, CelStyle.ClearLine }, CelStyles.MenuOrder);
            Assert.Equal(new[] { "Comic book", "Animated film", "Clear line" },
                Array.ConvertAll(CelStyles.MenuOrder, Texts.StyleName));
        }

        [Fact]
        public void OutOfBoundsValuesAreBroughtBack()
        {
            var s = new Settings
            {
                Style = (CelStyle)42,
                ComicBook = new ComicBookSettings { Tones = 99, OutlineWidth = 0, OutlineStrength = -5, EdgeSensitivity = 42, Vibrance = 10 },
                AnimatedFilm = new AnimatedFilmSettings { ShadeTones = 9, OutlineStrength = 300, RimLight = -1, Haze = 101 },
                ClearLine = new ClearLineSettings { OutlineStrength = -3, EdgeSensitivity = 0, Shadows = 90, Vibrance = 500 },
            }.Normalized();

            Assert.Equal(CelStyles.Default, s.Style);
            Assert.Equal(ComicBookSettings.TonesMax, s.ComicBook.Tones);
            Assert.Equal(ComicBookSettings.OutlineWidthMin, s.ComicBook.OutlineWidth);
            Assert.Equal(ComicBookSettings.OutlineStrengthMin, s.ComicBook.OutlineStrength);
            Assert.Equal(ComicBookSettings.EdgeSensitivityMax, s.ComicBook.EdgeSensitivity);
            Assert.Equal(ComicBookSettings.VibranceMin, s.ComicBook.Vibrance);
            Assert.Equal(AnimatedFilmSettings.ShadeTonesMax, s.AnimatedFilm.ShadeTones);
            Assert.Equal(AnimatedFilmSettings.OutlineStrengthMax, s.AnimatedFilm.OutlineStrength);
            Assert.Equal(AnimatedFilmSettings.RimLightMin, s.AnimatedFilm.RimLight);
            Assert.Equal(AnimatedFilmSettings.HazeMax, s.AnimatedFilm.Haze);
            Assert.Equal(ClearLineSettings.OutlineStrengthMin, s.ClearLine.OutlineStrength);
            Assert.Equal(ClearLineSettings.EdgeSensitivityMin, s.ClearLine.EdgeSensitivity);
            Assert.Equal(ClearLineSettings.ShadowsMax, s.ClearLine.Shadows);
            Assert.Equal(ClearLineSettings.VibranceMax, s.ClearLine.Vibrance);
        }

        [Fact]
        public void ACopyIsIndependent()
        {
            var original = new Settings();
            var copy = original.Copy();
            copy.AnimatedFilm.Haze = 0;
            copy.ComicBook.Tones = 7;

            Assert.Equal(AnimatedFilmSettings.HazeDefault, original.AnimatedFilm.Haze);
            Assert.Equal(ComicBookSettings.TonesDefault, original.ComicBook.Tones);
        }

        [Fact]
        public void ANewPlayerGetsTheDefaults()
        {
            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.True(s.Enabled);
            Assert.Equal(CelStyle.ClearLine, s.Style);
            Assert.Equal(ComicBookSettings.TonesDefault, s.ComicBook.Tones);
        }

        [Fact]
        public void AnExistingPlayerWhoNeverPickedAStyleGetsClearLineAndKeepsTheComicBookSettings()
        {
            // The file of the version before the choice of style, as it wrote it.
            Write(SettingsFile.LegacyFileName,
                "<?xml version=\"1.0\"?>\n<Reglages xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n"
                + "  <Active>true</Active>\n  <Teintes>6</Teintes>\n  <Epaisseur>2</Epaisseur>\n  <Force>70</Force>\n"
                + "  <Sensibilite>4</Sensibilite>\n  <Vivacite>130</Vivacite>\n</Reglages>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.True(s.Enabled);
            Assert.Equal(CelStyle.ClearLine, s.Style);
            Assert.Equal(6, s.ComicBook.Tones);
            Assert.Equal(2, s.ComicBook.OutlineWidth);
            Assert.Equal(70, s.ComicBook.OutlineStrength);
            Assert.Equal(4, s.ComicBook.EdgeSensitivity);
            Assert.Equal(130, s.ComicBook.Vibrance);
        }

        [Fact]
        public void TheNewFileWinsOverTheLegacyOne()
        {
            Write(SettingsFile.LegacyFileName, "<?xml version=\"1.0\"?><Reglages><Active>false</Active><Teintes>6</Teintes></Reglages>");
            SettingsFile.Save(m_directory, new Settings { Style = CelStyle.AnimatedFilm });

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.True(s.Enabled);
            Assert.Equal(CelStyle.AnimatedFilm, s.Style);
            Assert.Equal(ComicBookSettings.TonesDefault, s.ComicBook.Tones);
        }

        [Fact]
        public void TheChosenStyleAndEverySettingSurviveARoundTrip()
        {
            var saved = new Settings
            {
                Enabled = false,
                Style = CelStyle.AnimatedFilm,
                ComicBook = new ComicBookSettings { Tones = 6, OutlineWidth = 2, OutlineStrength = 70, EdgeSensitivity = 4, Vibrance = 130 },
                AnimatedFilm = new AnimatedFilmSettings { ShadeTones = 2, OutlineStrength = 40, RimLight = 90, Haze = 20 },
                ClearLine = new ClearLineSettings { OutlineStrength = 80, EdgeSensitivity = 2, Shadows = 30, Vibrance = 150 },
            };
            SettingsFile.Save(Path.Combine(m_directory, "sub-folder"), saved);

            string problem;
            var s = SettingsFile.Load(Path.Combine(m_directory, "sub-folder"), out problem);

            Assert.Null(problem);
            Assert.False(s.Enabled);
            Assert.Equal(CelStyle.AnimatedFilm, s.Style);
            Assert.Equal(6, s.ComicBook.Tones);
            Assert.Equal(2, s.ComicBook.OutlineWidth);
            Assert.Equal(70, s.ComicBook.OutlineStrength);
            Assert.Equal(4, s.ComicBook.EdgeSensitivity);
            Assert.Equal(130, s.ComicBook.Vibrance);
            Assert.Equal(2, s.AnimatedFilm.ShadeTones);
            Assert.Equal(40, s.AnimatedFilm.OutlineStrength);
            Assert.Equal(90, s.AnimatedFilm.RimLight);
            Assert.Equal(20, s.AnimatedFilm.Haze);
            Assert.Equal(80, s.ClearLine.OutlineStrength);
            Assert.Equal(2, s.ClearLine.EdgeSensitivity);
            Assert.Equal(30, s.ClearLine.Shadows);
            Assert.Equal(150, s.ClearLine.Vibrance);
            Assert.False(File.Exists(SettingsFile.PathIn(Path.Combine(m_directory, "sub-folder")) + ".tmp"));
        }

        [Theory]
        [InlineData("ComicBook", CelStyle.ComicBook)]
        [InlineData("AnimatedFilm", CelStyle.AnimatedFilm)]
        [InlineData("ClearLine", CelStyle.ClearLine)]
        [InlineData("animatedfilm", CelStyle.AnimatedFilm)]
        [InlineData("Watercolor", CelStyle.ClearLine)]
        [InlineData("", CelStyle.ClearLine)]
        public void TheStyleIsStoredByName(string stored, CelStyle expected)
        {
            Write(SettingsFile.FileName, "<?xml version=\"1.0\"?><Settings><Enabled>true</Enabled><Style>" + stored + "</Style></Settings>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.Equal(expected, s.Style);
        }

        [Fact]
        public void AFileWithoutAStyleGivesClearLine()
        {
            Write(SettingsFile.FileName, "<?xml version=\"1.0\"?><Settings><Enabled>true</Enabled></Settings>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.Equal(CelStyle.ClearLine, s.Style);
            Assert.Equal(ClearLineSettings.ShadowsDefault, s.ClearLine.Shadows);
        }

        [Fact]
        public void AnUnreadableFileDoesNotBlockTheGame()
        {
            Write(SettingsFile.FileName, "<not xml");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.NotNull(problem);
            Assert.True(s.Enabled);
            Assert.Equal(CelStyle.ClearLine, s.Style);
        }

        [Fact]
        public void AFileEditedByHandIsBroughtBack()
        {
            Write(SettingsFile.FileName, "<?xml version=\"1.0\"?><Settings><Enabled>true</Enabled><ComicBook><Tones>500</Tones></ComicBook></Settings>");

            string problem;
            var s = SettingsFile.Load(m_directory, out problem);

            Assert.Null(problem);
            Assert.Equal(ComicBookSettings.TonesMax, s.ComicBook.Tones);
            Assert.Equal(ComicBookSettings.VibranceDefault, s.ComicBook.Vibrance);
        }
    }
}
