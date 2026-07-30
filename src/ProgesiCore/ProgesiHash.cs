using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace ProgesiCore
{
  public static class ProgesiHash
  {
    /// <summary>Hashtag payload scheme version (v1 = current SHA-256 JSON payloads; hex is not prefixed).</summary>
    public const int HashtagSchemeVersion = 1;

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      NullValueHandling = NullValueHandling.Include
    };

    // ===== helper visibile anche da altri tipi =====
    public static string CanonicalValue(object? obj)
    {
      return obj is null
          ? "<null>"
          : obj switch
          {
            string s => s,
            bool b => b ? "true" : "false",
            int i => i.ToString(),
            long l => l.ToString(),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTime dt => dt.ToUniversalTime().ToString("o"),
            _ => JsonConvert.SerializeObject(obj, JsonSettings) ?? string.Empty,
          };
    }

    internal static string Sha256Hex(string s)
    {
      using var sha = SHA256.Create();
      byte[] bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
      byte[] hash = sha.ComputeHash(bytes);
      return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    private static string Sha256Hex(byte[] bytes)
    {
      using var sha = SHA256.Create();
      byte[] hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
      return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    private static string NormalizeUri(Uri u)
    {
      // lower-case host; remove trailing slash
      string s = u.ToString();
      if (u.IsAbsoluteUri)
      {
        var builder = new UriBuilder(u) { Host = u.Host.ToLowerInvariant() };
        s = builder.Uri.ToString();
      }
      if (s.EndsWith("/"))
      {
        s = s.Substring(0, s.Length - 1);
      }

      return s;
    }

    // ===== Compute per Variable =====
    public static string Compute(ProgesiVariable v)
    {
      int[] depends = (v.DependsFrom ?? Array.Empty<int>()).OrderBy(x => x).ToArray();
      int[] metadataIds = v.MetadataIds ?? Array.Empty<int>();

      string json;
      if (metadataIds.Length <= 1)
      {
        var payload = new
        {
          v.Name,
          Value = CanonicalValue(v.Value),
          Depends = depends,
          MetadataId = metadataIds.Length == 1 ? (int?)metadataIds[0] : null,
          Assumption = v.IsAssumption
        };
        json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
      }
      else
      {
        var payload = new
        {
          v.Name,
          Value = CanonicalValue(v.Value),
          Depends = depends,
          MetadataIds = metadataIds.OrderBy(x => x).ToArray(),
          Assumption = v.IsAssumption
        };
        json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
      }

      return Sha256Hex(json);
    }

    // ===== Compute per VariableCluster =====
    public static string Compute(ProgesiVariableCluster c)
    {
      if (c == null) throw new ArgumentNullException(nameof(c));

      int[] ids = (c.ProgesiVariableIds ?? Array.Empty<int>())
        .OrderBy(x => x)
        .ToArray();

      var payload = new
      {
        c.Name,
        VariableIds = ids,
        c.Description
      };

      string json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
      return Sha256Hex(json);
    }

    // ===== Compute per Metadata =====
    public static string Compute(ProgesiMetadata m)
    {
      string[] refs = (m.References ?? Array.Empty<Uri>())
                 .Select(NormalizeUri)
                 .OrderBy(s => s, StringComparer.Ordinal)
                 .ToArray();

      var snips = (m.Snips ?? Array.Empty<ProgesiSnip>())
          .Select(s => new
          {
            ContentHash = Sha256Hex(s.Content ?? Array.Empty<byte>()),
            MimeType = s.MimeType ?? "image/png",
            Source = s.Source ?? string.Empty
          })
          .OrderBy(x => x.ContentHash, StringComparer.Ordinal)
          .ThenBy(x => x.MimeType, StringComparer.Ordinal)
          .ThenBy(x => x.Source, StringComparer.Ordinal)
          .ToArray();

      var payload = new
      {
        m.CreatedBy,
        AdditionalInfo = m.AdditionalInfo ?? string.Empty,
        References = refs,
        Snips = snips
      };

      string json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
      return Sha256Hex(json);
    }

    // ===== Compute per Snip =====
    public static string Compute(ProgesiSnip snip)
    {
      if (snip == null) throw new ArgumentNullException(nameof(snip));

      var payload = new
      {
        ContentHash = Sha256Hex(snip.Content ?? Array.Empty<byte>()),
        MimeType = snip.MimeType ?? "image/png",
        Source = snip.Source ?? string.Empty
      };

      string json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
      return Sha256Hex(json);
    }

    // ===== Compute per ProgesiFunction =====
    public static string Compute(ProgesiFunction function)
    {
      if (function == null) throw new ArgumentNullException(nameof(function));

      var segments = function.Segments
        .OrderBy(s => s.Start)
        .ThenBy(s => s.End)
        .Select(s => new
        {
          s.Start,
          s.End,
          Kind = s.Kind.ToString(),
          Constant = s.ConstantValue.HasValue ? s.ConstantValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
          Expression = s.Expression ?? string.Empty
        })
        .ToArray();

      var payload = new
      {
        function.Name,
        Segments = segments
      };

      string json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
      return Sha256Hex(json);
    }

    // ===== Compute per ProgesiAxisVariable =====
    public static string Compute(ProgesiAxisVariable axis)
    {
      if (axis == null) throw new ArgumentNullException(nameof(axis));

      var entries = axis.EnumerateAll()
        .OrderBy(t => t.positionNormalized)
        .ThenBy(t => t.variableId)
        .Select(t => new { Position = t.positionNormalized, t.variableId })
        .ToArray();

      var keyPoints = (axis.KeyPoints ?? Array.Empty<double>()).OrderBy(x => x).ToArray();

      object? functionRef = null;
      if (!axis.FunctionRef.IsEmpty)
      {
        if (axis.FunctionRef.Embedded != null)
        {
          functionRef = new
          {
            EmbeddedHash = Compute(axis.FunctionRef.Embedded)
          };
        }
        else
        {
          functionRef = new
          {
            axis.FunctionRef.FunctionId,
            axis.FunctionRef.FunctionHashtag
          };
        }
      }

      var payload = new
      {
        axis.AxisName,
        AxisLength = axis.AxisLength.HasValue ? axis.AxisLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
        axis.CurvePayload,
        Mode = axis.Mode.ToString(),
        KeyPoints = keyPoints,
        axis.Name,
        axis.ValueTypeKey,
        axis.RuleId,
        FunctionRef = functionRef,
        Entries = entries
      };

      string json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
      return Sha256Hex(json);
    }
  }
}
