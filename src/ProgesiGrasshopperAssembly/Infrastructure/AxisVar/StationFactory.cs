using System;
using System.Collections.Generic;
using System.Linq;
using ProgesiCore;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  public interface IStationStrategy
  {
    IReadOnlyList<double> CreateNormalizedStations(CurveParameterMapper mapper);
  }

  /// <summary>(a) Explicit real stations converted to normalized via the mapper.</summary>
  public sealed class ByStationValueStrategy : IStationStrategy
  {
    private readonly IReadOnlyList<double> _realStations;

    public ByStationValueStrategy(IEnumerable<double> realStations)
    {
      if (realStations == null) throw new ArgumentNullException(nameof(realStations));
      _realStations = realStations.ToList();
    }

    public IReadOnlyList<double> CreateNormalizedStations(CurveParameterMapper mapper)
    {
      if (mapper == null) throw new ArgumentNullException(nameof(mapper));
      return _realStations
        .Select(s => mapper.RealToNormalized(s))
        .OrderBy(x => x)
        .Distinct()
        .ToList();
    }
  }

  /// <summary>(b) N equal normalized divisions including endpoints.</summary>
  public sealed class ByEqualSegmentsStrategy : IStationStrategy
  {
    private readonly int _count;

    public ByEqualSegmentsStrategy(int count)
    {
      if (count < 2) throw new ArgumentOutOfRangeException(nameof(count), "Segment count must be >= 2.");
      _count = count;
    }

    public IReadOnlyList<double> CreateNormalizedStations(CurveParameterMapper mapper)
    {
      if (mapper == null) throw new ArgumentNullException(nameof(mapper));
      return Enumerable.Range(0, _count)
        .Select(i => i / (double)(_count - 1))
        .ToList();
    }
  }

  /// <summary>(c) Fixed real spacing along the abscissa curve.</summary>
  public sealed class BySegmentLengthStrategy : IStationStrategy
  {
    private readonly double _segmentLength;

    public BySegmentLengthStrategy(double segmentLength)
    {
      if (segmentLength <= 0.0) throw new ArgumentOutOfRangeException(nameof(segmentLength));
      _segmentLength = segmentLength;
    }

    public IReadOnlyList<double> CreateNormalizedStations(CurveParameterMapper mapper)
    {
      if (mapper == null) throw new ArgumentNullException(nameof(mapper));
      var list = new List<double> { 0.0 };
      double pos = _segmentLength;
      while (pos < mapper.TotalLength - CurveParameterMapper.LengthTolerance)
      {
        list.Add(mapper.RealToNormalized(pos));
        pos += _segmentLength;
      }
      if (list[list.Count - 1] < 1.0 - CurveParameterMapper.LengthTolerance)
        list.Add(1.0);
      return list;
    }
  }

  /// <summary>(d) Project 3D points to nearest station on the axis.</summary>
  public sealed class ByPointsStrategy : IStationStrategy
  {
    private readonly IReadOnlyList<Point3d> _points;

    public ByPointsStrategy(IEnumerable<Point3d> points)
    {
      if (points == null) throw new ArgumentNullException(nameof(points));
      _points = points.ToList();
    }

    public IReadOnlyList<double> CreateNormalizedStations(CurveParameterMapper mapper)
    {
      if (mapper == null) throw new ArgumentNullException(nameof(mapper));
      var curve = mapper.SourceCurve;
      return _points
        .Select(p =>
        {
          curve.ClosestPoint(p, out double t);
          mapper.TryParameterToNormalized(t, out double normalized);
          return normalized;
        })
        .OrderBy(x => x)
        .Distinct()
        .ToList();
    }
  }

  /// <summary>(e) Reuse normalized stations from another axis variable.</summary>
  public sealed class InheritFromStrategy : IStationStrategy
  {
    private readonly IReadOnlyList<double> _normalizedStations;

    public InheritFromStrategy(IEnumerable<double> normalizedStations)
    {
      if (normalizedStations == null) throw new ArgumentNullException(nameof(normalizedStations));
      _normalizedStations = normalizedStations.ToList();
    }

    public IReadOnlyList<double> CreateNormalizedStations(CurveParameterMapper mapper)
      => _normalizedStations.ToList();
  }

  public static class StationFactory
  {
    public static IReadOnlyList<double> Create(IStationStrategy strategy, CurveParameterMapper mapper)
    {
      if (strategy == null) throw new ArgumentNullException(nameof(strategy));
      if (mapper == null) throw new ArgumentNullException(nameof(mapper));
      return strategy.CreateNormalizedStations(mapper);
    }
  }
}
