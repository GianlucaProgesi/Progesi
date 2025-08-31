using System.Collections.Generic;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiHashEdgeTests
    {
        // Usare tipi nullable per evitare xUnit1012 quando si passa null
        public static IEnumerable<object?[]> CanonicalBasicData()
        {
            yield return new object?[] { null, "<null>" };
            yield return new object?[] { "", "" };
            yield return new object?[] { "ciao", "ciao" };
        }

        [Theory]
        [MemberData(nameof(CanonicalBasicData))]
        public void CanonicalValue_Basic(object? input, string expected)
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
        public void Compute_Variable_OrderIndependent_And_Sensitive_To_DependsOrName()
        {
            // Stesso id, name, value, metadata; dipendenze stesse ma in ordine diverso -> hash uguale
            var a = new ProgesiVariable(10, "K", 42, new[] { 3, 1, 2 }, metadataId: 7);
            var b = new ProgesiVariable(10, "K", 42, new[] { 1, 2, 3 }, metadataId: 7);

            ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b)); // l'ordine NON conta

            // Cambia il contenuto delle dipendenze -> hash diverso
            var c = new ProgesiVariable(10, "K", 42, new[] { 1, 2, 4 }, metadataId: 7);
            ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));

            // (in alternativa) cambiare il Name deve cambiare l'hash
            var d = new ProgesiVariable(10, "K2", 42, new[] { 3, 1, 2 }, metadataId: 7);
            ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(d));
        }
    }
}
