using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using ProgesiCore; // <-- ValueObject è in questo namespace

namespace ProgesiCore.Tests
{
    // Piccolo VO di test che espone 3 componenti: int, string (null->""), e una sequenza di int
    public sealed class DemoVO : ValueObject
    {
        public int A { get; }
        public string? B { get; }
        public IReadOnlyList<int> Items { get; }

        public DemoVO(int a, string? b, IEnumerable<int>? items = null)
        {
            A = a;
            B = b;
            Items = (items ?? Array.Empty<int>()).ToArray();
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return A;
            yield return B ?? string.Empty; // normalizza i null per stabilizzare l’uguaglianza
            foreach (var i in Items) yield return i;
        }
    }

    // Un tipo diverso con stesse componenti: serve a coprire il ramo "tipo diverso => false"
    public sealed class OtherVO : ValueObject
    {
        public int A { get; }
        public string? B { get; }
        public IReadOnlyList<int> Items { get; }

        public OtherVO(int a, string? b, IEnumerable<int>? items = null)
        {
            A = a; B = b; Items = (items ?? Array.Empty<int>()).ToArray();
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return A;
            yield return B ?? string.Empty;
            foreach (var i in Items) yield return i;
        }
    }

    public class ValueObjectCoverageTests
    {
        [Fact]
        public void Equals_Null_IsFalse()
        {
            var v = new DemoVO(1, "x", new[] { 1, 2 });
            v.Equals(null).Should().BeFalse();
        }

        [Fact]
        public void Equals_SameReference_IsTrue()
        {
            var v = new DemoVO(1, "x", new[] { 1, 2 });
            v.Equals(v).Should().BeTrue();
        }

        [Fact]
        public void Equals_DifferentType_IsFalse()
        {
            var a = new DemoVO(1, "x", new[] { 1, 2 });
            var b = new OtherVO(1, "x", new[] { 1, 2 });
            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void Equals_SameComponents_IsTrue_AndHashMatches()
        {
            var a = new DemoVO(7, null, new[] { 3, 3, 7 }); // B=null -> "" in GetEqualityComponents
            var b = new DemoVO(7, "", new[] { 3, 3, 7 });

            a.Equals(b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentSequenceLength_IsFalse()
        {
            var a = new DemoVO(1, "x", new[] { 1, 2 });
            var b = new DemoVO(1, "x", new[] { 1, 2, 3 }); // lunghezza diversa
            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void Equals_SameLength_DifferentOrder_IsFalse()
        {
            var a = new DemoVO(1, "x", new[] { 1, 2, 3 });
            var b = new DemoVO(1, "x", new[] { 3, 2, 1 }); // ordine diverso
            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void GetHashCode_Varies_With_Components()
        {
            var a = new DemoVO(1, "x", new[] { 1, 2 });
            var b = new DemoVO(2, "x", new[] { 1, 2 });
            var c = new DemoVO(1, "y", new[] { 1, 2 });
            var d = new DemoVO(1, "x", new[] { 2, 1 });

            // Non garantisco unicità, ma almeno uno differisce
            var hashes = new[] { a.GetHashCode(), b.GetHashCode(), c.GetHashCode(), d.GetHashCode() };
            hashes.Distinct().Count().Should().BeGreaterThan(1);
        }
    }
}
