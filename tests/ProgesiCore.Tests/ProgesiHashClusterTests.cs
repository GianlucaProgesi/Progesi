using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ProgesiHashClusterTests
  {
    [Fact]
    public void Compute_Cluster_Throws_On_Null()
    {
      System.Action act = () => ProgesiHash.Compute((ProgesiVariableCluster)null!);
      act.Should().Throw<System.ArgumentNullException>();
    }

    [Fact]
    public void Compute_Cluster_OrderIndependent_For_VariableIds()
    {
      var a = ProgesiVariableCluster.Rehydrate(1, "C1", new[] { 3, 1, 2 }, "desc");
      var b = ProgesiVariableCluster.Rehydrate(1, "C1", new[] { 1, 2, 3 }, "desc");

      ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));
    }

    [Fact]
    public void Compute_Cluster_Sensitive_To_Name_And_Description()
    {
      var baseline = ProgesiVariableCluster.Rehydrate(1, "C1", new[] { 1, 2 }, "desc");
      var renamed = ProgesiVariableCluster.Rehydrate(1, "C2", new[] { 1, 2 }, "desc");
      var redesc = ProgesiVariableCluster.Rehydrate(1, "C1", new[] { 1, 2 }, "other");

      ProgesiHash.Compute(baseline).Should().NotBe(ProgesiHash.Compute(renamed));
      ProgesiHash.Compute(baseline).Should().NotBe(ProgesiHash.Compute(redesc));
    }

    [Fact]
    public void Compute_Cluster_Is_Deterministic()
    {
      var cluster = ProgesiVariableCluster.CreateNew("Stable", new[] { 5, 4 }, "note");
      var first = ProgesiHash.Compute(cluster);
      var second = ProgesiHash.Compute(cluster);

      first.Should().Be(second);
      first.Should().NotBeNullOrWhiteSpace();
    }
  }
}
