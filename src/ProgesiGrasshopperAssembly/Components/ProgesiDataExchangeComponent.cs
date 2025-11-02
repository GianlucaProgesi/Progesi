// ProgesiDataExchangeComponent.cs
#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using ProgesiCore;
using Rhino;
using ProgesiGrasshopperAssembly.Infrastructure;
using ClosedXML.Excel;

namespace ProgesiGrasshopperAssembly.Components
{
  /// <summary>
  /// Unico DataEx per import/export (repo Rhino ↔ Excel/SQLite/EF).
  /// S1-5/A: ExportExcel
  /// S1-5/B: ImportExcel (dedupe coerenti con Var/Meta)
  /// S1-5/C: ImportExcel Validation & Errori (Strict/Lenient, header/mapping/coercizione, log dettagliato)
  ///
  /// Act supportati (case-insensitive):
  ///   ExportExcel | ImportExcel
  /// (placeholder futuri: ExportSqlite | ImportSqlite | ExportEf | ImportEf)
  /// </summary>
  public sealed class ProgesiDataExchangeComponent : GH_Component
  {
    public ProgesiDataExchangeComponent()
      : base("ProgesiDataExchange", "DataEx",
             "Importa/Esporta i dati Progesi (repo Rhino ↔ Excel/SQLite/EF).  S1-5A/B/C: ExportExcel / ImportExcel (+validazione).",
             "Progesi", "IO")
    { }

    public override Guid ComponentGuid => new Guid("E5C4F9D7-1C2E-4C1A-9F3D-7C7A8C5AC101");
    protected override System.Drawing.Bitmap Icon => ProgesiGrasshopperAssembly.Infrastructure.ProgesiIcons.DataEx;

    // IN: Run, Act, Path, Overwrite, Mode
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);
      p.AddTextParameter("Act", "Act", "ExportExcel | ImportExcel", GH_ParamAccess.item, "ExportExcel");
      p.AddTextParameter("Path", "Path", "Percorso file .xlsx (Export: destinazione; Import: sorgente).", GH_ParamAccess.item, "");
      p.AddBooleanParameter("Overwrite", "Ovr", "Export: sovrascrive il file esistente. Import: ignorato.", GH_ParamAccess.item, true);

