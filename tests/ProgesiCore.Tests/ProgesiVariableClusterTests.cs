using System;
using System.Linq;
using Progesi.Core.Variables;
using Xunit;

namespace Progesi.Core.Tests.Variables
{
  public class ProgesiVariableClusterTests
  {
    [Fact]
    public void CreateNew_Should_NormalizeIds()
    {
      // arrange
      var ids = new[] { 3, 1, 2, 3 };

      // act
      var cluster = ProgesiVariableCluster.CreateNew("MyCluster", ids, "test");

      // assert
      cluster.ProgesiVariableIds.SequenceEqual(new[] { 1, 2, 3 }).ShouldBeTrue();
    }

    [Fact]
    public void CreateNew_Should_Throw_If_Name_Is_Empty()
    {
      var ids = new[] { 1 };

      Assert.Throws<ArgumentException>(() =>
          ProgesiVariableCluster.CreateNew(string.Empty, ids));
    }

    [Fact]
    public void CreateNew_Should_Throw_If_No_VariableIds()
    {
      Assert.Throws<ArgumentException>(() =>
          ProgesiVariableCluster.CreateNew("MyCluster", Array.Empty<int>()));
    }

    [Fact]
    public void IsEquivalentTo_Should_Be_True_For_Same_Data_Different_Ids()
    {
      var ids = new[] { 1, 2, 3 };

      var cluster1 = ProgesiVariableCluster.Rehydrate(1, "C1", ids, "desc");
      var cluster2 = ProgesiVariableCluster.Rehydrate(99, "C1", ids, "desc");

      cluster1.IsEquivalentTo(cluster2).ShouldBeTrue();
    }

    [Fact]
    public void BuildHashtag_Should_Be_Deterministic()
    {
      var ids = new[] { 3, 1, 2 };

      var c1 = ProgesiVariableCluster.Rehydrate(10, "C", ids, "d");
      var c2 = ProgesiVariableCluster.Rehydrate(10, "C", new[] { 1, 2, 3 }, "d");

      c1.Hashtag.ShouldBe(c2.Hashtag);
    }
  }

  // Piccola estensione per evitare dipendenza da Shouldly
  internal static class AssertExtensions
  {
    public static void ShouldBeTrue(this bool value)
    {
      Assert.True(value);
    }

    public static void ShouldBe(this string actual, string expected)
    {
      Assert.Equal(expected, actual);
    }
  }
}
