// MetadataRepositoryCompatExtensions.cs
#nullable disable
using System;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.DocObjects.Tables;
using ProgesiCore;
using ProgesiRepositories.Rhino;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Adattatori compatibili per i componenti GH.
  /// Versione RHINO-ONLY: niente SQLite né mock file.
  /// </summary>
  internal static class MetadataRepositoryCompatExtensions
  {
    // ---- DTO minimi per l'UI ----
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

    // ---- helpers generici ----
    private static string ReadString(object obj, string name)
    {
      if (obj == null) return "";
      var pi = obj.GetType().GetProperty(name,
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
      if (pi == null) return "";
      var v = pi.GetValue(obj, null);
      return v == null ? "" : v.ToString();
    }

    private static int ReadInt(object obj, string name)
    {
      if (obj == null) return 0;
      var pi = obj.GetType().GetProperty(name,
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
      if (pi == null) return 0;
      var v = pi.GetValue(obj, null);
      if (v == null) return 0;
      return int.TryParse(v.ToString(), out var n) ? n : 0;
    }

    private static string IsoNowUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    // ---- helpers RHINO (StringTable come KV store) ----
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

    private static string[] SplitPipe(string s)
      => string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

    // ================= METADATA =================

    public static bool TryGetByHashThenId(object repoObj, string hash, int id, out object metadata, out string info)
    {
      metadata = null; info = string.Empty;

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;
        var rid = id;

        if (!string.IsNullOrWhiteSpace(hash) && rid <= 0)
          TryResolveIdByHash(table, "Progesi.MetaHash", hash, out rid);

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
        var rf = ReadString(payload, "rf"); // refs pipe-sep.
        var sn = ReadString(payload, "sn"); // snips pipe-sep. (ignorato per HF)

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

    // ================= VARIABLES =================

    public static bool TryGetVariableByHashThenId(object repoObj, string hash, int id, out object variable, out string info)
    {
      variable = null; info = string.Empty;

      if (repoObj is ServiceHub.RhinoContext rh)
      {
        var doc = rh.Doc;
        var table = doc.Strings;
        var rid = id;

        if (!string.IsNullOrWhiteSpace(hash) && rid <= 0)
          TryResolveIdByHash(table, "Progesi.VarHash", hash, out rid);

        if (rid <= 0) { info = "Input non valido (hash/id)."; return false; }

        var repo = new RhinoVariableRepository(doc);
        var v = repo.GetByIdAsync(rid).GetAwaiter().GetResult();
        if (v == null) { info = "Non trovato (rhino)"; return false; }

        variable = new LiveVarDto
        {
          Id = v.Id,
          Hash = ProgesiHash.Compute(v),
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

        var inv = CultureInfo.InvariantCulture;
        if (double.TryParse(value, NumberStyles.Any, inv, out var vFix) &&
            !string.IsNullOrWhiteSpace(unit) &&
            double.TryParse(unit, NumberStyles.Any, inv, out var uFix))
        {
          value = (vFix * uFix).ToString(inv);
          unit = "";
        }

        if (id <= 0) id = NextId(table, "Progesi.Var", "__next__");

        object typedValue = value;
        if (int.TryParse(value, NumberStyles.Integer, inv, out var asInt)) typedValue = asInt;
        else if (double.TryParse(value, NumberStyles.Float, inv, out var asDbl)) typedValue = asDbl;
        else if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) typedValue = true;
        else if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) typedValue = false;

        var repo = new RhinoVariableRepository(doc);

        var current = repo.GetByIdAsync(id).GetAwaiter().GetResult();
        if (current != null)
        {
          var oldHash = ProgesiHash.Compute(current);
          UnindexHash(table, "Progesi.VarHash", oldHash);
        }

        var variableNew = new ProgesiVariable(id, name ?? string.Empty, typedValue, dependsFrom: Array.Empty<int>(), metadataId: null);
        repo.SaveAsync(variableNew).GetAwaiter().GetResult();

        var newHash = ProgesiHash.Compute(variableNew);
        IndexHash(table, "Progesi.VarHash", newHash, id);

        persisted = new { Id = id, Hash = newHash, Name = name ?? string.Empty, Value = value ?? "" };
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
          var h = ProgesiHash.Compute(current);
          UnindexHash(table, "Progesi.VarHash", h);
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
