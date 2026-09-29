using Xunit;

namespace SirCelShading.Tests
{
    public class CoexistenceTests
    {
        [Fact]
        public void NobodyElseWhenTheMethodIsFree()
        {
            Assert.Empty(Coexistence.OtherOwners(null, "sir-cel-shading"));
        }

        [Fact]
        public void OurOwnPatchOnlyCountsForUs()
        {
            Assert.Empty(Coexistence.OtherOwners(new[] { "sir-cel-shading" }, "sir-cel-shading"));
        }

        [Fact]
        public void AnotherOwnerIsReportedOnce()
        {
            var others = Coexistence.OtherOwners(
                new[] { "sir-cel-shading", "other.plugin", "other.plugin", "", null }, "sir-cel-shading");

            Assert.Equal(new[] { "other.plugin" }, others);
        }
    }
}