      // S1-5/C: Strict | Lenient (default)
      p.AddTextParameter("Mode", "Mode", "Import: Strict (rigido) oppure Lenient (tollerante).", GH_ParamAccess.item, "Lenient");
    }

    // OUT: Info, Path
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);
      p.AddTextParameter("Path", "Path", "Percorso completo del file usato/creato (o log).", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false; string act = "ExportExcel"; string path = ""; bool overwrite = true; string mode = "Lenient";
      DA.GetData(0, ref run);
      DA.GetData(1, ref act);
      DA.GetData(2, ref path);
      DA.GetData(3, ref overwrite);
      DA.GetData(4, ref mode);

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
              var strict = string.Equals((mode ?? "").Trim(), "STRICT", StringComparison.OrdinalIgnoreCase);
              var (src, logPath, msg) = ImportExcelValidated(path, strict);
              DA.SetData(0, msg);
              DA.SetData(1, string.IsNullOrWhiteSpace(logPath) ? (src ?? "") : logPath);
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

      string p = NormalizeXlsxPathForExport(inPath);
      if (File.Exists(p))
      {
        if (!overwrite) throw new IOException("Il file esiste già: " + p);
        try { using (var s = File.Open(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } } catch { /* best effort */ }
      }

      var vars = ReadAllVariables(table);
      var mets = ReadAllMetadata(table);

      using (var wb = new XLWorkbook())
      {
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
      if (Directory.Exists(p)) p = Path.Combine(p, "Progesi_Export.xlsx");
      if (Path.GetExtension(p).Length == 0) p = p.TrimEnd('.', ' ') + ".xlsx";
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
    // S1-5/C Import → Excel con validazione & log
    // =========================================================================
    private static (string srcPath, string logPath, string info) ImportExcelValidated(string inPath, bool strict)
    {
      string p = (inPath ?? "").Trim();
      if (string.IsNullOrWhiteSpace(p)) throw new ArgumentException("Path .xlsx non specificato.");
      if (!File.Exists(p)) throw new FileNotFoundException("File Excel non trovato.", p);

      var log = new List<string>();
      void LOG(string level, string msg) => log.Add($"[{DateTime.UtcNow:HH:mm:ss}] {level}: {msg}");

      object repo; string hub;
      ServiceHub.TryGetMetadataRepository(out repo, out hub);
      if (!(repo is ServiceHub.RhinoContext)) throw new InvalidOperationException("Repository Rhino non disponibile.");
      LOG("INFO", $"Start ImportExcel (mode={(strict ? "Strict" : "Lenient")}) → {p}");

      int metaRows = 0, metaOk = 0, metaWarn = 0, metaErr = 0;
      int varRows = 0, varOk = 0, varWarn = 0, varErr = 0;
      int maxMetaIdSeen = 0, maxVarIdSeen = 0;

      using (var wb = new XLWorkbook(p))
      {
        // ----- METADATA -----
        var wsMeta = FindSheet(wb, "ProgesiMetadata", "Metadata");
        if (wsMeta == null)
        {
          string m = "Sheet 'ProgesiMetadata' non trovato.";
          if (strict) { LOG("ERROR", m); throw new InvalidOperationException(m); }
          else { LOG("WARN", m); }
        }
        else
        {
          var hMeta = BuildHeaderMap(wsMeta, out int firstRow, out int lastRow);
          var reqMeta = new[] { "BY", "DESCRIPTION" };
          var missMeta = MissingHeaders(hMeta, reqMeta);
          if (missMeta.Count > 0)
          {
            string m = "Header mancanti in ProgesiMetadata: " + string.Join(",", missMeta);
            if (strict) { LOG("ERROR", m); throw new InvalidOperationException(m); }
            else { LOG("WARN", m); }
          }

          for (int r = firstRow + 1; r <= lastRow; r++)
          {
            metaRows++;
            string by = ReadCell(wsMeta, r, hMeta, "BY");
            string descr = ReadCell(wsMeta, r, hMeta, "DESCRIPTION");
            string refs = ReadCell(wsMeta, r, hMeta, "REFS");
            int id = SafeInt(ReadCell(wsMeta, r, hMeta, "ID"));

            if (string.IsNullOrWhiteSpace(by) && string.IsNullOrWhiteSpace(descr))
            { LOG("WARN", $"Meta riga {r}: riga vuota → skip"); metaWarn++; continue; }

            var payload = new { id = id, by = by ?? "", info = descr ?? "", rf = refs ?? "", sn = "" };
            object persisted; string upInfo;
            bool ok = MetadataRepositoryCompatExtensions.TryUpsert(repo, payload, out persisted, out upInfo);
            if (ok)
            {
              metaOk++;
              int pid = 0; ReadIf(persisted, "Id", ref pid);
              if (pid > maxMetaIdSeen) maxMetaIdSeen = pid;
            }
            else
            { metaErr++; LOG("ERROR", $"Meta riga {r}: import fallito ({upInfo})"); }
          }
        }

        // ----- VARIABILI -----
        var wsVar = FindSheet(wb, "ProgesiVariables", "Variables");
        if (wsVar == null)
        {
          string m = "Sheet 'ProgesiVariables' non trovato.";
          if (strict) { LOG("ERROR", m); throw new InvalidOperationException(m); }
          else { LOG("WARN", m); }
        }
        else
        {
          var hVar = BuildHeaderMap(wsVar, out int firstRow, out int lastRow);
          var reqVar = new[] { "NAME", "VALUE" };
          var missVar = MissingHeaders(hVar, reqVar);
          if (missVar.Count > 0)
          {
            string m = "Header mancanti in ProgesiVariables: " + string.Join(",", missVar);
            if (strict) { LOG("ERROR", m); throw new InvalidOperationException(m); }
            else { LOG("WARN", m); }
          }

          for (int r = firstRow + 1; r <= lastRow; r++)
          {
            varRows++;
            string name = ReadCell(wsVar, r, hVar, "NAME");
            string value = ReadCell(wsVar, r, hVar, "VALUE");
            string depS = ReadCell(wsVar, r, hVar, "DEPENDS");
            string assS = ReadCell(wsVar, r, hVar, "ASSUMPTION");
            int id = SafeInt(ReadCell(wsVar, r, hVar, "ID"));
            int metaId = SafeInt(ReadCell(wsVar, r, hVar, "METAID"));

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(value))
            { LOG("WARN", $"Var riga {r}: riga vuota → skip"); varWarn++; continue; }

            int[] depends = ParseDepends(depS);
            bool ass = SafeBool(assS);

            var payload = new
            {
              id = id,
              name = name ?? "",
              value = value ?? "",
              unit = "",
              by = "", // non persistito lato Rhino
              isAssumption = ass,
              mid = metaId > 0 ? metaId.ToString(CultureInfo.InvariantCulture) : "",
              depends = depends
            };

            object persisted; string upInfo;
            bool ok = MetadataRepositoryCompatExtensions.TryUpsertVariable(repo, payload, out persisted, out upInfo);
            if (ok)
            {
              varOk++;
              int pid = 0; ReadIf(persisted, "Id", ref pid);
              if (pid > maxVarIdSeen) maxVarIdSeen = pid;
            }
            else
            { varErr++; LOG("ERROR", $"Var riga {r}: import fallito ({upInfo})"); }
          }
        }
      }

      // sincronizza i contatori __next__
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
      if (maxMetaIdSeen > 0) table.SetString("Progesi.Meta", "__next__", (maxMetaIdSeen + 1).ToString(CultureInfo.InvariantCulture));
      if (maxVarIdSeen > 0) table.SetString("Progesi.Var", "__next__", (maxVarIdSeen + 1).ToString(CultureInfo.InvariantCulture));

      // scrivi log
      string logPath = p + ".import.log.txt";
      try { File.WriteAllLines(logPath, log, Encoding.UTF8); } catch { logPath = ""; }

      string info = string.Format(CultureInfo.InvariantCulture,
        "OK ImportExcel ← {0} | Meta {1}/{2} (warn:{3}, err:{4}) | Vars {5}/{6} (warn:{7}, err:{8}) | Log: {9}",
        p, metaOk, metaRows, metaWarn, metaErr, varOk, varRows, varWarn, varErr, (string.IsNullOrWhiteSpace(logPath) ? "-" : logPath));

      return (p, logPath, info);
    }

    // ===== Helpers Import =====
    private static IXLWorksheet FindSheet(XLWorkbook wb, params string[] names)
    {
      foreach (var n in names) if (wb.TryGetWorksheet(n, out var ws)) return ws;
      foreach (var ws in wb.Worksheets)
        foreach (var n in names)
          if (string.Equals(ws.Name, n, StringComparison.OrdinalIgnoreCase)) return ws;
      return null;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws, out int firstRow, out int lastRow)
    {
      var used = ws.RangeUsed();
      if (used == null) { firstRow = 1; lastRow = 0; return new Dictionary<string, int>(); }
      firstRow = used.RangeAddress.FirstAddress.RowNumber;
      lastRow = used.RangeAddress.LastAddress.RowNumber;

      var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var header = ws.Row(firstRow);
      foreach (var cell in header.CellsUsed())
      {
        var name = NormalizeHeader(cell.GetString());
        if (!string.IsNullOrWhiteSpace(name))
        {
          if (!map.ContainsKey(name)) map.Add(name, cell.Address.ColumnNumber);
        }
      }
      return map;
    }

    private static string NormalizeHeader(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return "";
      var up = new string(s.Trim().ToUpperInvariant().ToCharArray()
                .Where(ch => char.IsLetterOrDigit(ch)).ToArray());
      return up;
    }

    private static List<string> MissingHeaders(Dictionary<string, int> map, IEnumerable<string> required)
    {
      var miss = new List<string>();
      foreach (var r in required)
        if (!map.ContainsKey(r)) miss.Add(r);
      return miss;
    }

    private static string ReadCell(IXLWorksheet ws, int row, Dictionary<string, int> map, string key)
    {
      if (!map.TryGetValue(key, out int col)) return "";
      return ws.Cell(row, col).GetString() ?? "";
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
