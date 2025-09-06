using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ValueObjectCompareMoreTests
    {
        private sealed class VO : ValueObject
        {
            public int Key { get; }
            public VO(int k) => Key = k;
            protected override IEnumerable<object> GetEqualityComponents()
            {
                yield return Key;
            }
        }

        [Fact]
        public void CompareTo_Incompatible_Type_DoesNotThrow_And_Returns_NonZero()
        {
            var a = new VO(1);
            var other = new object();

            // Nessuna eccezione
            var result = a.CompareTo(other);

            // Deve essere != 0 (perché non confrontabili)
            result.Should().NotBe(0);
        }

        [Fact]
        public void CompareTo_Orders_By_Key()
        {
            var a = new VO(1);
            var b = new VO(2);

            a.CompareTo(b).Should().BeLessThan(0);
            b.CompareTo(a).Should().BeGreaterThan(0);
            a.CompareTo(new VO(1)).Should().Be(0);
        }
    }
}
