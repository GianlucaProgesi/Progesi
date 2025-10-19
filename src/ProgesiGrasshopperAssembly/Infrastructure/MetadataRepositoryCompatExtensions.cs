// MetadataRepositoryCompatExtensions.cs
#nullable disable
using System;
using System.Globalization;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Rhino;
using Rhino.DocObjects.Tables;
using ProgesiCore;
using ProgesiRepositories.Rhino;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Adattatori per i componenti GH (RHINO-only).
  /// - DEDUPE su content-hash (Name, ValC, Depends, MetaId, Ass, By)
  /// - Hash “umano” sulle porte (ID/NAME/VALC/BY/MID/DEP/ASS)
  /// - Lookup Hash: accetta riepilogo con ID:... oppure digest esadecimale (64 char)
  /// </summary>
  internal static class MetadataRepositoryCompatExtensions
  {
    private sealed class LiveMetaDto
    {
      public int Id { get; set; }
      public string Hash { get; set; }
      public string By { get; set; }
      public string[] Refs { get; set; }
      public string[] Snips { get; set; }
      public string LastModified { get; set; }
    }

    private sealed class LiveVarDto
    {
      public int Id { get; set; }
      public string Hash { get; set; }
      public string Name { get; set; }
      public string Value { get; set; }
      public string Unit { get; set; }
      public string By { get; set; }
      public string LastModified { get; set; }
    }

    // ---------- helpers ----------
    private static string ReadString(object obj, string name)
    {
      if (obj == null) return "";
      var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return "";
      var v = pi.GetValue(obj, null);
      return v == null ? "" : v.ToString();
    }
    private static int ReadInt(object obj, string name)
    {
      if (obj == null) return 0;
      var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return 0;
      var v = pi.GetValue(obj, null);
      if (v == null) return 0;
      return int.TryParse(v.ToString(), out var n) ? n : 0;
    }
    private static bool ReadBool(object obj, string name)
    {
      if (obj == null) return false;
      var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return false;
      var v = pi.GetValue(obj, null);
      if (v == null) return false;
      if (v is bool b) return b;
      bool bb; return bool.TryParse(v.ToString(), out bb) && bb;
    }
    private static int[] ReadDepends(object payload)
    {
      var pi = payload?.GetType().GetProperty("depends", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return Array.Empty<int>();
      var v = pi.GetValue(payload, null);
      if (v == null) return Array.Empty<int>();

      if (v is IEnumerable en && !(v is string))
      {
        var list = new System.Collections.Generic.List<int>();
        foreach (var o in en)
        {
          if (o == null) continue;
          if (o is int ii) { list.Add(ii); continue; }
          int n; if (int.TryParse(o.ToString(), out n)) list.Add(n);
        }
        return list.Distinct().Where(x => x > 0).OrderBy(x => x).ToArray();
      }

      var s = v.ToString() ?? "";
      var tokens = s.Split(new[] { '|', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
      return tokens.Select(t => { int n; return int.TryParse(t.Trim(), out n) ? n : 0; })
                   .Where(n => n > 0).Distinct().OrderBy(n => n).ToArray();
    }

    private static string IsoNowUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static int NextId(StringTable table, string scope, string counterKey)
    {
      if (table == null) return 1;
      var s = table.GetValue(scope, counterKey) ?? string.Empty;
      if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cur)) cur = 0;
      var next = (cur <= 0 ? 1 : cur + 1);
      table.SetString(scope, counterKey, next.ToString(CultureInfo.InvariantCulture));
      return next;
    }

    private static bool TryResolveIdByHash(StringTable table, string indexScope, string hash, out int id)
    {
      id = 0;
      if (table == null || string.IsNullOrWhiteSpace(hash)) return false;
      var val = table.GetValue(indexScope, hash) ?? string.Empty;
      return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id > 0;
    }
    private static void IndexHash(StringTable table, string indexScope, string hash, int id)
    {
      if (table == null || string.IsNullOrWhiteSpace(hash) || id <= 0) return;
      table.SetString(indexScope, hash, id.ToString(CultureInfo.InvariantCulture));
    }
    private static void UnindexHash(StringTable table, string indexScope, string hash)
    {
      if (table == null || string.IsNullOrWhiteSpace(hash)) return;
      table.Delete(indexScope, hash);
    }

    private static string ToNorm(string s) => (s ?? "").Trim().ToUpperInvariant();

    private static string Sha256Hex(string s)
    {
      using var sha = SHA256.Create();
      var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
      var hash = sha.ComputeHash(bytes);
      var sb = new StringBuilder(hash.Length * 2);
      foreach (var b in hash) sb.Append(b.ToString("x2"));
      return sb.ToString();
    }

    // estrae il primo digest hex (64 char) da una stringa
    private static string ExtractDigest(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return "";
      s = s.Trim();
      int run = 0, start = -1;
      for (int i = 0; i < s.Length; i++)
      {
        char c = s[i];
        bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        if (hex) { if (run == 0) start = i; run++; if (run >= 64) return s.Substring(start, 64).ToLowerInvariant(); }
        else { run = 0; start = -1; }
      }
      return "";
    }

    // estrae ID:123 da un “hash” riepilogo
    private static int ExtractIdFromSummary(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return 0;
      var idx = s.IndexOf("ID:", StringComparison.OrdinalIgnoreCase);
      if (idx < 0) return 0;
      idx += 3;
      int n = 0; int i = idx;
      while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
      while (i < s.Length && char.IsDigit(s[i])) { n = checked(n * 10 + (s[i] - '0')); i++; }
      return n;
    }

    // ===================== METADATA =====================
    public static bool TryGetByHashThenId(object repoObj, string hash, int id, out object metadata, out string info)
    {
      metadata = null; info = string.Empty;

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;
        var rid = id;

        if (rid <= 0)
        {
          var tryId = ExtractIdFromSummary(hash);
          if (tryId > 0) rid = tryId;
        }

        if (rid <= 0)
        {
          var digest = ExtractDigest(hash);
          if (!string.IsNullOrWhiteSpace(digest))
            TryResolveIdByHash(table, "Progesi.MetaHash", digest, out rid);
        }

        if (rid <= 0) { info = "Input non valido (hash/id)."; return false; }

        var repo = new RhinoMetadataRepository(doc);
        var m = repo.GetAsync(rid).GetAwaiter().GetResult();
        if (m == null) { info = "Non trovato (rhino)"; return false; }

        metadata = new LiveMetaDto
        {
          Id = m.Id,
          Hash = ProgesiHash.Compute(m),
          By = m.CreatedBy ?? "",
          Refs = m.References?.Select(u => u.ToString()).ToArray() ?? Array.Empty<string>(),
          Snips = m.Snips?.Select(_ => "snip:" + _.MimeType).ToArray() ?? Array.Empty<string>(),
          LastModified = m.LastModified.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        info = "OK";
        return true;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }

    public static bool TryUpsert(object repoObj, object payload, out object persisted, out string info)
    {
      persisted = null; info = "OK";

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;

        var id = ReadInt(payload, "id");
        var by = ReadString(payload, "by");
        var descr = ReadString(payload, "info");
        var rf = ReadString(payload, "rf");

        if (id <= 0) id = NextId(table, "Progesi.Meta", "__next__");

        var meta = ProgesiMetadata.Create(by ?? string.Empty, descr ?? string.Empty, null, null, DateTime.UtcNow, id);

        if (!string.IsNullOrWhiteSpace(rf))
        {
          foreach (var r in rf.Split('|'))
            if (Uri.TryCreate(r, UriKind.RelativeOrAbsolute, out var u)) meta.AddReference(u);
        }

        var repo = new RhinoMetadataRepository(doc);
        repo.UpsertAsync(meta).GetAwaiter().GetResult();

        var newHash = ProgesiHash.Compute(meta);
        IndexHash(table, "Progesi.MetaHash", newHash, id);

        persisted = new { Id = id, Hash = newHash };
        return true;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }

    public static bool TryDelete(object repoObj, int id, out string info)
    {
      info = "OK";

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;
        var repo = new RhinoMetadataRepository(doc);

        var current = repo.GetAsync(id).GetAwaiter().GetResult();
        if (current != null)
        {
          var h = ProgesiHash.Compute(current);
          UnindexHash(table, "Progesi.MetaHash", h);
        }

        var ok = repo.DeleteAsync(id).GetAwaiter().GetResult();
        info = ok ? "OK" : "Delete non riuscita";
        return ok;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }

    // ===================== VARIABLES =====================
    public static bool TryGetVariableByHashThenId(object repoObj, string hash, int id, out object variable, out string info)
    {
      variable = null; info = string.Empty;

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;
        var rid = id;

        if (rid <= 0)
        {
          var tryId = ExtractIdFromSummary(hash);
          if (tryId > 0) rid = tryId;
        }

        if (rid <= 0)
        {
          var digest = ExtractDigest(hash);
          if (!string.IsNullOrWhiteSpace(digest))
          {
            if (!TryResolveIdByHash(table, "Progesi.VarStrictHash", digest, out rid))
              TryResolveIdByHash(table, "Progesi.VarHash", digest, out rid);
          }
        }

        if (rid <= 0) { info = "Input non valido (hash/id)."; return false; }

        var repo = new RhinoVariableRepository(doc);
        var v = repo.GetByIdAsync(rid).GetAwaiter().GetResult();
        if (v == null) { info = "Non trovato (rhino)"; return false; }

        variable = new LiveVarDto
        {
          Id = v.Id,
          Hash = ProgesiHash.Compute(v), // content-hash interno
          Name = v.Name ?? "",
          Value = v.Value?.ToString() ?? "",
          Unit = "",
          By = "",
          LastModified = IsoNowUtc()
        };
        info = "OK";
        return true;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }

    public static bool TryUpsertVariable(object repoObj, object payload, out object persisted, out string info)
    {
      persisted = null; info = "OK";

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;

        var id = ReadInt(payload, "id");
        var name = ReadString(payload, "name");
        var value = ReadString(payload, "value");
        var unit = ReadString(payload, "unit");
        var by = ReadString(payload, "by");
        var isAss = ReadBool(payload, "isAssumption");
        var midStr = ReadString(payload, "mid");
        var depends = ReadDepends(payload);

        var inv = CultureInfo.InvariantCulture;
        // Value × Unit se ENTRAMBI numerici
        if (double.TryParse(value, NumberStyles.Any, inv, out var vFix) &&
            double.TryParse(unit, NumberStyles.Any, inv, out var uFix))
        {
          value = (vFix * uFix).ToString(inv);
          unit = "";
        }

        // risolvi MetadataId
        int? metaId = null;
        if (!string.IsNullOrWhiteSpace(midStr))
        {
          if (int.TryParse(midStr.Trim(), NumberStyles.Integer, inv, out var midNum) && midNum > 0)
            metaId = midNum;
          else if (TryResolveIdByHash(table, "Progesi.MetaHash", midStr.Trim(), out var rid))
            metaId = rid;
        }

        // canonicalizza value e normalizza altri campi
        object typedValue = value;
        if (int.TryParse(value, NumberStyles.Integer, inv, out var asInt)) typedValue = asInt;
        else if (double.TryParse(value, NumberStyles.Float, inv, out var asDbl)) typedValue = asDbl;
        else if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) typedValue = true;
        else if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) typedValue = false;

        var valC = ProgesiHash.CanonicalValue(typedValue);
        var nameN = ToNorm(name);
        var byN = ToNorm(by);
        var depN = (depends ?? Array.Empty<int>()).Distinct().OrderBy(x => x).ToArray();
        var assN = isAss ? "1" : "0";
        var midN = metaId.HasValue ? metaId.Value.ToString(inv) : "-";

        // ---- content-hash (senza Id) per dedupe
        var contentSig = $"NAME={nameN}|VALC={valC}|DEP=[{string.Join(",", depN)}]|MID={midN}|ASS={assN}|BY={byN}";
        var contentHash = Sha256Hex(contentSig);

        // dedupe: se esiste già, riusa l'Id
        if (id <= 0 && TryResolveIdByHash(table, "Progesi.VarHash", contentHash, out var existsId))
          id = existsId;

        if (id <= 0) id = NextId(table, "Progesi.Var", "__next__");

        var repo = new RhinoVariableRepository(doc);

        // se update: dis-indicizza vecchio content-hash
        var current = repo.GetByIdAsync(id).GetAwaiter().GetResult();
        if (current != null)
        {
          var oldContent = ProgesiHash.Compute(current);
          UnindexHash(table, "Progesi.VarHash", oldContent);
        }

        var variableNew = new ProgesiVariable(id, name ?? string.Empty, typedValue,
                                              dependsFrom: depN,
                                              metadataId: metaId,
                                              isAssumption: isAss);
        repo.SaveAsync(variableNew).GetAwaiter().GetResult();

        // indicizza content-hash
        IndexHash(table, "Progesi.VarHash", contentHash, id);

        // strict-hash (include anche l’Id) — solo per compat interna/lookup
        var strictSig = $"{contentSig}|ID={id}";
        var strictHash = Sha256Hex(strictSig);
        IndexHash(table, "Progesi.VarStrictHash", strictHash, id);

        // ---- hash “umano” per le porte GH (niente digest davanti)
        var summary = $"ID:{id} | NAME:{nameN} | VALC:{valC} | BY:{(string.IsNullOrEmpty(byN) ? "-" : byN)} | MID:{midN} | DEP:[{string.Join(",", depN)}] | ASS:{assN}";

        persisted = new
        {
          Id = id,
          Hash = summary,          // porta Hash
          MetaId = metaId ?? 0,
          Depends = depN,
          IsAssumption = isAss,
          ValueCanonical = valC,
          Summary = summary        // per la porta Info
        };
        return true;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }

    public static bool TryDeleteVariable(object repoObj, int id, out string info)
    {
      info = "OK";

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;
        var repo = new RhinoVariableRepository(doc);

        var current = repo.GetByIdAsync(id).GetAwaiter().GetResult();
        if (current != null)
        {
          var oldContent = ProgesiHash.Compute(current);
          UnindexHash(table, "Progesi.VarHash", oldContent);
        }

        var ok = repo.DeleteAsync(id).GetAwaiter().GetResult();
        info = ok ? "OK" : "Delete non riuscita";
        return ok;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }
  }
}
