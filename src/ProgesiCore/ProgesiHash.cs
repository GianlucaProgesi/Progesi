using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

#nullable enable
namespace ProgesiCore
{
    public static class ProgesiHash
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include
        };

        // ===== helper visibile anche da altri tipi =====
        public static string CanonicalValue(object? obj)
        {
            if (obj is null) return "<null>";
            switch (obj)
            {
                case string s:
                    return s;
                case bool b:
                    return b ? "true" : "false";
                case int i:
                    return i.ToString();
                case long l:
                    return l.ToString();
                case double d:
                    return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case float f:
                    return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case decimal m:
                    return m.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return JsonConvert.SerializeObject(obj, JsonSettings) ?? string.Empty;
            }
        }

        internal static string Sha256Hex(string s)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        private static string NormalizeUri(Uri u)
        {
            // lower-case host; remove trailing slash
            var s = u.ToString();
            if (u.IsAbsoluteUri)
            {
                var builder = new UriBuilder(u) { Host = u.Host.ToLowerInvariant() };
                s = builder.Uri.ToString();
            }
            if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
            return s;
        }

        // ===== Compute per Variable =====
        public static string Compute(ProgesiVariable v)
        {
            var depends = (v.DependsFrom ?? Array.Empty<int>()).OrderBy(x => x).ToArray();
            var payload = new
            {
                v.Name,
                Value = CanonicalValue(v.Value),
                Depends = depends,
                v.MetadataId
            };
            var json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
            return Sha256Hex(json);
        }

        // ===== Compute per Metadata =====
        public static string Compute(ProgesiMetadata m)
        {
            var refs = (m.References ?? Array.Empty<Uri>())
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

            var json = JsonConvert.SerializeObject(payload, JsonSettings) ?? string.Empty;
            return Sha256Hex(json);
        }
    }
}
