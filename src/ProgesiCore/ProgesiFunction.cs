using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Newtonsoft.Json;
using ProgesiCore.Internal;

namespace ProgesiCore
{
  public enum ProgesiFunctionSegmentKind
  {
    Constant = 0,
    Expression = 1,
    Undefined = 2
  }

  /// <summary>
  /// One segment of a piecewise function over normalized [0,1].
  /// </summary>
  public sealed class ProgesiFunctionSegment : ValueObject
  {
    public double Start { get; }
    public double End { get; }
    public ProgesiFunctionSegmentKind Kind { get; }
    public double? ConstantValue { get; }
    public string? Expression { get; }

    public ProgesiFunctionSegment(
      double start,
      double end,
      ProgesiFunctionSegmentKind kind,
      double? constantValue = null,
      string? expression = null)
    {
      ValidateInterval(start, end);
      Start = start;
      End = end;
      Kind = kind;

      switch (kind)
      {
        case ProgesiFunctionSegmentKind.Constant:
          if (!constantValue.HasValue)
            throw new ArgumentException("Constant segment requires constantValue.", nameof(constantValue));
          ConstantValue = constantValue.Value;
          Expression = null;
          break;
        case ProgesiFunctionSegmentKind.Expression:
          if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression segment requires expression.", nameof(expression));
          ConstantValue = null;
          Expression = expression.Trim();
          break;
        case ProgesiFunctionSegmentKind.Undefined:
          ConstantValue = null;
          Expression = null;
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(kind));
      }
    }

    internal static void ValidateInterval(double start, double end)
    {
      if (double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(end) || double.IsInfinity(end))
        throw new ArgumentOutOfRangeException(nameof(start), "Segment bounds must be finite.");
      if (start < -ProgesiAxisVariable.DefaultTolerance || end > 1.0 + ProgesiAxisVariable.DefaultTolerance)
        throw new ArgumentOutOfRangeException(nameof(start), "Segment bounds must lie within [0,1] (± tol).");
      if (end < start - ProgesiAxisVariable.DefaultTolerance)
        throw new ArgumentOutOfRangeException(nameof(end), "Segment end must be >= start.");
    }

    public bool Contains(double normalizedPosition, double tol = ProgesiAxisVariable.DefaultTolerance)
    {
      return normalizedPosition + tol >= Start && normalizedPosition <= End + tol;
    }

    public double? Evaluate(double normalizedPosition)
    {
      switch (Kind)
      {
        case ProgesiFunctionSegmentKind.Constant:
          return ConstantValue;
        case ProgesiFunctionSegmentKind.Undefined:
          return null;
        case ProgesiFunctionSegmentKind.Expression:
          return ProgesiFunctionExpressionEvaluator.Evaluate(Expression!, normalizedPosition);
        default:
          return null;
      }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Start;
      yield return End;
      yield return Kind;
      yield return ConstantValue.HasValue ? ConstantValue.Value : double.NaN;
      yield return Expression ?? string.Empty;
    }
  }

  /// <summary>
  /// Rhino-free piecewise function describing numeric variation along a normalized axis.
  /// </summary>
  public sealed class ProgesiFunction : ValueObject
  {
    public int Id { get; }
    public string Name { get; }
    public IReadOnlyList<ProgesiFunctionSegment> Segments { get; }

    public string Hashtag => ProgesiHash.Compute(this);
    public string ContentHash => Hashtag;

    public ProgesiFunction(int id, string name, IEnumerable<ProgesiFunctionSegment> segments)
    {
      Guard.Against.Negative(id, nameof(id));
      Guard.Against.NullOrWhiteSpace(name, nameof(name));
      Guard.Against.Null(segments, nameof(segments));

      var list = segments.ToList();
      if (list.Count == 0)
        throw new ArgumentException("At least one segment is required.", nameof(segments));

      ValidateNonOverlapping(list);

      Id = id;
      Name = name.Trim();
      Segments = list.AsReadOnly();
    }

    public double? Evaluate(double normalizedPosition, double tol = ProgesiAxisVariable.DefaultTolerance)
    {
      ValidateNormalizedPosition(normalizedPosition);

      foreach (var segment in Segments)
      {
        if (segment.Contains(normalizedPosition, tol))
          return segment.Evaluate(normalizedPosition);
      }

      return null;
    }

    public string ToJson() => JsonConvert.SerializeObject(ProgesiFunctionSerializationDto.FromDomain(this));

    public static ProgesiFunction FromJson(string json)
    {
      Guard.Against.NullOrWhiteSpace(json, nameof(json));
      var dto = JsonConvert.DeserializeObject<ProgesiFunctionSerializationDto>(json)
        ?? throw new FormatException("Invalid ProgesiFunction JSON payload.");
      return dto.ToDomain();
    }

    private static void ValidateNonOverlapping(IReadOnlyList<ProgesiFunctionSegment> segments)
    {
      var ordered = segments.OrderBy(s => s.Start).ThenBy(s => s.End).ToList();
      for (int i = 1; i < ordered.Count; i++)
      {
        if (ordered[i].Start < ordered[i - 1].End - ProgesiAxisVariable.DefaultTolerance)
          throw new ArgumentException("Function segments must not overlap.", nameof(segments));
      }
    }

    private static void ValidateNormalizedPosition(double positionNormalized)
    {
      if (double.IsNaN(positionNormalized) || double.IsInfinity(positionNormalized))
        throw new ArgumentOutOfRangeException(nameof(positionNormalized), "Position must be finite.");
      if (positionNormalized < -ProgesiAxisVariable.DefaultTolerance || positionNormalized > 1.0 + ProgesiAxisVariable.DefaultTolerance)
        throw new ArgumentOutOfRangeException(nameof(positionNormalized), "Position must lie within [0,1] (± tol).");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Id;
      yield return Name;
      foreach (var segment in Segments.OrderBy(s => s.Start).ThenBy(s => s.End))
        yield return segment;
    }

    internal sealed class ProgesiFunctionSerializationDto
    {
      public int Id { get; set; }
      public string Name { get; set; } = string.Empty;
      public List<SegmentDto> Segments { get; set; } = new List<SegmentDto>();

      internal sealed class SegmentDto
      {
        public double Start { get; set; }
        public double End { get; set; }
        public ProgesiFunctionSegmentKind Kind { get; set; }
        public double? ConstantValue { get; set; }
        public string? Expression { get; set; }
      }

      public static ProgesiFunctionSerializationDto FromDomain(ProgesiFunction function)
      {
        return new ProgesiFunctionSerializationDto
        {
          Id = function.Id,
          Name = function.Name,
          Segments = function.Segments.Select(s => new SegmentDto
          {
            Start = s.Start,
            End = s.End,
            Kind = s.Kind,
            ConstantValue = s.ConstantValue,
            Expression = s.Expression
          }).ToList()
        };
      }

      public ProgesiFunction ToDomain()
      {
        var segments = Segments.Select(s =>
          new ProgesiFunctionSegment(s.Start, s.End, s.Kind, s.ConstantValue, s.Expression));
        return new ProgesiFunction(Id, Name, segments);
      }
    }
  }
}
