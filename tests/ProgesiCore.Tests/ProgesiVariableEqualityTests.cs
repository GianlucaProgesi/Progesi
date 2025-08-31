using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiVariableEqualityTests
    {
        [Fact]
        public void Equality_Ignores_DependsFrom_Order()
        {
            var v1 = new ProgesiVariable(id: 1, name: "A", value: null, dependsFrom: new[] { 3, 1, 2 }, metadataId: 7);
            var v2 = new ProgesiVariable(id: 1, name: "A", value: null, dependsFrom: new[] { 2, 3, 1 }, metadataId: 7);

            _ = v1.Should().Be(v2);

            var set = new HashSet<ProgesiVariable> { v1, v2 };
            _ = set.Should().HaveCount(1);
        }

        [Fact]
        public void Inequality_When_Value_Differs()
        {
            var a = new ProgesiVariable(1, "K", 42, new[] { 1, 2, 3 }, metadataId: 7);
            var b = new ProgesiVariable(1, "K", "42", new[] { 1, 2, 3 }, metadataId: 7);

            _ = a.Should().NotBe(b);
        }
    }
}
