using System.Collections.Generic;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ValueObjectEqualityTests
    {
        private sealed class SampleVO : ProgesiCore.ValueObject
        {
            public int A { get; }
            public string B { get; }

            public SampleVO(int a, string b)
            {
                A = a;
                B = b;
            }

            // NOTA: ritorna IEnumerable<object> (non nullable) per allinearsi alla firma base
            protected override IEnumerable<object> GetEqualityComponents()
            {
                yield return A;
                yield return B;
            }
        }

        [Fact]
        public void Equals_SameValues_True()
        {
            var x = new SampleVO(1, "x");
            var y = new SampleVO(1, "x");
            Assert.True(x.Equals(y));
            Assert.True(y.Equals(x));
            Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentValues_False()
        {
            var x = new SampleVO(1, "x");
            var y = new SampleVO(2, "x");
            var z = new SampleVO(1, "y");

            Assert.False(x.Equals(y));
            Assert.False(x.Equals(z));
        }

        [Fact]
        public void Equals_Null_False()
        {
            var x = new SampleVO(1, "x");
            Assert.False(x.Equals(null));
        }

        [Fact]
        public void HashCode_Stable_ForSameValues()
        {
            var x = new SampleVO(1, "x");
            var h1 = x.GetHashCode();
            var h2 = x.GetHashCode();
            Assert.Equal(h1, h2);
        }
    }
}
