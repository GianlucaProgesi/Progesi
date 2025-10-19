// MetadataRepositoryCompatExtensions.cs
#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
  /// Adattatori compatibili per i componenti GH – versione RHINO-only.
  ///
  /// VARIABLES
  ///   - Dedupe su content-hash di dominio (ProgesiHash.Compute su ProgesiVariable).
  ///   - Indici:
  ///       Progesi.VarHash       → content-hash ⇒ Id
  ///       Progesi.VarStrictHash → Sha256(content-hash + "|ID=<id>") ⇒ Id
  ///   - Lookup: priorità a Hash (summary umano "ID:..." oppure digest), fallback a Id.
  ///   - Ritorna anche Summary "umano" e ValueCanonical per VarIn/VarOut.
  ///
  /// METADATA
  ///   - Dedupe su coppia (By, Description) – Ref e Snip NON influiscono sull’indice.
  ///   - Indice:
  ///       Progesi.MetaContentHash → Sha256("BY=<UPPER>|INFO=<descr>") ⇒ Id
  ///   - Salva i Ref normalizzati (se presenti).
  ///   - Lookup: priorità a Hash (summary umano "ID:..." oppure digest), fallback a Id.
  ///   - Ritorna Summary umano + campi (By, Description, Refs, Snips, LM).
  /// </summary>
  internal static class MetadataRepositoryCompatExtensions
  {
    // =====================================================================
    // Utilities (riflessione, formato, hashing, KV helpers)
    // =====================================================================

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
        var list = new List<int>();
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
    private static string ToNorm(string s) => (s ?? "").Trim().ToUpperInvariant();

    // -------- Rhino StringTable come KV store --------
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

    // -------- hashing helpers --------
    private static string Sha256Hex(string s)
    {
      using var sha = SHA256.Create();
      var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
      var hash = sha.ComputeHash(bytes);
      var sb = new StringBuilder(hash.Length * 2);
      foreach (var b in hash) sb.Append(b.ToString("x2"));
      return sb.ToString();
    }

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

    // =====================================================================
    // METADATA
    // =====================================================================

    public static bool TryGetByHashThenId(object repoObj, string hash, int id, out object metadata, out string info)
    {
      metadata = null; info = string.Empty;

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;

        int rid = id;

        // priorità HASH: prima summary umano (ID:), poi digest content
        if (rid <= 0) { var tryId = ExtractIdFromSummary(hash); if (tryId > 0) rid = tryId; }
        if (rid <= 0)
        {
          var digest = ExtractDigest(hash);
          if (!string.IsNullOrWhiteSpace(digest))
            TryResolveIdByHash(table, "Progesi.MetaContentHash", digest, out rid);
        }

        if (rid <= 0) { info = "Input non valido (hash/id)."; return false; }

        var repo = new RhinoMetadataRepository(doc);
        var m = repo.GetAsync(rid).GetAwaiter().GetResult();
        if (m == null) { info = "Non trovato (rhino)"; return false; }

        var by = m.CreatedBy ?? "";
        var byN = ToNorm(by);
        var desc = m.AdditionalInfo ?? "";
        var refs = (m.References ?? Array.Empty<Uri>()).Select(u => u.ToString()).ToArray();
        var snips = (m.Snips ?? Array.Empty<ProgesiSnip>()).Select(s => "snip:" + (s.MimeType ?? "application/octet-stream")).ToArray();
        var summary = $"ID:{m.Id} | BY:{(string.IsNullOrEmpty(byN) ? "-" : byN)} | DESC:{desc}";

        metadata = new
        {
          Id = m.Id,
          Hash = summary,                 // summary umano
          Summary = summary,
          LastModified = m.LastModified.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
          By = string.IsNullOrWhiteSpace(by) ? "-" : by,
          Description = desc,
          Refs = refs,
          Snips = snips
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
        var descr = ReadString(payload, "info");   // NB: “info” = Description del componente
        var refsS = ReadString(payload, "rf");     // normalizzati in MetIn (pipe)
        // snipS disponibile ma ignorato (non persistiamo contenuti in questo HF)

        // dedupe BY + Description (Refs/Snip NON influiscono)
        var byN = ToNorm(by);
        var descrN = (descr ?? "").Trim();
        var contentSig = $"BY={byN}|INFO={descrN}";
        var contentHash = Sha256Hex(contentSig);

        if (id <= 0 && TryResolveIdByHash(table, "Progesi.MetaContentHash", contentHash, out var existsId))
          id = existsId;

        if (id <= 0) id = NextId(table, "Progesi.Meta", "__next__");

        var repo = new RhinoMetadataRepository(doc);

        // se update: togli vecchio content-hash se BY/INFO cambiano
        var current = repo.GetAsync(id).GetAwaiter().GetResult();
        if (current != null)
        {
          var prevSig = $"BY={ToNorm(current.CreatedBy ?? "")}|INFO={(current.AdditionalInfo ?? "").Trim()}";
          var prevHash = Sha256Hex(prevSig);
          if (!string.Equals(prevHash, contentHash, StringComparison.Ordinal))
            UnindexHash(table, "Progesi.MetaContentHash", prevHash);
        }

        // costruisci metadata e **salva i Ref normalizzati**
        var meta = ProgesiMetadata.Create(by ?? string.Empty, descr ?? string.Empty, null, null, DateTime.UtcNow, id);
        if (!string.IsNullOrWhiteSpace(refsS))
        {
          foreach (var r in refsS.Split('|'))
          {
            var s = r?.Trim(); if (string.IsNullOrEmpty(s)) continue;
            if (Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var u))
              meta.AddReference(u);
          }
        }
        repo.UpsertAsync(meta).GetAwaiter().GetResult();

        // indicizza BY+INFO
        IndexHash(table, "Progesi.MetaContentHash", contentHash, id);

        var summary = $"ID:{id} | BY:{(string.IsNullOrEmpty(byN) ? "-" : byN)} | DESC:{descrN}";
        persisted = new { Id = id, Hash = summary, Summary = summary };
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
        var table = rh.Doc.Strings;
        var repo = new RhinoMetadataRepository(doc);

        var current = repo.GetAsync(id).GetAwaiter().GetResult();
        if (current != null)
        {
          var prevSig = $"BY={ToNorm(current.CreatedBy ?? "")}|INFO={(current.AdditionalInfo ?? "").Trim()}";
          var prevHash = Sha256Hex(prevSig);
          UnindexHash(table, "Progesi.MetaContentHash", prevHash);
        }

        var ok = repo.DeleteAsync(id).GetAwaiter().GetResult();
        info = ok ? "OK" : "Delete non riuscita";
        return ok;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }

    // =====================================================================
    // VARIABLES
    // =====================================================================

    /// <summary>
    /// Lookup variabile con priorità all'Hash (summary umano "ID:..." oppure digest);
    /// fallback all'Id in input. Ritorna anche campi estesi per VarOut.
    /// </summary>
    public static bool TryGetVariableByHashThenId(object repoObj, string hash, int id, out object variable, out string info)
    {
      variable = null; info = string.Empty;

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;

        // 1) priorità HASH (summary umano con ID:..., poi digest strict/content)
        int rid = 0;
        var tryId = ExtractIdFromSummary(hash);
        if (tryId > 0) rid = tryId;

        if (rid <= 0)
        {
          var digest = ExtractDigest(hash);
          if (!string.IsNullOrWhiteSpace(digest))
          {
            if (!TryResolveIdByHash(table, "Progesi.VarStrictHash", digest, out rid))
              TryResolveIdByHash(table, "Progesi.VarHash", digest, out rid);
          }
        }

        // 2) fallback all'Id input
        if (rid <= 0 && id > 0) rid = id;

        if (rid <= 0) { info = "Input non valido (hash/id)."; return false; }

        var repo = new RhinoVariableRepository(doc);
        var v = repo.GetByIdAsync(rid).GetAwaiter().GetResult();
        if (v == null) { info = "Non trovato (rhino)"; return false; }

        var name = v.Name ?? "";
        var value = v.Value?.ToString() ?? "";
        var valc = ProgesiHash.CanonicalValue(v.Value);
        var by = "-";                       // non persistito lato Rhino
        var mid = v.MetadataId ?? 0;
        var deps = v.DependsFrom ?? Array.Empty<int>();
        var ass = v.IsAssumption ? "1" : "0";
        var depStr = deps.Length == 0 ? "-" : string.Join(",", deps);
        var nameN = name.Trim().ToUpperInvariant();
        var byN = by;
        var summary = $"ID:{v.Id} | NAME:{nameN} | VALC:{valc} | BY:{byN} | MID:{(mid > 0 ? mid.ToString() : "-")} | DEP:[{depStr}] | ASS:{ass}";

        variable = new
        {
          Id = v.Id,
          Hash = ProgesiHash.Compute(v),     // content-hash interno
          Name = name,
          Value = value,
          ValueCanonical = valc,
          By = by,
          LastModified = IsoNowUtc(),
          MetaId = mid,
          Depends = deps,
          IsAssumption = v.IsAssumption,
          Summary = summary
        };
        info = "OK";
        return true;
      }

      info = "Repo non supportato (serve RHINO).";
      return false;
    }

    /// <summary>
    /// Upsert variabile con dedupe su content-hash di dominio (ProgesiHash.Compute).
    /// Ritorna summary umano per VarIn e VarOut.
    /// </summary>
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
        var by = ReadString(payload, "by");            // solo per summary
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

        // resolve MetaId da mid (Id numerico o Hash di MetaContent)
        int? metaId = null;
        if (!string.IsNullOrWhiteSpace(midStr))
        {
          if (int.TryParse(midStr.Trim(), NumberStyles.Integer, inv, out var midNum) && midNum > 0)
            metaId = midNum;
          else
          {
            var digest = ExtractDigest(midStr.Trim());
            if (!string.IsNullOrWhiteSpace(digest) && TryResolveIdByHash(table, "Progesi.MetaContentHash", digest, out var ridMeta))
              metaId = ridMeta;
          }
        }

        // cast a tipo semplice
        object typedValue = value;
        if (int.TryParse(value, NumberStyles.Integer, inv, out var asInt)) typedValue = asInt;
        else if (double.TryParse(value, NumberStyles.Float, inv, out var asDbl)) typedValue = asDbl;
        else if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) typedValue = true;
        else if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) typedValue = false;

        // dedupe sul content-hash DI DOMINIO
        var depN = (depends ?? Array.Empty<int>()).Distinct().OrderBy(x => x).ToArray();
        var tmp = new ProgesiVariable(0, name ?? string.Empty, typedValue, depN, metaId, isAss);
        var contentHash = ProgesiHash.Compute(tmp);

        if (id <= 0 && TryResolveIdByHash(table, "Progesi.VarHash", contentHash, out var existsId))
          id = existsId;

        if (id <= 0) id = NextId(table, "Progesi.Var", "__next__");

        var repo = new RhinoVariableRepository(doc);

        // dis-indicizza vecchio content-hash se update
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

        var newContent = ProgesiHash.Compute(variableNew);
        IndexHash(table, "Progesi.VarHash", newContent, id);

        // strict-hash solo per compat
        var strictHash = Sha256Hex(newContent + "|ID=" + id.ToString(inv));
        IndexHash(table, "Progesi.VarStrictHash", strictHash, id);

        // summary umano per le porte
        var byN = ToNorm(by);
        var valc = ProgesiHash.CanonicalValue(typedValue);
        var depStr = depN.Length == 0 ? "-" : string.Join(",", depN);
        var assN = isAss ? "1" : "0";
        var nameN = ToNorm(name);
        var summary = $"ID:{id} | NAME:{nameN} | VALC:{valc} | BY:{(string.IsNullOrEmpty(byN) ? "-" : byN)} | MID:{(metaId.HasValue ? metaId.Value.ToString() : "-")} | DEP:[{depStr}] | ASS:{assN}";

        persisted = new
        {
          Id = id,
          Hash = summary,                 // per la porta Hash (umano)
          MetaId = metaId ?? 0,
          Depends = depN,
          IsAssumption = isAss,
          ValueCanonical = valc,
          Summary = summary              // per la porta Info
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
