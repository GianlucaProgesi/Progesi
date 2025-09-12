using System.Collections.Generic;
using FluentAssertions;
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
      _ = (x == null).Should().BeFalse();
      _ = (null == x).Should().BeFalse();
      _ = (x != null).Should().BeTrue();
      _ = (null != x).Should().BeTrue();
    }

    [Fact]
    public void Equals_Object_With_DifferentType_IsFalse()
    {
      var x = new DummyVo2(1, "a");
      _ = x.Equals("not-a-vo").Should().BeFalse();
    }

    [Fact]
    public void CompareTo_SameComponents_IsZero()
    {
      var x = new DummyVo2(1, "a");
      var y = new DummyVo2(1, "a");
      _ = x.CompareTo(y).Should().Be(0);
    }

    [Fact]
    public void GetHashCode_Is_Stable()
    {
      var x = new DummyVo2(2, "b");
      int h1 = x.GetHashCode();
      int h2 = x.GetHashCode();
      _ = h2.Should().Be(h1);
    }
  }
}
