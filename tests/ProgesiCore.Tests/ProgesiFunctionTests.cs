using System;
using System.Linq;
using Xunit;
using ProgesiCore;

namespace ProgesiCore.Tests
{
  public class ProgesiFunctionTests
  {
    [Fact]
    public void ConstantSegment_Evaluates_Value()
    {
      var fn = new ProgesiFunction(1, "flat", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: 3.5)
      });

      fn.Evaluate(0.25).ShouldBeAbout(3.5);
      fn.Evaluate(0.0).ShouldBeAbout(3.5);
      fn.Evaluate(1.0).ShouldBeAbout(3.5);
    }

    [Fact]
    public void UndefinedSegment_Returns_Null()
    {
      var fn = new ProgesiFunction(2, "gap", new[]
      {
        new ProgesiFunctionSegment(0.0, 0.4, ProgesiFunctionSegmentKind.Undefined),
        new ProgesiFunctionSegment(0.6, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: 1.0)
      });

      Assert.Null(fn.Evaluate(0.5));
      fn.Evaluate(0.7).ShouldBeAbout(1.0);
    }

    [Fact]
    public void ExpressionSegment_Evaluates_Deterministically()
    {
      var fn = new ProgesiFunction(3, "line", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Expression, expression: "2*x + 1")
      });

      fn.Evaluate(0.0).ShouldBeAbout(1.0);
      fn.Evaluate(0.5).ShouldBeAbout(2.0);
      fn.Evaluate(1.0).ShouldBeAbout(3.0);
    }

    [Fact]
    public void ExpressionSegment_Supports_Trig_And_MinMax()
    {
      var fn = new ProgesiFunction(4, "trig", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Expression, expression: "max(0, sin(x))")
      });

      fn.Evaluate(0.0).ShouldBeAbout(0.0);
      fn.Evaluate(0.5).ShouldBeAbout(Math.Max(0, Math.Sin(0.5)), 10);
    }

    [Fact]
    public void ContentHash_Is_Deterministic_For_Same_Function()
    {
      var segments = new[]
      {
        new ProgesiFunctionSegment(0.0, 0.5, ProgesiFunctionSegmentKind.Constant, constantValue: 1.0),
        new ProgesiFunctionSegment(0.5, 1.0, ProgesiFunctionSegmentKind.Expression, expression: "x")
      };

      var a = new ProgesiFunction(5, "fn", segments);
      var b = new ProgesiFunction(5, "fn", segments);

      Assert.Equal(a.ContentHash, b.ContentHash);
      Assert.Equal(a.Hashtag, b.Hashtag);
    }

    [Fact]
    public void Json_RoundTrip_Preserves_EqualState()
    {
      var original = new ProgesiFunction(6, "json-fn", new[]
      {
        new ProgesiFunctionSegment(0.0, 0.25, ProgesiFunctionSegmentKind.Constant, constantValue: 2.0),
        new ProgesiFunctionSegment(0.25, 1.0, ProgesiFunctionSegmentKind.Expression, expression: "x * x")
      });

      var json = original.ToJson();
      var rebuilt = ProgesiFunction.FromJson(json);

      Assert.True(original.Equals(rebuilt));
      Assert.Equal(original.ContentHash, rebuilt.ContentHash);
    }

    [Fact]
    public void OverlappingSegments_Are_Rejected()
    {
      Assert.Throws<ArgumentException>(() => new ProgesiFunction(7, "bad", new[]
      {
        new ProgesiFunctionSegment(0.0, 0.6, ProgesiFunctionSegmentKind.Constant, constantValue: 1.0),
        new ProgesiFunctionSegment(0.5, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: 2.0)
      }));
    }
  }

  internal static class ProgesiFunctionTestExtensions
  {
    public static void ShouldBeAbout(this double? actual, double expected, int precision = 12)
    {
      Assert.NotNull(actual);
      Assert.Equal(expected, actual.Value, precision);
    }
  }
}
