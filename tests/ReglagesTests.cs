using System;
using System.IO;
using Xunit;

namespace SirCelShading.Tests
{
    public class ReglagesTests : IDisposable
    {
        private readonly string m_dossier = Path.Combine(Path.GetTempPath(), "sir-cel-shading-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(m_dossier))
                Directory.Delete(m_dossier, true);
        }

        [Fact]
        public void ActifDesLInstallation()
        {
            Assert.True(new Reglages().Active);
        }

        [Fact]
        public void LesValeursHorsBornesSontRamenees()
        {
            var r = new Reglages { Teintes = 99, Epaisseur = 0, Force = -5, Sensibilite = 42, Vivacite = 10 }.Normalises();

            Assert.Equal(Reglages.TeintesMax, r.Teintes);
            Assert.Equal(Reglages.EpaisseurMin, r.Epaisseur);
            Assert.Equal(Reglages.ForceMin, r.Force);
            Assert.Equal(Reglages.SensibiliteMax, r.Sensibilite);
            Assert.Equal(Reglages.VivaciteMin, r.Vivacite);
        }

        [Fact]
        public void UnFichierAbsentDonneLesValeursParDefaut()
        {
            string probleme;
            var r = FichierReglages.Charger(Path.Combine(m_dossier, "absent.xml"), out probleme);

            Assert.Null(probleme);
            Assert.True(r.Active);
            Assert.Equal(Reglages.TeintesParDefaut, r.Teintes);
        }

        [Fact]
        public void LesReglagesSurviventAUnAllerRetour()
        {
            var chemin = Path.Combine(m_dossier, "sous-dossier", FichierReglages.NomDuFichier);
            FichierReglages.Enregistrer(chemin, new Reglages { Active = false, Teintes = 6, Epaisseur = 2, Force = 70, Sensibilite = 4, Vivacite = 130 });

            string probleme;
            var r = FichierReglages.Charger(chemin, out probleme);

            Assert.Null(probleme);
            Assert.False(r.Active);
            Assert.Equal(6, r.Teintes);
            Assert.Equal(2, r.Epaisseur);
            Assert.Equal(70, r.Force);
            Assert.Equal(4, r.Sensibilite);
            Assert.Equal(130, r.Vivacite);
            Assert.False(File.Exists(chemin + ".tmp"));
        }

        [Fact]
        public void UnFichierIllisibleNeBloquePasLeJeu()
        {
            Directory.CreateDirectory(m_dossier);
            var chemin = Path.Combine(m_dossier, FichierReglages.NomDuFichier);
            File.WriteAllText(chemin, "<pas du xml");

            string probleme;
            var r = FichierReglages.Charger(chemin, out probleme);

            Assert.NotNull(probleme);
            Assert.True(r.Active);
        }

        [Fact]
        public void UnFichierRetoucheAlaMainEstRamene()
        {
            Directory.CreateDirectory(m_dossier);
            var chemin = Path.Combine(m_dossier, FichierReglages.NomDuFichier);
            File.WriteAllText(chemin, "<?xml version=\"1.0\"?><Reglages><Active>true</Active><Teintes>500</Teintes></Reglages>");

            string probleme;
            var r = FichierReglages.Charger(chemin, out probleme);

            Assert.Null(probleme);
            Assert.Equal(Reglages.TeintesMax, r.Teintes);
        }
    }
}
