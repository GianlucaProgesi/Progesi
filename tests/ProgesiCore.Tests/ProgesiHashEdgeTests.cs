using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiHashEdgeTests
    {
        [Theory]
        [InlineData(null, "<null>")]
        [InlineData("", "")]
        [InlineData("ciao", "ciao")]
        public void CanonicalValue_Basic(object input, string expected)
        {
            ProgesiHash.CanonicalValue(input).Should().Be(expected);
        }

        [Fact]
        public void CanonicalValue_Primitives_Invariant()
        {
            ProgesiHash.CanonicalValue(true).Should().Be("true");
            ProgesiHash.CanonicalValue(123).Should().Be("123");
            ProgesiHash.CanonicalValue(1.5).Should().Be("1.5");
        }

        [Fact]
        public void Compute_Variable_OrderIndependent_And_ValueSensitive()
        {
            var a = new ProgesiVariable(10, "K", 42, new[] { 3, 1, 2 }, metadataId: 7);
            var b = new ProgesiVariable(10, "K", 42, new[] { 1, 2, 3 }, metadataId: 7);
            var c = new ProgesiVariable(10, "K", "42", new[] { 1, 2, 3 }, metadataId: 7);

            ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));
            ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));
        }
    }
}
