using Xunit;

namespace SirCelShading.Tests
{
    public class CommandeCelTests
    {
        [Theory]
        [InlineData("/cel", ActionCel.Basculer)]
        [InlineData("  /CEL  ", ActionCel.Basculer)]
        [InlineData("/cel activer", ActionCel.Activer)]
        [InlineData("/cel oui", ActionCel.Activer)]
        [InlineData("/cel couper", ActionCel.Couper)]
        [InlineData("/cel non", ActionCel.Couper)]
        [InlineData("/cel etat", ActionCel.Etat)]
        [InlineData("/cel état", ActionCel.Etat)]
        [InlineData("/cel n'importe", ActionCel.Inconnue)]
        [InlineData("/cellule", ActionCel.Aucune)]
        [InlineData("bonjour", ActionCel.Aucune)]
        [InlineData("", ActionCel.Aucune)]
        [InlineData(null, ActionCel.Aucune)]
        public void Interprete(string texte, ActionCel attendue)
        {
            Assert.Equal(attendue, CommandeCel.Interpreter(texte));
        }
    }

    public class CohabitationTests
    {
        [Fact]
        public void PersonneDAutreQuandLaMethodeEstLibre()
        {
            Assert.Empty(Cohabitation.AutresProprietaires(null, "sir-cel-shading"));
        }

        [Fact]
        public void NotreProprePatchNeCompteQuePourNous()
        {
            Assert.Empty(Cohabitation.AutresProprietaires(new[] { "sir-cel-shading" }, "sir-cel-shading"));
        }

        [Fact]
        public void UnAutreProprietaireEstSignaleUneFois()
        {
            var autres = Cohabitation.AutresProprietaires(
                new[] { "sir-cel-shading", "autre.greffon", "autre.greffon", "", null }, "sir-cel-shading");

            Assert.Equal(new[] { "autre.greffon" }, autres);
        }
    }
}
