using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SirCelShading.Tests
{
    public class ShaderTests
    {
        [Fact]
        public void LaProfondeurEstLieeEnT31()
        {
            Assert.Equal(31, SourceDuShader.EmplacementProfondeur);
            Assert.Contains("register(t31)", SourceDuShader.Texte);
        }

        [Fact]
        public void AucunEmplacementDuPassageDuJeuNEstRedeclare()
        {
            // Le passage du jeu lie t0 à t3, u0 et s0 : notre variante n'en
            // déclare pas d'autre que t31.
            var emplacements = Regex.Matches(SourceDuShader.Texte, @"register\s*\(\s*(\w+)\s*\)")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.Equal(new[] { "t31" }, emplacements);
        }

        [Fact]
        public void LesEnTetesDuJeuSontInclusEntreChevrons()
        {
            // Entre guillemets, le compilateur du jeu les chercherait à côté de
            // notre fichier, dans le dossier du joueur, et échouerait.
            foreach (var enTete in SourceDuShader.EnTetesDuJeu)
                Assert.Contains("#include <" + enTete + ">", SourceDuShader.Texte);
            Assert.DoesNotContain("#include \"", SourceDuShader.Texte);
        }

        [Fact]
        public void LePointDEntreeEstCeluiQueLeJeuAttend()
        {
            Assert.Contains("void __compute_shader(uint3 dispatchThreadID : SV_DispatchThreadID)", SourceDuShader.Texte);
            Assert.Contains("[numthreads(NUMTHREADS_X, NUMTHREADS_Y, 1)]", SourceDuShader.Texte);
        }

        [Fact]
        public void LesTroisDrapeauxDuJeuSontHonores()
        {
            Assert.Contains("#ifndef DISABLE_TONEMAPPING", SourceDuShader.Texte);
            Assert.Contains("#ifdef FILL_ALPHA_LUMINANCE", SourceDuShader.Texte);
            Assert.Contains("#ifndef DISABLE_COLOR_FILTERS", SourceDuShader.Texte);
        }

        [Fact]
        public void LeFichierEstEcritEnFinsDeLigneUnix()
        {
            Assert.DoesNotContain("\r", SourceDuShader.Texte);
        }
    }

    public class VariantesDuShaderTests
    {
        [Theory]
        [InlineData(true, false, Variante.Normale)]
        [InlineData(true, true, Variante.LuminanceAlpha)]
        [InlineData(false, false, Variante.SansTonemapping)]
        [InlineData(false, true, Variante.SansTonemapping)]
        public void ChoisitCommeLeJeu(bool tonemapping, bool luminanceAlpha, Variante attendue)
        {
            Assert.Equal(attendue, VariantesDuShader.Choisir(tonemapping, luminanceAlpha));
        }

        [Fact]
        public void LesChampsSuiventLOrdreDesVariantes()
        {
            Assert.Equal("m_cs", VariantesDuShader.ChampsDuJeu[(int)Variante.Normale]);
            Assert.Equal("m_csAlphaLuminance", VariantesDuShader.ChampsDuJeu[(int)Variante.LuminanceAlpha]);
            Assert.Equal("m_csSkip", VariantesDuShader.ChampsDuJeu[(int)Variante.SansTonemapping]);
        }

        [Fact]
        public void ChaqueVariantePorteLesMacrosDuJeu()
        {
            var r = new Reglages();

            var normale = VariantesDuShader.Macros(Variante.Normale, r).Select(m => m.ToString()).ToArray();
            var alpha = VariantesDuShader.Macros(Variante.LuminanceAlpha, r).Select(m => m.ToString()).ToArray();
            var sans = VariantesDuShader.Macros(Variante.SansTonemapping, r).Select(m => m.ToString()).ToArray();

            Assert.Equal("NUMTHREADS=8", normale[0]);
            Assert.DoesNotContain("FILL_ALPHA_LUMINANCE", normale);
            Assert.DoesNotContain("DISABLE_TONEMAPPING", normale);
            Assert.Contains("FILL_ALPHA_LUMINANCE", alpha);
            Assert.Contains("DISABLE_TONEMAPPING", sans);
        }

        [Fact]
        public void LesNombresSontEcritsAvecUnPointQuelleQueSoitLaLangue()
        {
            var ancienne = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                var macros = VariantesDuShader.Macros(Variante.Normale, new Reglages { Force = 85, Vivacite = 115, Sensibilite = 3 })
                    .ToDictionary(m => m.Nom, m => m.Valeur);

                Assert.Equal("0.85f", macros["CEL_FORCE"]);
                Assert.Equal("1.15f", macros["CEL_SATURATION"]);
                Assert.Equal("0.75f", macros["CEL_SEUIL"]);
                Assert.Equal("1.0f", VariantesDuShader.Macros(Variante.Normale, new Reglages { Force = 100 })
                    .Single(m => m.Nom == "CEL_FORCE").Valeur);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = ancienne;
            }
        }

        [Fact]
        public void LaSensibiliteAbaisseLeSeuil()
        {
            for (var s = Reglages.SensibiliteMin; s < Reglages.SensibiliteMax; s++)
                Assert.True(VariantesDuShader.Seuil(s + 1) < VariantesDuShader.Seuil(s));
        }

        [Fact]
        public void LInterrupteurNeDemandePasDeRecompiler()
        {
            Assert.Equal(
                VariantesDuShader.Signature(new Reglages { Active = true }),
                VariantesDuShader.Signature(new Reglages { Active = false }));
            Assert.NotEqual(
                VariantesDuShader.Signature(new Reglages { Teintes = 4 }),
                VariantesDuShader.Signature(new Reglages { Teintes = 5 }));
        }
    }
}
