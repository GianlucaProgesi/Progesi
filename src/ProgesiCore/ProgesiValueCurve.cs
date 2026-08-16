using System;
using System.Collections.Generic;

namespace ProgesiCore
{
  /// <summary>
  /// Domain-facing name for the ADR-019 value-curve container.
  /// Persisted and serialized as <see cref="ProgesiFunction"/>; no distinct storage identity.
  /// </summary>
  public sealed class ProgesiValueCurve
  {
    public ProgesiFunction Function { get; }

    public ProgesiValueCurve(ProgesiFunction function)
    {
      Function = function ?? throw new ArgumentNullException(nameof(function));
    }

    public int Id => Function.Id;
    public string Name => Function.Name;
    public IReadOnlyList<ProgesiFunctionSegment> Segments => Function.Segments;
    public string Hashtag => Function.Hashtag;
    public string ContentHash => Function.ContentHash;

    public static ProgesiValueCurve Create(int id, string name, IEnumerable<ProgesiFunctionSegment> segments)
      => new ProgesiValueCurve(new ProgesiFunction(id, name, segments));

    public static ProgesiValueCurve FromJson(string json)
      => new ProgesiValueCurve(ProgesiFunction.FromJson(json));

    public string ToJson() => Function.ToJson();

    public double? Evaluate(double normalizedPosition, double tol = ProgesiAxisVariable.DefaultTolerance)
      => Function.Evaluate(normalizedPosition, tol);

    public static implicit operator ProgesiFunction(ProgesiValueCurve curve) => curve.Function;

    public static implicit operator ProgesiValueCurve(ProgesiFunction function) => new ProgesiValueCurve(function);
  }
}
