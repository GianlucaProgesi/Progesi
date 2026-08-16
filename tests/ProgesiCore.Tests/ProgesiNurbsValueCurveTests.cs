using System;
using System.Linq;
using Xunit;
using ProgesiCore;

namespace ProgesiCore.Tests
{
  public class ProgesiNurbsValueCurveTests
  {
    private static ProgesiNurbsPayload LinearNurbs(double x0, double y0, double x1, double y1)
    {
      return new ProgesiNurbsPayload(
        degree: 1,
        controlPoints: new[] { (x0, y0), (x1, y1) },
        weights: new[] { 1.0, 1.0 },
        knots: new[] { 0.0, 0.0, 1.0, 1.0 });
    }

    private static ProgesiNurbsPayload ConstantNurbs(double x0, double x1, double value)
      => LinearNurbs(x0, value, x1, value);

    [Fact]
    public void NurbsSegment_Constant_Evaluates_Exactly()
    {
      var fn = new ProgesiFunction(1, "flat-nurbs", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: ConstantNurbs(0.0, 1.0, 4.25))
      });

      fn.Evaluate(0.0).ShouldBeAbout(4.25);
      fn.Evaluate(0.33).ShouldBeAbout(4.25);
      fn.Evaluate(1.0).ShouldBeAbout(4.25);
    }

    [Fact]
    public void NurbsSegment_Linear_Evaluates_Exactly()
    {
      var fn = new ProgesiFunction(2, "line-nurbs", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: LinearNurbs(0.0, 0.0, 1.0, 2.0))
      });

      fn.Evaluate(0.0).ShouldBeAbout(0.0);
      fn.Evaluate(0.5).ShouldBeAbout(1.0);
      fn.Evaluate(1.0).ShouldBeAbout(2.0);
    }

    [Fact]
    public void NurbsSegment_Quadratic_Conic_Evaluates_Exactly()
    {
      var nurbs = new ProgesiNurbsPayload(
        degree: 2,
        controlPoints: new[] { (0.0, 0.0), (0.5, 1.0), (1.0, 0.0) },
        weights: new[] { 1.0, 1.0, 1.0 },
        knots: new[] { 0.0, 0.0, 0.0, 1.0, 1.0, 1.0 });

      var fn = new ProgesiFunction(3, "parabola", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: nurbs)
      });

      fn.Evaluate(0.5).ShouldBeAbout(0.5, 8);
    }

    [Fact]
    public void NurbsSegment_Approximates_Transcendental_Within_Tolerance()
    {
      int samples = 33;
      var cps = Enumerable.Range(0, samples)
        .Select(i =>
        {
          double x = i / (double)(samples - 1);
          return (x, Math.Sin(x * Math.PI));
        })
        .ToArray();

      int degree = 5;
      var knots = BuildClampedKnots(degree, cps.Length);
      var nurbs = new ProgesiNurbsPayload(degree, cps, Enumerable.Repeat(1.0, cps.Length), knots);

      var fn = new ProgesiFunction(4, "sin-nurbs", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: nurbs)
      });

      double maxError = 0.0;
      for (int i = 1; i < samples - 1; i++)
      {
        double x = i / (double)(samples - 1);
        double expected = Math.Sin(x * Math.PI);
        var actual = fn.Evaluate(x);
        Assert.NotNull(actual);
        maxError = Math.Max(maxError, Math.Abs(actual.Value - expected));
      }

      Assert.True(maxError < 0.15, $"max NURBS sin approximation error {maxError}");
    }

    [Fact]
    public void NurbsSegment_Json_RoundTrip_Preserves_Equality()
    {
      var nurbs = LinearNurbs(0.0, 1.0, 1.0, 3.0);
      var original = new ProgesiFunction(5, "json-nurbs", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: nurbs)
      });

      var rebuilt = ProgesiFunction.FromJson(original.ToJson());

      Assert.True(original.Equals(rebuilt));
      Assert.Equal(original.ContentHash, rebuilt.ContentHash);
    }

    [Fact]
    public void ProgesiValueCurve_Alias_Wraps_Function()
    {
      var fn = new ProgesiFunction(6, "alias", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: LinearNurbs(0.0, 0.0, 1.0, 5.0))
      });

      ProgesiValueCurve curve = fn;
      Assert.Equal(fn.ContentHash, curve.ContentHash);
      var value = curve.Evaluate(0.2);
      Assert.NotNull(value);
      Assert.True(Math.Abs(value.Value - 1.0) < 1e-6);
    }

    [Fact]
    public void NurbsPayload_Rejects_NonMonotonic_ControlPoint_X()
    {
      Assert.Throws<ArgumentException>(() => new ProgesiNurbsPayload(
        1,
        new[] { (0.0, 0.0), (0.4, 1.0), (0.3, 2.0) },
        new[] { 1.0, 1.0, 1.0 },
        new[] { 0.0, 0.0, 0.5, 0.5, 1.0, 1.0 }));
    }

    [Fact]
    public void Labels_RoundTrip_Equality_And_Hash()
    {
      var axis = new ProgesiAxisVariable(1, "AX", "V", "System.Double");
      axis.SetLabel(0.25, "Start");
      axis.SetLabel(0.75, "End");

      var copy = new ProgesiAxisVariable(1, "AX", "V", "System.Double");
      copy.ReplaceLabels(axis.GetLabels().Select(kv => (kv.Key, kv.Value)));

      Assert.Equal("Start", copy.GetLabel(0.25));
      Assert.Equal("End", copy.GetLabel(0.75));
      Assert.Equal(axis.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void Discontinuity_LeftRight_Retrieval()
    {
      var axis = new ProgesiAxisVariable(1, "AX", "V", "System.Double");
      var sigLeft = new ProgesiAxisVariable.ProgesiVariableSignature(10, "V", "System.Double");
      var sigRight = new ProgesiAxisVariable.ProgesiVariableSignature(20, "V", "System.Double");

      axis.Add(sigLeft, 0.5, ProgesiAxisStationSide.Left);
      axis.Add(sigRight, 0.5, ProgesiAxisStationSide.Right);

      Assert.Single(axis.GetAt(0.5, ProgesiAxisStationSide.Left));
      Assert.Equal(10, axis.GetAt(0.5, ProgesiAxisStationSide.Left).First());
      Assert.Single(axis.GetAt(0.5, ProgesiAxisStationSide.Right));
      Assert.Equal(20, axis.GetAt(0.5, ProgesiAxisStationSide.Right).First());
      Assert.Empty(axis.GetAt(0.5, ProgesiAxisStationSide.None));
    }

    private static double[] BuildClampedKnots(int degree, int controlPointCount)
    {
      int knotCount = controlPointCount + degree + 1;
      var knots = new double[knotCount];
      for (int i = 0; i <= degree; i++)
        knots[i] = 0.0;
      for (int i = knotCount - degree - 1; i < knotCount; i++)
        knots[i] = 1.0;
      int interior = knotCount - 2 * (degree + 1);
      for (int i = 0; i < interior; i++)
        knots[degree + 1 + i] = (i + 1) / (double)(interior + 1);
      return knots;
    }
  }
}
