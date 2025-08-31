using System.Collections.Generic;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    // VO di supporto dedicato a questi test (nome diverso da altri dummy)
    public sealed class DummyVo2 : ValueObject
    {
        public int A { get; }
        public string B { get; }

        public DummyVo2(int a, string b)
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

    public class ValueObjectMoreTests
    {
        [Fact]
        public void Operators_Handle_Null_Correctly()
        {
            var x = new DummyVo2(1, "a");
            (x == null).Should().BeFalse();
            (null == x).Should().BeFalse();
            (x != null).Should().BeTrue();
            (null != x).Should().BeTrue();
        }

        [Fact]
        public void Equals_Object_With_DifferentType_IsFalse()
        {
            var x = new DummyVo2(1, "a");
            x.Equals("not-a-vo").Should().BeFalse();
        }

        [Fact]
        public void CompareTo_SameComponents_IsZero()
        {
            var x = new DummyVo2(1, "a");
            var y = new DummyVo2(1, "a");
            x.CompareTo(y).Should().Be(0);
        }

        [Fact]
        public void GetHashCode_Is_Stable()
        {
            var x = new DummyVo2(2, "b");
            var h1 = x.GetHashCode();
            var h2 = x.GetHashCode();
            h2.Should().Be(h1);
        }
    }
}
