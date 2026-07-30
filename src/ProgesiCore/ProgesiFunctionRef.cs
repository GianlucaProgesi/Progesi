using System;
using Ardalis.GuardClauses;

namespace ProgesiCore
{
  /// <summary>
  /// Reference to a reusable <see cref="ProgesiFunction"/> by id/hashtag or an embedded copy.
  /// </summary>
  public sealed class ProgesiFunctionRef : ValueObject
  {
    public int? FunctionId { get; }
    public string? FunctionHashtag { get; }
    public ProgesiFunction? Embedded { get; }

    private ProgesiFunctionRef(int? functionId, string? functionHashtag, ProgesiFunction? embedded)
    {
      if (functionId.HasValue)
        Guard.Against.Negative(functionId.Value, nameof(functionId));
      if (!string.IsNullOrWhiteSpace(functionHashtag))
        FunctionHashtag = functionHashtag.Trim();
      else
        FunctionHashtag = null;

      FunctionId = functionId;
      Embedded = embedded;
    }

    public static ProgesiFunctionRef ById(int functionId) =>
      new ProgesiFunctionRef(functionId, null, null);

    public static ProgesiFunctionRef ByHashtag(string hashtag) =>
      new ProgesiFunctionRef(null, hashtag ?? throw new ArgumentNullException(nameof(hashtag)), null);

    public static ProgesiFunctionRef Embed(ProgesiFunction function) =>
      new ProgesiFunctionRef(null, null, function ?? throw new ArgumentNullException(nameof(function)));

    public static ProgesiFunctionRef Empty { get; } = new ProgesiFunctionRef(null, null, null);

    public bool IsEmpty =>
      !FunctionId.HasValue && string.IsNullOrWhiteSpace(FunctionHashtag) && Embedded == null;

    protected override System.Collections.Generic.IEnumerable<object> GetEqualityComponents()
    {
      yield return FunctionId.HasValue ? FunctionId.Value : int.MinValue;
      yield return FunctionHashtag ?? string.Empty;
      if (Embedded != null)
        yield return Embedded;
    }
  }
}
