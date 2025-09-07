using System;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiHashSmokeTests
    {
        [Fact]
        public void Type_Exists()
        {
            var t = typeof(ProgesiCore.ProgesiHash);
            Assert.NotNull(t);
        }

        [Fact]
        public void HashCodes_Match_ForEqualValueObjects()
        {
            // Senza chiamare metodi specifici di ProgesiHash (API non nota),
            // verifichiamo l'effetto atteso a livello di ValueObject.
            var a1 = new SampleVO(1, "x");
            var a2 = new SampleVO(1, "x");

            Assert.True(a1.Equals(a2));
            Assert.Equal(a1.GetHashCode(), a2.GetHashCode());
        }

        // ValueObject minimale per il test
        private sealed class SampleVO : ProgesiCore.ValueObject
        {
            public int A { get; }
            public string B { get; }

            public SampleVO(int a, string b)
            {
                A = a;
                B = b;
            }

            protected override System.Collections.Generic.IEnumerable<object> GetEqualityComponents()
            {
                yield return A;
                yield return B;
            }
        }
    }
}
