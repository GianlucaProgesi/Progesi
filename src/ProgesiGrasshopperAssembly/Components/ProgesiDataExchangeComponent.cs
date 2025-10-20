// ProgesiDataExchangeComponent.cs
#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using ProgesiCore;
using Rhino;
using ProgesiGrasshopperAssembly.Infrastructure;
using ClosedXML.Excel;

namespace ProgesiGrasshopperAssembly.Components
{
  /// <summary>
  /// Unico DataEx per import/export.
  /// S1-5/A: ExportExcel  → crea un .xlsx con Variabili e Metadata dal repo Rhino
  /// S1-5/B: ImportExcel  → importa da .xlsx nel repo Rhino (dedupe coerenti con VarIn/MetIn)
  ///
  /// Act supportati (case-insensitive):
  ///   ExportExcel | ImportExcel
  ///   (placeholder futuri: ExportSqlite | ImportSqlite | ExportEf | ImportEf)
  /// </summary>
  public sealed class ProgesiDataExchangeComponent : GH_Component
  {
    public ProgesiDataExchangeComponent()
      : base("ProgesiDataExchange", "DataEx",
             "Importa/Esporta i dati Progesi (repo Rhino ↔ Excel/SQLite/EF).  S1-5A/B: ExportExcel / ImportExcel.",
             "Progesi", "IO")
    { }

    public override Guid ComponentGuid => new Guid("E5C4F9D7-1C2E-4C1A-9F3D-7C7A8C5AC101");
    protected override System.Drawing.Bitmap Icon => ProgesiGrasshopperAssembly.Infrastructure.ProgesiIcons.DataEx;

