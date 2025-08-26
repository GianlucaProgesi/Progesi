using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace ProgesiCore
{
    /// <summary>
    /// Calcolo hash deterministico per Variable/Metadata.
    /// NON include Id/LastModified; normalizza e ordina collezioni;
    /// per Snip usa l'hash del contenuto.
    /// </summary>
    public static class ProgesiHash
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            Culture = System.Globalization.CultureInfo.InvariantCulture
        };

        public static string Compute(ProgesiVariable v)
        {
            var valueRepr = CanonicalValue(v.Value);

            var depends = (v.DependsFrom ?? Array.Empty<int>()).ToArray();
            Array.Sort(depends);

            var payload = new
            {
                Name = v.Name ?? string.Empty,
                Value = valueRepr,
                DependsFrom = depends,
                MetadataId = v.MetadataId ?? 0
            };

            var json = JsonConvert.SerializeObject(payload, JsonSettings);
            return Sha256Hex(json);
        }

        public static string Compute(ProgesiMetadata m)
        {
            // References normalizzate e ordinate
            var refs = (m.References ?? Array.Empty<Uri>())
                       .Select(NormalizeUri)
                       .OrderBy(s => s, StringComparer.Ordinal)
                       .ToArray();

            // Snip: hash del contenuto + metadati essenziali (NO Caption)
            var snips = (m.Snips ?? Array.Empty<ProgesiSnip>())
                        .Select(s => new
                        {
                            ContentHash = Sha256Hex(s.Content ?? Array.Empty<byte>()),
                            MimeType = s.MimeType ?? "image/png",
                            Source = s.Source ?? string.Empty
                        })
                        // ordina per stabilità
                        .OrderBy(x => x.ContentHash, StringComparer.Ordinal)
                        .ThenBy(x => x.MimeType, StringComparer.Ordinal)
                        .ThenBy(x => x.Source, StringComparer.Ordinal)
                        .ToArray();

            var payload = new
            {
                CreatedBy = m.CreatedBy ?? string.Empty,
                AdditionalInfo = m.AdditionalInfo ?? string.Empty,
                References = refs,
                Snips = snips
            };

            var json = JsonConvert.SerializeObject(payload, JsonSettings);
            return Sha256Hex(json);
        }


        private static string CanonicalValue(object obj)
        {
            if (obj == null) return "null";
            switch (obj)
            {
                case string s: return "s:" + s;
                case int i: return "i:" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case double d: return "d:" + d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case bool b: return "b:" + (b ? "1" : "0");
                default:
                    return "j:" + JsonConvert.SerializeObject(obj, JsonSettings);
            }
        }

        private static string NormalizeUri(Uri u)
        {
            if (u == null) return string.Empty;
            var s = u.ToString().Trim();
            if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
            try
            {
                var uri = new Uri(s, UriKind.RelativeOrAbsolute);
                if (uri.IsAbsoluteUri)
                {
                    var builder = new UriBuilder(uri)
                    {
                        Scheme = uri.Scheme.ToLowerInvariant(),
                        Host = uri.Host.ToLowerInvariant()
                    };
                    return builder.Uri.ToString().TrimEnd('/');
                }
            }
            catch { }
            return s;
        }

        private static string Sha256Hex(string text)
            => Sha256Hex(Encoding.UTF8.GetBytes(text));

        private static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
