using System.Collections.Generic;
using Xunit;

namespace SirCelShading.Tests
{
    public class ArretDeSessionTests
    {
        [Fact]
        public void UnArretEcritAuJournalEtPrevientLeJoueur()
        {
            var journal = new List<string>();
            var arret = new ArretDeSession(journal.Add);

            Assert.True(arret.Arreter("détail technique", Textes.ArretVarianteRefusee));

            Assert.True(arret.EstArrete);
            Assert.Equal(new[] { "détail technique" }, journal);
            Assert.Equal(Textes.ArretVarianteRefusee, arret.Raison);

            var vus = new List<string>();
            arret.Delivrer(true, vus.Add);
            Assert.Equal(new[] { Textes.ArretVarianteRefusee }, vus);
        }

        [Fact]
        public void LArretVautPourLaSession()
        {
            var journal = new List<string>();
            var arret = new ArretDeSession(journal.Add);

            arret.Arreter("premier", "premier message");
            Assert.False(arret.Arreter("second", "second message"));

            Assert.True(arret.EstArrete);
            Assert.Single(journal);
            Assert.Equal("premier message", arret.Raison);

            var vus = new List<string>();
            arret.Delivrer(true, vus.Add);
            Assert.Equal(new[] { "premier message" }, vus);
        }

        [Fact]
        public void AuMenuLesNotificationsAttendentUnePartie()
        {
            var arret = new ArretDeSession(null);
            arret.Arreter("détail", "message");

            var vus = new List<string>();
            Assert.Equal(0, arret.Delivrer(false, vus.Add));
            Assert.Empty(vus);

            Assert.Equal(1, arret.Delivrer(true, vus.Add));
            Assert.Equal(new[] { "message" }, vus);

            Assert.Equal(0, arret.Delivrer(true, vus.Add));
        }

        [Fact]
        public void UneSimpleInformationNArreteRien()
        {
            var arret = new ArretDeSession(null);
            arret.Prevenir(Textes.Active);

            Assert.False(arret.EstArrete);
            var vus = new List<string>();
            arret.Delivrer(true, vus.Add);
            Assert.Equal(new[] { Textes.Active }, vus);
        }
    }
}
