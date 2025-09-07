using System;
using System.Collections.Generic;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiVariableEqualityTests
    {
        private static ProgesiCore.ProgesiVariable Make(int id, string key, object? value) =>
            new ProgesiCore.ProgesiVariable(id, key, value, null, null);

        [Fact]
        public void Equals_SameValues_True()
        {
            var v1 = Make(1, "Key1", "Value1");
            var v2 = Make(1, "Key1", "Value1");

            Assert.True(v1.Equals(v2));
            Assert.True(v2.Equals(v1));
            Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentKeyOrValue_False()
        {
            var baseVar = Make(1, "Key1", "Value1");
            var keyDiff = Make(1, "Key2", "Value1");
            var valDiff = Make(1, "Key1", "Value2");

            Assert.False(baseVar.Equals(keyDiff));
            Assert.False(baseVar.Equals(valDiff));
        }

        [Fact]
        public void Equals_Null_False()
        {
            var v1 = Make(1, "Key1", "Value1");
            Assert.False(v1.Equals(null));
        }

        [Fact]
        public void HashCode_Stable_ForSameValues()
        {
            var v1 = Make(1, "Key1", "Value1");
            var h1 = v1.GetHashCode();
            var h2 = v1.GetHashCode();

            Assert.Equal(h1, h2);
        }

        [Fact]
        public void ToString_IsNotNullOrEmpty()
        {
            var v1 = Make(1, "Key1", "Value1");
            var s = v1.ToString();
            Assert.False(string.IsNullOrWhiteSpace(s));
        }
    }
}