    // IN: Run, Act, Path, Overwrite
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);
      p.AddTextParameter("Act", "Act", "ExportExcel | ImportExcel", GH_ParamAccess.item, "ExportExcel");
      p.AddTextParameter("Path", "Path", "Percorso file .xlsx (in Export: destinazione; in Import: sorgente).", GH_ParamAccess.item, "");
      p.AddBooleanParameter("Overwrite", "Ovr", "Export: sovrascrive il file esistente. Import: ignorato.", GH_ParamAccess.item, true);
    }

    // OUT: Info, Path
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);
      p.AddTextParameter("Path", "Path", "Percorso completo del file usato/creato.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false; string act = "ExportExcel"; string path = ""; bool overwrite = true;
      DA.GetData(0, ref run);
      DA.GetData(1, ref act);
      DA.GetData(2, ref path);
      DA.GetData(3, ref overwrite);

      if (!run) { DA.SetData(0, "Idle"); DA.SetData(1, ""); return; }

      act = (act ?? "").Trim();
      try
      {
        switch (act.ToUpperInvariant())
        {
          case "EXPORTEXCEL":
            {
              var (outPath, msg) = ExportExcel(path, overwrite);
              DA.SetData(0, msg);
              DA.SetData(1, outPath ?? "");
              return;
            }
          case "IMPORTEXCEL":
            {
              var (src, msg) = ImportExcel(path);
              DA.SetData(0, msg);
              DA.SetData(1, src ?? "");
              return;
            }
          default:
            DA.SetData(0, "Act non supportato in questo step: " + act);
            DA.SetData(1, "");
            return;
        }
      }
      catch (Exception ex)
      {
        DA.SetData(0, "Errore: " + ex.Message);
        DA.SetData(1, "");
      }
    }

    // =========================================================================
    // S1-5/A Export → Excel (repo Rhino → file .xlsx)
    // =========================================================================
    private static (string path, string info) ExportExcel(string inPath, bool overwrite)
    {
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");

      // prepara path
      string p = NormalizeXlsxPathForExport(inPath);
      if (File.Exists(p))
      {
        if (!overwrite) throw new IOException("Il file esiste già: " + p);
        // prova a sbloccare
        try { using (var s = File.Open(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } } catch { /* best effort */ }
      }

      // leggi Variabili e Metadata dal StringTable (scan 1..__next__-1)
      var vars = ReadAllVariables(table);
      var mets = ReadAllMetadata(table);

      // crea XLSX (ClosedXML)
      using (var wb = new XLWorkbook())
      {
        // Sheet: ProgesiVariables
        var wv = wb.AddWorksheet("ProgesiVariables");
        int r = 1;
        wv.Cell(r, 1).Value = "Id";
        wv.Cell(r, 2).Value = "Hash";
        wv.Cell(r, 3).Value = "Name";
        wv.Cell(r, 4).Value = "Value";
        wv.Cell(r, 5).Value = "ValC";
        wv.Cell(r, 6).Value = "MetaId";
        wv.Cell(r, 7).Value = "Depends";
        wv.Cell(r, 8).Value = "Assumption";
        r++;

        foreach (var v in vars)
        {
          wv.Cell(r, 1).Value = v.Id;
          wv.Cell(r, 2).Value = v.Hash ?? "";
          wv.Cell(r, 3).Value = v.Name ?? "";
          wv.Cell(r, 4).Value = v.Value ?? "";
          wv.Cell(r, 5).Value = v.ValC ?? "";
          wv.Cell(r, 6).Value = v.MetaId;
          wv.Cell(r, 7).Value = string.Join(",", v.Depends ?? Array.Empty<int>());
          wv.Cell(r, 8).Value = v.Assumption ? 1 : 0;
          r++;
        }

        // Sheet: ProgesiMetadata
        var wm = wb.AddWorksheet("ProgesiMetadata");
        r = 1;
        wm.Cell(r, 1).Value = "Id";
        wm.Cell(r, 2).Value = "Hash";
        wm.Cell(r, 3).Value = "By";
        wm.Cell(r, 4).Value = "Description";
        wm.Cell(r, 5).Value = "Refs";
        wm.Cell(r, 6).Value = "LM";
        r++;

        foreach (var m in mets)
        {
          wm.Cell(r, 1).Value = m.Id;
          wm.Cell(r, 2).Value = m.Hash ?? "";
          wm.Cell(r, 3).Value = m.By ?? "";
          wm.Cell(r, 4).Value = m.Description ?? "";
          wm.Cell(r, 5).Value = string.Join("|", m.Refs ?? Array.Empty<string>());
          wm.Cell(r, 6).Value = m.LM ?? "";
          r++;
        }

        wv.Columns().AdjustToContents(); wm.Columns().AdjustToContents();
        wb.SaveAs(p);
      }

      string info = string.Format(CultureInfo.InvariantCulture,
        "OK ExportExcel → {0} (Vars:{1}, Meta:{2})", p, vars.Count, mets.Count);
      return (p, info);
    }

    private static string NormalizeXlsxPathForExport(string inPath)
    {
      string p = (inPath ?? "").Trim();
      if (string.IsNullOrWhiteSpace(p))
      {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        p = Path.Combine(desktop, "Progesi_Export.xlsx");
      }
      if (Directory.Exists(p))
      {
        p = Path.Combine(p, "Progesi_Export.xlsx");
      }
      if (Path.GetExtension(p).Length == 0)
      {
        p = p.TrimEnd('.', ' ') + ".xlsx";
      }
      return p;
    }

    // ========= Lettura Variabili (per Export) =========
    private static List<VarRow> ReadAllVariables(Rhino.DocObjects.Tables.StringTable table)
    {
      int max = ReadCounter(table, "Progesi.Var");
      var list = new List<VarRow>();
      if (max <= 1) return list;

      for (int id = 1; id < max; id++)
      {
        string json = table.GetValue("Progesi.Var", $"var:{id}");
        if (string.IsNullOrWhiteSpace(json)) continue;

        var dto = JsonConvert.DeserializeObject<VarDto>(json);
        if (dto == null) continue;

        object typed = ParseValue(dto.Value, dto.ValueType);
        var depends = dto.Depends ?? Array.Empty<int>();
        bool isAss = dto.IsAssumption ?? false;

        var pv = new ProgesiVariable(id, dto.Name ?? string.Empty, typed, depends, dto.MetadataId, isAss);
        string hash = ProgesiHash.Compute(pv);
        string valC = ProgesiHash.CanonicalValue(typed);

        list.Add(new VarRow
        {
          Id = id,
          Hash = hash,
          Name = dto.Name ?? "",
          Value = dto.Value ?? "",
          ValC = valC,
          MetaId = dto.MetadataId ?? 0,
          Depends = depends,
          Assumption = isAss
        });
      }
      return list;
    }

    private static object ParseValue(string value, string valueType)
    {
      if (string.Equals(valueType, "null", StringComparison.OrdinalIgnoreCase)) return null;
      try
      {
        switch ((valueType ?? "").ToLowerInvariant())
        {
          case "string": return value ?? "";
          case "int": return int.Parse(value);
          case "long": return long.Parse(value);
          case "double": return double.Parse(value, CultureInfo.InvariantCulture);
          case "bool": return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
          default:
            var t = Type.GetType(valueType, throwOnError: false);
            if (t == null) return value ?? "";
            return JsonConvert.DeserializeObject(value, t) ?? (object)(value ?? "");
        }
      }
      catch { return value ?? ""; }
    }

    private sealed class VarDto
    {
      public int Id { get; set; }
      public string Name { get; set; }
      public string ValueType { get; set; }
      public string Value { get; set; }
      public int? MetadataId { get; set; }
      public int[] Depends { get; set; }
      public bool? IsAssumption { get; set; }
    }

    private sealed class VarRow
    {
      public int Id { get; set; }
      public string Hash { get; set; }
      public string Name { get; set; }
      public string Value { get; set; }
      public string ValC { get; set; }
      public int MetaId { get; set; }
      public int[] Depends { get; set; }
      public bool Assumption { get; set; }
    }

    // ========= Lettura Metadata (per Export) =========
    private static List<MetaRow> ReadAllMetadata(Rhino.DocObjects.Tables.StringTable table)
    {
      int max = ReadCounter(table, "Progesi.Meta");
      var list = new List<MetaRow>();
      if (max <= 1) return list;

      for (int id = 1; id < max; id++)
      {
        string json = table.GetValue("Progesi.Meta", $"meta:{id}");
        if (string.IsNullOrWhiteSpace(json)) continue;

        var dto = JsonConvert.DeserializeObject<MetaDto>(json);
        if (dto == null) continue;

        var refs = new List<Uri>();
        if (dto.References != null)
        {
          foreach (var s in dto.References)
          {
            if (Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var u)) refs.Add(u);
          }
        }

        var m = ProgesiMetadata.Create(dto.CreatedBy ?? "", dto.AdditionalInfo ?? "",
                                       refs, null, dto.LastModified == default ? DateTime.UtcNow : dto.LastModified,
                                       id);
        string hash = ProgesiHash.Compute(m);

        list.Add(new MetaRow
        {
          Id = id,
          Hash = hash,
          By = dto.CreatedBy ?? "",
          Description = dto.AdditionalInfo ?? "",
          Refs = dto.References ?? Array.Empty<string>(),
          LM = (dto.LastModified == default ? DateTime.UtcNow : dto.LastModified)
                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
      }
      return list;
    }

    private sealed class MetaDto
    {
      public int Id { get; set; }
      public DateTime LastModified { get; set; }
      public string CreatedBy { get; set; }
      public string AdditionalInfo { get; set; }
      public string[] References { get; set; }
      public SnipDto[] Snips { get; set; }
    }

    private sealed class SnipDto
    {
      public string MimeType { get; set; }
      public string Caption { get; set; }
      public string Source { get; set; }
      public byte[] Content { get; set; }
    }

    private sealed class MetaRow
    {
      public int Id { get; set; }
      public string Hash { get; set; }
      public string By { get; set; }
      public string Description { get; set; }
      public string[] Refs { get; set; }
      public string LM { get; set; }
    }

    private static int ReadCounter(Rhino.DocObjects.Tables.StringTable table, string scope)
    {
      string s = table.GetValue(scope, "__next__");
      if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var next))
        return next;
      return 1;
    }

    // =========================================================================
    // S1-5/B Import → Excel (xlsx → repo Rhino)
    // =========================================================================
    private static (string path, string info) ImportExcel(string inPath)
    {
      string p = (inPath ?? "").Trim();
      if (string.IsNullOrWhiteSpace(p)) throw new ArgumentException("Path .xlsx non specificato.");
      if (!File.Exists(p)) throw new FileNotFoundException("File Excel non trovato.", p);

      object repo; string hub;
      ServiceHub.TryGetMetadataRepository(out repo, out hub);
      if (!(repo is ServiceHub.RhinoContext)) throw new InvalidOperationException("Repository Rhino non disponibile.");

      int metaProcessed = 0, varProcessed = 0;
      int maxMetaIdSeen = 0, maxVarIdSeen = 0;

      using (var wb = new XLWorkbook(p))
      {
        // --- Import METADATA prima (serve per risolvere MId nelle variabili)
        var metaSheet = FindSheet(wb, "ProgesiMetadata", "Metadata");
        if (metaSheet != null)
        {
          // header atteso:
          // Id | Hash | By | Description | Refs | LM
          var rows = metaSheet.RangeUsed()?.RowsUsed();
          if (rows != null)
          {
            bool first = true;
            foreach (var row in rows)
            {
              if (first) { first = false; continue; } // skip header
              int id = SafeInt(row.Cell(1).GetString());
              string by = row.Cell(3).GetString() ?? "";
              string descr = row.Cell(4).GetString() ?? "";
              string refs = row.Cell(5).GetString() ?? "";

              var payload = new
              {
                id = id,
                by = by,
                info = descr,
                rf = refs,
                sn = "" // ignorato in HF
              };

              object persisted; string upInfo;
              bool ok = MetadataRepositoryCompatExtensions.TryUpsert(repo, payload, out persisted, out upInfo);
              if (ok) metaProcessed++;

              if (id > maxMetaIdSeen) maxMetaIdSeen = id;
              // se il compat ha assegnato un nuovo id, prova a leggerlo
              int pid = 0;
              ReadIf(persisted, "Id", ref pid);
              if (pid > maxMetaIdSeen) maxMetaIdSeen = pid;
            }
          }
        }

        // --- Import VARIABILI
        var varSheet = FindSheet(wb, "ProgesiVariables", "Variables");
        if (varSheet != null)
        {
          // header atteso:
          // Id | Hash | Name | Value | ValC | MetaId | Depends | Assumption
          var rows = varSheet.RangeUsed()?.RowsUsed();
          if (rows != null)
          {
            bool first = true;
            foreach (var row in rows)
            {
              if (first) { first = false; continue; }
              int id = SafeInt(row.Cell(1).GetString());
              string name = row.Cell(3).GetString() ?? "";
              string value = row.Cell(4).GetString() ?? "";
              int metaId = SafeInt(row.Cell(6).GetString());
              string dependsS = row.Cell(7).GetString() ?? "";
              int[] depends = ParseDepends(dependsS);
              bool ass = SafeBool(row.Cell(8).GetString());

              var payload = new
              {
                id = id,
                name = name,
                value = value,
                unit = "",
                by = "", // non persiste nel repo Rhino
                isAssumption = ass,
                mid = metaId > 0 ? metaId.ToString(CultureInfo.InvariantCulture) : "",
                depends = depends
              };

              object persisted; string upInfo;
              bool ok = MetadataRepositoryCompatExtensions.TryUpsertVariable(repo, payload, out persisted, out upInfo);
              if (ok) varProcessed++;

              if (id > maxVarIdSeen) maxVarIdSeen = id;
              int pid = 0;
              ReadIf(persisted, "Id", ref pid);
              if (pid > maxVarIdSeen) maxVarIdSeen = pid;
            }
          }
        }
      }

      // aggiorna i contatori __next__ coerenti (max+1)
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
      if (maxMetaIdSeen > 0)
        table.SetString("Progesi.Meta", "__next__", (maxMetaIdSeen + 1).ToString(CultureInfo.InvariantCulture));
      if (maxVarIdSeen > 0)
        table.SetString("Progesi.Var", "__next__", (maxVarIdSeen + 1).ToString(CultureInfo.InvariantCulture));

      string info = string.Format(CultureInfo.InvariantCulture,
        "OK ImportExcel ← {0} (Meta:{1}, Vars:{2})", p, metaProcessed, varProcessed);
      return (p, info);
    }

    private static IXLWorksheet FindSheet(XLWorkbook wb, params string[] names)
    {
      foreach (var n in names)
      {
        if (wb.TryGetWorksheet(n, out var ws)) return ws;
      }
      // fallback case-insensitive
      foreach (var ws in wb.Worksheets)
      {
        foreach (var n in names)
        {
          if (string.Equals(ws.Name, n, StringComparison.OrdinalIgnoreCase)) return ws;
        }
      }
      return null;
    }

    private static int SafeInt(string s)
    {
      if (int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
      return 0;
    }

    private static bool SafeBool(string s)
    {
      var t = (s ?? "").Trim();
      if (t == "1") return true;
      if (t == "0") return false;
      bool b; if (bool.TryParse(t, out b)) return b;
      return false;
    }

    private static int[] ParseDepends(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return Array.Empty<int>();
      var tokens = s.Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
      var list = new List<int>();
      foreach (var t in tokens)
      {
        if (int.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
          list.Add(n);
      }
      list.Sort();
      return list.ToArray();
    }

    // piccoli helper reflection per leggere persisted.Id
    private static void ReadIf(object obj, string prop, ref int target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      if (v == null) return;
      if (int.TryParse(v.ToString(), out var n)) target = n;
    }
  }
}
