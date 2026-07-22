using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ProgesiVariableAssumptionTests
  {
    [Fact]
    public void Default_IsAssumption_False()
    {
      var v = new ProgesiVariable(1, "L", 10.0);
      v.IsAssumption.Should().BeFalse();
    }

    [Fact]
    public void Hash_Differs_When_IsAssumption_Changes()
    {
      var a = new ProgesiVariable(1, "K", 100, new[] { 1, 2 }, metadataId: 7, isAssumption: false);
      var b = new ProgesiVariable(1, "K", 100, new[] { 1, 2 }, metadataId: 7, isAssumption: true);

      ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(b));
    }

    [Fact]
    public void Equality_Fails_When_IsAssumption_Differs()
    {
      var a = new ProgesiVariable(1, "X", 5.0, new[] { 1, 3 }, metadataId: 10, isAssumption: false);
      var b = new ProgesiVariable(1, "X", 5.0, new[] { 1, 3 }, metadataId: 10, isAssumption: true);

      a.Should().NotBe(b);
    }
  }
}
