using System.Collections.Generic;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    // VO minimale per esercitare la semantica del base ValueObject
    public sealed class DummyVo : ValueObject
    {
        public int A { get; }
        public string B { get; }

        public DummyVo(int a, string b)
        {
            A = a;
            B = b;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return A;
            yield return B ?? string.Empty;
        }
    }

    public class ValueObjectEqualityTests
    {
        [Fact]
        public void Equals_And_Hash_Work()
        {
            var x = new DummyVo(1, "k");
            var y = new DummyVo(1, "k");
            var z = new DummyVo(2, "k");

            x.Equals(y).Should().BeTrue();
            x.GetHashCode().Should().Be(y.GetHashCode());
            x.Equals(z).Should().BeFalse();

            var set = new HashSet<DummyVo> { x, y, z };
            set.Should().HaveCount(2); // x e y si deduplicano
        }
    }
}
