using System;
using System.Collections.Generic;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ClusterOutNamingTests
  {
    [Theory]
    [InlineData("Var 1", "Var_1")]
    [InlineData("A/B", "A_B")]
    [InlineData("A|B", "A_B")]
    [InlineData("  spaced   name ", "spaced_name")]
    public void SanitizeNick_Works(string input, string expected)
    {
      Assert.Equal(expected, ClusterOutNaming.SanitizeNick(input));
    }

    [Fact]
    public void MakeUnique_AppendsSuffix()
    {
      var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      var a = ClusterOutNaming.MakeUnique("Load", used);
      var b = ClusterOutNaming.MakeUnique("Load", used);
      var c = ClusterOutNaming.MakeUnique("Load", used);

      Assert.Equal("Load", a);
      Assert.Equal("Load_2", b);
      Assert.Equal("Load_3", c);
    }

    [Fact]
    public void BuildSignature_IsStableForSameInput()
    {
      var vars = new List<ProgesiVariable>
      {
        new ProgesiVariable(1, "Var-1", 10, Array.Empty<int>(), null, false),
        new ProgesiVariable(2, "Var-2", 20, Array.Empty<int>(), null, false)
      };

      var s1 = ClusterOutNaming.BuildSignature(7, vars);
      var s2 = ClusterOutNaming.BuildSignature(7, vars);

      Assert.Equal(s1, s2);
    }

    [Fact]
    public void BuildSignature_ChangesWhenNameChanges()
    {
      var varsA = new List<ProgesiVariable>
      {
        new ProgesiVariable(1, "A", 10, Array.Empty<int>(), null, false)
      };

      var varsB = new List<ProgesiVariable>
      {
        new ProgesiVariable(1, "B", 10, Array.Empty<int>(), null, false)
      };

      var s1 = ClusterOutNaming.BuildSignature(1, varsA);
      var s2 = ClusterOutNaming.BuildSignature(1, varsB);

      Assert.NotEqual(s1, s2);
    }
  }
}
