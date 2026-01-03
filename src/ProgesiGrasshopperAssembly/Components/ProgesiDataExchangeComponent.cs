// -----------------------------------------------------------------------------
// File   : ProgesiDataExchangeComponent.cs
// Scope  : ProgesiGrasshopperAssembly / Components
// Desc   : DataEx (Export/Import Excel) con validazione & log – RHINO-only
// Target : .NET Framework 4.8 + Grasshopper + ClosedXML (>= 0.102) + Newtonsoft.Json
// -----------------------------------------------------------------------------
#nullable disable
using ClosedXML.Excel;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Progesi.Core.Variables;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure; // ServiceHub, ProgesiIcons, MetadataRepositoryCompatExtensions
using ProgesiRepositories.Rhino;
using Rhino;
using Rhino.DocObjects.Tables;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;



namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiDataExchangeComponent : GH_Component
  {
    // static ctor – eseguito una volta quando GH carica l'assembly
  

    public ProgesiDataExchangeComponent()
      : base("ProgesiData-Excel", "DataEx",
             "Importa/Esporta dati Progesi (Rhino StringTable ↔ Excel) con dedupe e validazione.",
             "Progesi", "Data")
    { }


    public override Guid ComponentGuid => new Guid("E7A9D2E5-4E28-4B60-9E8F-41D92A7F5E11");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    // IN: 0 Run, 1 Action, 2 Path, 3 Overwrite, 4 Mode, 5 FailOnError, 6 MaxErrors, 7 Map(JSON)
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (TRUE avvia l’azione).", GH_ParamAccess.item, false);
      p.AddTextParameter("Action", "Act", "ExportExcel | ImportExcel | ExportSqlite | ImportSqlite | ExportEf | ImportEf", GH_ParamAccess.item, "ExportExcel");
      p.AddTextParameter("Path", "Path", "Percorso file .xlsx (Export: dest., Import: src).", GH_ParamAccess.item, "");
      p.AddBooleanParameter("Overwrite", "Ovr", "Se true, sovrascrive (Export).", GH_ParamAccess.item, true);

      // Validazione Import
      p.AddTextParameter("Mode", "Mode", "Import: 'Strict' oppure 'Lenient'.", GH_ParamAccess.item, "Lenient");
      p.AddBooleanParameter("FailOnError", "Fail", "Stop import se errori ≥ MaxErr.", GH_ParamAccess.item, false);
      p.AddIntegerParameter("MaxErrors", "MaxErr", "Soglia errori per stop (default 1000).", GH_ParamAccess.item, 1000);
      p.AddTextParameter("Map", "Map", "JSON alias colonne (opzionale).", GH_ParamAccess.item, "");
      p.AddBooleanParameter("DryRun", "Dry", "Se TRUE: valida e logga senza scrivere nel repository.", GH_ParamAccess.item, false);
      // alla fine di RegisterInputParams(...)
      for (int i = 1; i < Params.Input.Count; i++) Params.Input[i].Optional = true; // lascia Run non opzionale
    }

    // OUT: 0 Info, 1 Path/Log, 2 Warn(tree 0=Meta/1=Vars), 3 Errors(tree 0=Meta/1=Vars), 4 Counts(tree)
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Info", "Info", "Esito e riepilogo.", GH_ParamAccess.item);
      p.AddTextParameter("Path", "Path", "Percorso file utilizzato o file di log.", GH_ParamAccess.item);
      p.AddTextParameter("Warn", "Warn", "Avvisi (branch 0=Meta, 1=Vars).", GH_ParamAccess.tree);
      p.AddTextParameter("Errors", "Err", "Errori (branch 0=Meta, 1=Vars).", GH_ParamAccess.tree);
      p.AddTextParameter("Counts", "Counts", "Riepilogo (righe/ok/warn/err).", GH_ParamAccess.tree);
      p.AddIntegerParameter("ErrRC", "ErrRC", "Coordinate errori (branch 0=Meta, 1=Vars; subpath {branch;i} = [row,col]).", GH_ParamAccess.tree);

    }
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false, overwrite = true, fail = false;
      string action = "ExportExcel", path = "", mode = "Lenient", mapJson = "";
      int maxErrors = 1000;
      bool dryRun = false;

      DA.GetData(0, ref run);
      DA.GetData(1, ref action);
      DA.GetData(2, ref path);
      DA.GetData(3, ref overwrite);
      DA.GetData(4, ref mode);
      DA.GetData(5, ref fail);
      DA.GetData(6, ref maxErrors);
      DA.GetData(7, ref mapJson); DA.GetData(8, ref dryRun);
      bool strictMode = string.Equals((mode ?? "").Trim(), "STRICT", StringComparison.OrdinalIgnoreCase);
      string actNorm = (action ?? "").Trim().ToUpperInvariant();

      
      var warnTree = new GH_Structure<GH_String>();
      var errTree = new GH_Structure<GH_String>();
      var counts = new GH_Structure<GH_String>();

      if (!run)
      {
        DA.SetData(0, "Idle");
        DA.SetData(1, "");
        DA.SetDataTree(2, warnTree);
        DA.SetDataTree(3, errTree);
        DA.SetDataTree(4, counts);
        return;
      }

      try
      {


        if (actNorm == "EXPORTEXCEL")
        {
          var (outPath, msg) = ExportExcel(path, overwrite);
          counts.Append(new GH_String("Export: OK"), new GH_Path(0));
          DA.SetData(0, msg);
          DA.SetData(1, outPath ?? "");
          DA.SetDataTree(2, warnTree);
          DA.SetDataTree(3, errTree);
          DA.SetDataTree(4, counts);
          return;
        }
        if (actNorm == "EXPORTSQLITE")
        {
          var (outDb, msg) = ExportSqlite(path, overwrite);
          DA.SetData(0, msg);
          DA.SetData(1, outDb ?? "");
          DA.SetDataTree(2, new GH_Structure<GH_String>());
          DA.SetDataTree(3, new GH_Structure<GH_String>());
          var countsOut = new GH_Structure<GH_String>();
          countsOut.Append(new GH_String("ExportSqlite: OK"), new GH_Path(0));
          DA.SetDataTree(4, countsOut);

          return;
        }
        if (actNorm == "IMPORTSQLITE")
        {
          var (srcDb, logPath, wTree, eTree, cTree, rcTree, info) =
              ImportSqlite(path, strictMode, dryRun);

          DA.SetData(0, info);
          DA.SetData(1, string.IsNullOrWhiteSpace(logPath) ? (srcDb ?? "") : logPath);
          DA.SetDataTree(2, wTree);
          DA.SetDataTree(3, eTree);
          DA.SetDataTree(4, cTree);
          DA.SetDataTree(5, rcTree);       // << ErrRC (row,col)
          return;
        }
        if (actNorm == "IMPORTEXCEL")
        {
          // prima: bool strict = string.Equals(...);
          var (src, logPath, wTree, eTree, cTree, errRcTree, info) =
              ImportExcelValidated(path, strictMode, fail, Math.Max(0, maxErrors), mapJson, dryRun);


          DA.SetData(0, info);
          DA.SetData(1, string.IsNullOrWhiteSpace(logPath) ? (src ?? "") : logPath);
          DA.SetDataTree(2, wTree);
          DA.SetDataTree(3, eTree);
          DA.SetDataTree(4, cTree);
          DA.SetDataTree(5, errRcTree); // nuovo output ErrRC
          return;
        }
        if (actNorm == "EXPORTEF")
        {
          // Politica S2-C/2: nel plug-in GH usiamo sempre SQLite come formato “EF-ready”.
          var (outDb, msg) = ExportSqlite(path, overwrite);
          DA.SetData(0, $"[DB:SQLite] {msg}");
          DA.SetData(1, outDb ?? "");
          DA.SetDataTree(2, new GH_Structure<GH_String>());
          DA.SetDataTree(3, new GH_Structure<GH_String>());
          var countsOut = new GH_Structure<GH_String>();
          countsOut.Append(new GH_String("ExportEf→SQLite: OK"), new GH_Path(0));
          DA.SetDataTree(4, countsOut);
          return;
        }

        if (actNorm == "IMPORTEF")
        {
          // Politica S2-C/2: nel plug-in GH leggiamo sempre il DB SQLite (schema EF-compatibile)
          var (srcDb, logPath, wTree, eTree, cTree, rcTree, info) =
              ImportSqlite(path, strictMode, dryRun);

          DA.SetData(0, $"[DB:SQLite] {info}");
          DA.SetData(1, string.IsNullOrWhiteSpace(logPath) ? (srcDb ?? "") : logPath);
          DA.SetDataTree(2, wTree);
          DA.SetDataTree(3, eTree);
          DA.SetDataTree(4, cTree);
          DA.SetDataTree(5, rcTree);
          return;
        }


        DA.SetData(0, $"Unsupported Action: {action}");
        DA.SetData(1, "");
        DA.SetDataTree(2, warnTree);
        DA.SetDataTree(3, errTree);
        DA.SetDataTree(4, counts);
      }
      catch (Exception ex)
      {
        // Fallback automatico quando EF fallisce: usiamo la pipeline SQLite stabile
        try
        {
          if (actNorm == "EXPORTEF")
          {
            var (outPath, msg) = ExportSqlite(path, overwrite);
            DA.SetData(0, $"[EF fallback] {msg} — WHY: {ex.Message}");
            DA.SetData(1, outPath ?? "");
            DA.SetDataTree(2, new GH_Structure<GH_String>());
            DA.SetDataTree(3, new GH_Structure<GH_String>());
            var c = new GH_Structure<GH_String>(); c.Append(new GH_String("ExportEf→Sqlite fallback"), new GH_Path(0));
            DA.SetDataTree(4, c);
            return;
          }
          if (actNorm == "IMPORTEF")
          {
            var (srcDb, logPath, wTree, eTree, cTree, rcTree, info) = ImportSqlite(path, strictMode, dryRun);
            DA.SetData(0, $"[EF fallback] {info} — WHY: {ex.Message}");
            DA.SetData(1, string.IsNullOrWhiteSpace(logPath) ? (srcDb ?? "") : logPath);
            DA.SetDataTree(2, wTree);
            DA.SetDataTree(3, eTree);
            DA.SetDataTree(4, cTree);
            DA.SetDataTree(5, rcTree);
            return;
          }
        }
        catch { /* se anche il fallback qui dentro fallisse, cadiamo nel default sotto */ }

        // default: errore “pieno” (azioni diverse da EF)
        DA.SetData(0, "Error: " + ex.Message);
        DA.SetData(1, "");
        DA.SetDataTree(2, warnTree);
        DA.SetDataTree(3, errTree);
        DA.SetDataTree(4, counts);
      }



    }

    // ------------------------------ EXPORT --------------------------------
    private static (string path, string info) ExportExcel(string inPath, bool overwrite)
    {
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      StringTable table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");

      string p = NormalizeExportPath(inPath);
      if (File.Exists(p))
      {
        if (!overwrite) throw new InvalidOperationException("File already exists: " + p);
        try { using (var _ = File.Open(p, FileMode.Open, FileAccess.Write, FileShare.None)) { } } catch { }
      }

      var vars = ReadAllVarsFromTable(table);
      var metas = ReadAllMetasFromTable(table);
      var clusters = ReadAllClustersFromTable(table);


      using (var wb = new XLWorkbook())
      {
        // Variables
        var wsV = wb.Worksheets.Add("ProgesiVariables");
        wsV.Cell(1, 1).Value = "Id";
        wsV.Cell(1, 2).Value = "Hash";
        wsV.Cell(1, 3).Value = "Name";
        wsV.Cell(1, 4).Value = "Value";
        wsV.Cell(1, 5).Value = "ValC";
        wsV.Cell(1, 6).Value = "MetaId";
        wsV.Cell(1, 7).Value = "Depends";
        wsV.Cell(1, 8).Value = "Assumption";
        int r = 2;
        foreach (var v in vars)
        {
          wsV.Cell(r, 1).Value = v.Id;
          wsV.Cell(r, 2).Value = v.Hash ?? "";
          wsV.Cell(r, 3).Value = v.Name ?? "";
          wsV.Cell(r, 4).Value = v.Value ?? "";
          wsV.Cell(r, 5).Value = v.ValC ?? "";
          wsV.Cell(r, 6).Value = v.MetaId;
          wsV.Cell(r, 7).Value = (v.Depends != null && v.Depends.Length > 0) ? string.Join(",", v.Depends) : "";
          wsV.Cell(r, 8).Value = v.Assumption ? 1 : 0;
          r++;
        }
        wsV.Columns().AdjustToContents();

        // Metadata
        var wsM = wb.Worksheets.Add("ProgesiMetadata");
        wsM.Cell(1, 1).Value = "Id";
        wsM.Cell(1, 2).Value = "Hash";
        wsM.Cell(1, 3).Value = "By";
        wsM.Cell(1, 4).Value = "Description";
        wsM.Cell(1, 5).Value = "Refs";
        wsM.Cell(1, 6).Value = "LM";
        int r2 = 2;
        foreach (var m in metas)
        {
          wsM.Cell(r2, 1).Value = m.Id;
          wsM.Cell(r2, 2).Value = m.Hash ?? "";
          wsM.Cell(r2, 3).Value = m.By ?? "";
          wsM.Cell(r2, 4).Value = m.Description ?? "";
          wsM.Cell(r2, 5).Value = (m.Refs != null && m.Refs.Length > 0) ? string.Join("|", m.Refs) : "";
          wsM.Cell(r2, 6).Value = m.LM ?? "";
          r2++;
        }
        wsM.Columns().AdjustToContents();
        WriteClustersSheet(wb, clusters);
        wb.SaveAs(p);
      }

      string info = $"OK ExportExcel → {p} (Vars:{vars.Length}, Meta:{metas.Length}, Clusters:{clusters.Length})";

      return (p, info);
    }
    private static string NormalizeExportPath(string inPath)
    {
      string p = (inPath ?? "").Trim();
      if (string.IsNullOrEmpty(p))
      {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (string.IsNullOrEmpty(home)) home = AppDomain.CurrentDomain.BaseDirectory;
        p = Path.Combine(home, "Progesi_Export.xlsx");
      }
      if (Directory.Exists(p)) p = Path.Combine(p, "Progesi_Export.xlsx");
      if (Path.GetExtension(p).Length == 0) p = p.TrimEnd(' ', '.') + ".xlsx";
      return p;
    }
        private static void WriteClustersSheet(XLWorkbook wb, ClusterRow[] clusters)
    {
      var ws = wb.Worksheets.Add("ProgesiClusters");

      ws.Cell(1, 1).Value = "Id";
      ws.Cell(1, 2).Value = "Hash";
      ws.Cell(1, 3).Value = "Name";
      ws.Cell(1, 4).Value = "Description";
      ws.Cell(1, 5).Value = "VariableIds"; // CSV: "1,2,3"

      int r = 2;
      foreach (var c in clusters)
      {
        ws.Cell(r, 1).Value = c.Id;
        ws.Cell(r, 2).Value = c.Hash;
        ws.Cell(r, 3).Value = c.Name;
        ws.Cell(r, 4).Value = c.Description;
        ws.Cell(r, 5).Value = string.Join(",", c.VariableIds ?? Array.Empty<int>());
        r++;
      }

      ws.Columns().AdjustToContents();
    }

    // ------------------------------ IMPORT --------------------------------
    private static (string srcPath,
                    string logPath,
                    GH_Structure<GH_String> warnOut,
                    GH_Structure<GH_String> errOut,
                    GH_Structure<GH_String> countsOut,
                    GH_Structure<GH_Integer> errRcOut,
                    string info)
    ImportExcelValidated(string inPath, bool strict, bool failOnError, int maxErrors, string mapJson, bool dryRun)
    {
      // --- setup ---
      string p = (inPath ?? "").Trim();
      if (string.IsNullOrEmpty(p)) throw new ArgumentException("Path .xlsx not specified.");
      if (!File.Exists(p)) throw new FileNotFoundException("File not found", p);

      var warnTree = new GH_Structure<GH_String>();
      var errTree = new GH_Structure<GH_String>();
      var counts = new GH_Structure<GH_String>();
      var errRC = new GH_Structure<GH_Integer>(); // {branch; i}: [row, col]

      var logLines = new List<string>();
      Action<string, string> LOG = (lvl, msg) => logLines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {lvl}: {msg}");
      Action<int, string> WARN = (br, msg) => { warnTree.Append(new GH_String(msg), new GH_Path(br)); LOG("WARN", msg); };
      Action<int, string> ERR = (br, msg) => { errTree.Append(new GH_String(msg), new GH_Path(br)); LOG("ERROR", msg); };
      Action<int, int, int> AddErrRC = (br, row, col) =>
      {
        // un solo branch per sezione (0=Meta, 1=Vars), due interi consecutivi per ogni errore [row,col]
        var path = new GH_Path(br);
        errRC.Append(new GH_Integer(row), path);
        errRC.Append(new GH_Integer(col), path);
      };


      object repo; string hubInfo;
      if (!ServiceHub.TryGetMetadataRepository(out repo, out hubInfo))
        throw new InvalidOperationException("RHINO repository not available.");

      // alias
      var (varAliases, metaAliases) = BuildAliasMaps(mapJson);

      // limiti/regex
      const int NAME_MAX = 128;
      const int DESC_MAX = 512;
      Func<string, bool> IsPrintable = s => string.IsNullOrEmpty(s) || s.All(ch => ch >= 32 && ch != 127);
      Func<string, bool> IsHttpUrl = s => Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");
      Func<string, bool> IsAbsPath = s => !string.IsNullOrEmpty(s) && Path.IsPathRooted(s);

      // contatori
      int metaRows = 0, metaOk = 0, metaWarn = 0, metaErr = 0, maxMetaId = 0;
      int varRows = 0, varOk = 0, varWarn = 0, varErr = 0, maxVarId = 0;

      using (var wb = new XLWorkbook(p))
      {
        // ========== METADATA ==========
        var wsM = TryGetWorksheet(wb, "ProgesiMetadata", "Metadata");
        bool metaHeaderError = false;
        Dictionary<string, int> mapM = null;
        int r0M = 1, rNM = 0;

        if (wsM == null)
        {
          string m = "Sheet 'ProgesiMetadata' not found.";
          if (strict) { ERR(0, m); metaHeaderError = true; }
          else { WARN(0, m); }
        }
        else
        {
          var headerM = BuildHeaderMap(wsM, out r0M, out rNM);
          mapM = ResolveColumns(headerM, metaAliases);
          var missingMeta = MissingRequired(mapM, new[] { "BY", "DESCRIPTION" });
          if (missingMeta.Count > 0)
          {
            string m = "Missing headers (Meta): " + string.Join(",", missingMeta);
            if (strict) { ERR(0, m); metaHeaderError = true; AddErrRC(0, r0M, -1); }
            else { WARN(0, m); metaWarn += missingMeta.Count; }
          }
        }

        if (!metaHeaderError && wsM != null)
        {
          for (int r = r0M + 1; r <= rNM; r++)
          {
            string by = ReadCell(wsM, r, mapM, "BY");
            string desc = ReadCell(wsM, r, mapM, "DESCRIPTION");
            string refs = ReadCell(wsM, r, mapM, "REFS");
            int id = ToInt(ReadCell(wsM, r, mapM, "ID"));

            if (IsBlank(by) && IsBlank(desc) && IsBlank(refs))
            { WARN(0, $"[Meta R{r}] empty row → skip"); metaWarn++; metaRows++; continue; }

            // controlli extra
            if (by.Length > NAME_MAX || !IsPrintable(by))
            { var msg = $"[Meta R{r}] BY invalid (len/charset)"; (strict ? ERR : WARN)(0, msg); AddErrRC(0, r, mapM.TryGetValue("BY", out var c) ? c : 0); if (strict) { metaErr++; continue; } else { metaWarn++; } }

            if (desc.Length > DESC_MAX || !IsPrintable(desc))
            { var msg = $"[Meta R{r}] DESCRIPTION invalid (len/charset)"; (strict ? ERR : WARN)(0, msg); AddErrRC(0, r, mapM.TryGetValue("DESCRIPTION", out var c) ? c : 0); if (strict) { metaErr++; continue; } else { metaWarn++; } }

            if (!string.IsNullOrWhiteSpace(refs))
            {
              foreach (var token in refs.Split('|'))
              {
                var s = token?.Trim(); if (string.IsNullOrEmpty(s)) continue;
                if (!(IsHttpUrl(s) || IsAbsPath(s)))
                { var msg = $"[Meta R{r}] REF invalid: {s}"; (strict ? ERR : WARN)(0, msg); AddErrRC(0, r, mapM.TryGetValue("REFS", out var c) ? c : 0); if (strict) { metaErr++; goto NEXT_META; } else { metaWarn++; } }
              }
            }

            if (!dryRun)
            {
              var payload = new { id = id, by = by ?? "", info = desc ?? "", rf = refs ?? "", sn = "" };
              object persisted; string upInfo;
              bool ok = MetadataRepositoryCompatExtensions.TryUpsert(repo, payload, out persisted, out upInfo);
              if (!ok)
              { var msg = $"[Meta R{r}] import failed: {upInfo ?? "unknown"}"; ERR(0, msg); AddErrRC(0, r, 0); metaErr++; metaRows++; continue; }
              int pid = 0; ReadPersistedId(persisted, ref pid);
              if (pid > 0 && pid > maxMetaId) maxMetaId = pid;
            }

            metaOk++; metaRows++;
            NEXT_META:;
          }
        }

        // ========== VARIABLES ==========
        var wsV = TryGetWorksheet(wb, "ProgesiVariables", "Variables");
        bool varHeaderError = false;
        Dictionary<string, int> mapV = null;
        int r0V = 1, rNV = 0;

        if (wsV == null)
        {
          string m = "Sheet 'ProgesiVariables' not found.";
          if (strict) { ERR(1, m); varHeaderError = true; }
          else { WARN(1, m); }
        }
        else
        {
          var headerV = BuildHeaderMap(wsV, out r0V, out rNV);
          mapV = ResolveColumns(headerV, varAliases);
          var missingVar = MissingRequired(mapV, new[] { "NAME", "VALUE" });
          if (missingVar.Count > 0)
          {
            string m = "Missing headers (Vars): " + string.Join(",", missingVar);
            if (strict) { ERR(1, m); varHeaderError = true; AddErrRC(1, r0V, -1); }
            else { WARN(1, m); varWarn += missingVar.Count; }
          }
        }

        if (!varHeaderError && wsV != null)
        {
          for (int r = r0V + 1; r <= rNV; r++)
          {
            string name = ReadCell(wsV, r, mapV, "NAME");
            string value = ReadCell(wsV, r, mapV, "VALUE");
            string deps = ReadCell(wsV, r, mapV, "DEPENDS");
            string asS = ReadCell(wsV, r, mapV, "ASSUMPTION");
            int id = ToInt(ReadCell(wsV, r, mapV, "ID"));
            int mid = ToInt(ReadCell(wsV, r, mapV, "METAID"));

            if (IsBlank(name) && IsBlank(value) && IsBlank(deps) && IsBlank(asS))
            { WARN(1, $"[Var R{r}] empty row → skip"); varWarn++; varRows++; continue; }

            // extra checks
            if (name.Length > NAME_MAX || !IsPrintable(name))
            { var msg = $"[Var R{r}] NAME invalid (len/charset)"; (strict ? ERR : WARN)(1, msg); AddErrRC(1, r, mapV.TryGetValue("NAME", out var c) ? c : 0); if (strict) { varErr++; continue; } else { varWarn++; } }

            // MetaId existence (se fornito)
            if (mid > 0)
            {
              object dummy; string lookupInfo;
              bool okMeta = MetadataRepositoryCompatExtensions.TryGetByHashThenId(repo, "", mid, out dummy, out lookupInfo);

              if (!okMeta)
              { var msg = $"[Var R{r}] METAID not found: {mid}"; (strict ? ERR : WARN)(1, msg); AddErrRC(1, r, mapV.TryGetValue("METAID", out var c) ? c : 0); if (strict) { varErr++; continue; } else { varWarn++; mid = 0; } }
            }

            int[] depArr = ParseDepends(deps);
            bool ass = ToBool(asS);

            if (!dryRun)
            {
              var payload = new
              {
                id = id,
                name = name ?? "",
                value = value ?? "",
                unit = "",
                by = "",
                isAssumption = ass,
                mid = (mid > 0 ? mid.ToString(CultureInfo.InvariantCulture) : ""),
                depends = (object)depArr
              };

              object persisted; string upInfo;
              bool ok = MetadataRepositoryCompatExtensions.TryUpsertVariable(repo, payload, out persisted, out upInfo);
              if (!ok)
              { var msg = $"[Var R{r}] import failed: {upInfo ?? "unknown"}"; ERR(1, msg); AddErrRC(1, r, 0); varErr++; varRows++; continue; }
              int pid = 0; ReadPersistedId(persisted, ref pid);
              if (pid > 0 && pid > maxVarId) maxVarId = pid;
            }

            varOk++; varRows++;
          }
        }
      } // using wb
        // ===== CLUSTERS (Excel -> Rhino StringTable) =====
      int clusterRows = 0, clusterOk = 0, clusterWarn = 0, clusterErr = 0;

      try
      {
        // Import clusters solo se NON dryRun
        if (!dryRun)
        {
          var docC = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
          var tableC = docC.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");

          string res = ImportClustersFromExcel(p, tableC, msg => LOG("WARN", msg), strict);
          LOG("INFO", res);

          clusterRows = ExtractInt(res, "rows=");
          clusterOk = ExtractInt(res, "imported=");
          clusterWarn = ExtractInt(res, "skipped=");

        }
        else
        {
          WARN(0, "[Clusters] DryRun: import cluster skipped.");
          clusterWarn++;
        }
      }
      catch (Exception ex)
      {
        ERR(0, "[Clusters] Import failed: " + ex.Message);
        clusterErr++;
      }

      // Aggiorna contatori solo se NON è DryRun
      if (!dryRun)
      {
        var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
        StringTable table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
        int curMetaNext = ReadCounter(table, "Progesi.Meta");
        int curVarNext = ReadCounter(table, "Progesi.Var");
        if (maxMetaId > 0) table.SetString("Progesi.Meta", "__next__", Math.Max(curMetaNext, maxMetaId + 1).ToString(CultureInfo.InvariantCulture));
        if (maxVarId > 0) table.SetString("Progesi.Var", "__next__", Math.Max(curVarNext, maxVarId + 1).ToString(CultureInfo.InvariantCulture));
      }

      // log
      string logPath = p + ".import.log.txt";
      try { File.WriteAllLines(logPath, logLines, Encoding.UTF8); } catch { logPath = ""; }

      // counts
      counts.Append(new GH_String($"Meta rows={metaRows} ok={metaOk} warn={metaWarn} err={metaErr}"), new GH_Path(0));
      counts.Append(new GH_String($"Vars rows={varRows} ok={varOk} warn={varWarn} err={varErr}"), new GH_Path(1));
      counts.Append(new GH_String($"Clusters rows={clusterRows} ok={clusterOk} warn={clusterWarn} err={clusterErr}"), new GH_Path(2));

      string prefix = dryRun ? "PREVIEW " : "OK ";
      string info = $"{prefix}ImportExcel ← {p} | Meta {metaOk}/{metaRows} (warn:{metaWarn}, err:{metaErr}) | " +
                    $"Vars {varOk}/{varRows} (warn:{varWarn}, err:{varErr}) | Clusters {clusterOk}/{clusterRows} warn={clusterWarn} err={clusterErr}   | Log: {(string.IsNullOrWhiteSpace(logPath) ? "-" : logPath)}";

      return (p, logPath, warnTree, errTree, counts, errRC, info);
    }

    // =========================== SQLITE – EXPORT ===========================
    private static bool TryRunEfTool(string command, string dbPath, bool strict, bool dryRun, out string std, out string err)
    {
      std = ""; err = "";
      try
      {
        // cerchiamo l'exe prima accanto alla .gha, poi nella cartella GH Libraries
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var exe = Path.Combine(baseDir, "Progesi.EF.Tool.exe");
        if (!File.Exists(exe))
          exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grasshopper", "Libraries", "Progesi.EF.Tool.exe");
        if (!File.Exists(exe)) { err = "EF tool not found"; return false; }

        var args = new List<string>();
        if (string.Equals(command, "export", StringComparison.OrdinalIgnoreCase))
        {
          args.Add("export"); args.Add($"\"{dbPath}\"");
        }
        else
        {
          args.Add("import"); args.Add($"\"{dbPath}\"");
          if (strict) args.Add("--strict");
          if (dryRun) args.Add("--dry-run");
        }

        var psi = new ProcessStartInfo(exe, string.Join(" ", args))
        {
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };
        using (var p = Process.Start(psi))
        {
          std = p.StandardOutput.ReadToEnd();
          err = p.StandardError.ReadToEnd();
          p.WaitForExit();
          return p.ExitCode == 0;
        }
      }
      catch (Exception ex) { err = ex.Message; return false; }
    }

    // in cima al file, tra gli using:

    // ...dentro la classe ProgesiDataExchangeComponent sostituisci l'intero metodo ExportSqlite con questo:
    private static (string path, string info) ExportSqlite(string inPath, bool overwrite)
    {
      // 0) Sorgente: RHINO StringTable
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");

      // 1) Leggi tutto subito (una sola passata)
      var metas = ReadAllMetasFromTable(table);   // MetaRow[]
      var vars = ReadAllVarsFromTable(table);    // VarRow[]
      var metaIdsPresent = new HashSet<int>(metas.Select(m => m.Id));

      // 2) Prepara percorso destinazione
      string p = (inPath ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(p))                       // <-- fix qui
      {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (string.IsNullOrEmpty(desktop)) desktop = AppDomain.CurrentDomain.BaseDirectory;
        p = Path.Combine(desktop, "Progesi_Export.db");
      }
      if (Directory.Exists(p))
        p = Path.Combine(p, "Progesi_Export.db");

      if (File.Exists(p))
      {
        if (!overwrite)
          throw new InvalidOperationException("Il file esiste già: " + p);

        // prova a rimuoverlo; se è lockato, genera un nome alternativo con timestamp
        try
        {
          using (var fs = File.Open(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
          File.Delete(p);
        }
        catch
        {
          var dir = Path.GetDirectoryName(p) ?? ".";
          var name = Path.GetFileNameWithoutExtension(p);
          var ext = Path.GetExtension(p);
          var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
          p = Path.Combine(dir, $"{name}_{stamp}{ext}");
        }
      }

      // 3) Scrittura in un'unica transazione
      using (var cn = new SQLiteConnection($@"Data Source={p};Version=3;"))
      {
        cn.Open();

        using (var tx = cn.BeginTransaction())
        using (var cmd = new SQLiteCommand(cn))
        {
          // abilita FK e crea schema senza UNIQUE su Hash
          cmd.CommandText = "PRAGMA foreign_keys=ON;";
          cmd.ExecuteNonQuery();

          cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Metadata (
  Id           INTEGER PRIMARY KEY,
  Hash         TEXT NOT NULL,
  By           TEXT,
  Description  TEXT,
  LM           TEXT
);
CREATE TABLE IF NOT EXISTS Variables (
  Id           INTEGER PRIMARY KEY,
  Hash         TEXT NOT NULL,
  Name         TEXT NOT NULL,
  Value        TEXT,
  ValC         TEXT,
  MetaId       INTEGER NULL,
  Assumption   INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY (MetaId) REFERENCES Metadata(Id) ON DELETE SET NULL
);
CREATE TABLE IF NOT EXISTS Refs (
  MetaId       INTEGER NOT NULL,
  Ref          TEXT NOT NULL,
  PRIMARY KEY (MetaId, Ref),
  FOREIGN KEY (MetaId) REFERENCES Metadata(Id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS VariableDepends (
  VarId        INTEGER NOT NULL,
  DepId        INTEGER NOT NULL,
  PRIMARY KEY (VarId, DepId),
  FOREIGN KEY (VarId) REFERENCES Variables(Id) ON DELETE CASCADE
);";
          cmd.ExecuteNonQuery();

          // 3.1) Metadata
          cmd.CommandText = "INSERT OR REPLACE INTO Metadata (Id,Hash,By,Description,LM) VALUES (@id,@hash,@by,@descr,@lm)";
          var pId = new SQLiteParameter("@id");
          var pHash = new SQLiteParameter("@hash");
          var pBy = new SQLiteParameter("@by");
          var pDescr = new SQLiteParameter("@descr");
          var pLM = new SQLiteParameter("@lm");
          cmd.Parameters.AddRange(new[] { pId, pHash, pBy, pDescr, pLM });

          foreach (var m in metas)
          {
            pId.Value = m.Id;
            pHash.Value = m.Hash ?? string.Empty;
            pBy.Value = m.By ?? string.Empty;
            pDescr.Value = m.Description ?? string.Empty;
            pLM.Value = m.LM ?? string.Empty;
            cmd.ExecuteNonQuery();
          }

          // 3.2) Variabili (MetaId → NULL se non presente tra i metadata)
          cmd.Parameters.Clear();
          cmd.CommandText = "INSERT OR REPLACE INTO Variables (Id,Hash,Name,Value,ValC,MetaId,Assumption) VALUES (@id,@hash,@name,@value,@valc,@mid,@ass)";
          var vId = new SQLiteParameter("@id");
          var vHash = new SQLiteParameter("@hash");
          var vName = new SQLiteParameter("@name");
          var vVal = new SQLiteParameter("@value");
          var vValC = new SQLiteParameter("@valc");
          var vMid = new SQLiteParameter("@mid");
          var vAss = new SQLiteParameter("@ass");
          cmd.Parameters.AddRange(new[] { vId, vHash, vName, vVal, vValC, vMid, vAss });

          foreach (var v in vars)
          {
            vId.Value = v.Id;
            vHash.Value = v.Hash ?? string.Empty;
            vName.Value = v.Name ?? string.Empty;
            vVal.Value = v.Value ?? string.Empty;
            vValC.Value = v.ValC ?? string.Empty;
            vMid.Value = (v.MetaId > 0 && metaIdsPresent.Contains(v.MetaId)) ? (object)v.MetaId : (object)DBNull.Value;
            vAss.Value = v.Assumption ? 1 : 0;
            cmd.ExecuteNonQuery();
          }

          // 3.3) Refs
          cmd.Parameters.Clear();
          cmd.CommandText = "INSERT OR REPLACE INTO Refs (MetaId,Ref) VALUES (@mid,@ref)";
          var rMid = new SQLiteParameter("@mid");
          var rRef = new SQLiteParameter("@ref");
          cmd.Parameters.AddRange(new[] { rMid, rRef });

          foreach (var m in metas)
          {
            if (m.Refs == null || m.Refs.Length == 0) continue;
            if (!metaIdsPresent.Contains(m.Id)) continue;

            foreach (var rf in m.Refs)
            {
              rMid.Value = m.Id;
              rRef.Value = rf ?? string.Empty;
              cmd.ExecuteNonQuery();
            }
          }

          // 3.4) VariableDepends
          cmd.Parameters.Clear();
          cmd.CommandText = "INSERT OR REPLACE INTO VariableDepends (VarId,DepId) VALUES (@vid,@did)";
          var dVid = new SQLiteParameter("@vid");
          var dDid = new SQLiteParameter("@did");
          cmd.Parameters.AddRange(new[] { dVid, dDid });

          var varIds = new HashSet<int>(vars.Select(v => v.Id));
          foreach (var v in vars)
          {
            if (v.Depends == null || v.Depends.Length == 0) continue;
            foreach (var dep in v.Depends)
            {
              if (!varIds.Contains(dep)) continue;
              dVid.Value = v.Id;
              dDid.Value = dep;
              cmd.ExecuteNonQuery();
            }
          }

          tx.Commit();
        }
      }

      // 4) Riepilogo
      var info = string.Format(                                  // <-- fix Count/Length e firma di Format
          CultureInfo.InvariantCulture,
          "OK ExportSqlite → {0} (Meta:{1}, Vars:{2})",
          p, metas.Length, vars.Length);

      return (p, info);
    }

    // =========================== SQLITE – IMPORT ===========================
    private static (string srcDb,
                 string logPath,
                 GH_Structure<GH_String> warnOut,
                 GH_Structure<GH_String> errOut,
                 GH_Structure<GH_String> countsOut,
                 GH_Structure<GH_Integer> errRcOut,
                 string info)
 ImportSqlite(string inDbPath, bool strict, bool dryRun)
    {
      string db = (inDbPath ?? "").Trim();
      if (string.IsNullOrEmpty(db)) throw new ArgumentException("SQLite path not specified.");
      if (!File.Exists(db)) throw new FileNotFoundException("SQLite file not found.", db);

      var warnTree = new GH_Structure<GH_String>();
      var errTree = new GH_Structure<GH_String>();
      var counts = new GH_Structure<GH_String>();
      var errRC = new GH_Structure<GH_Integer>(); // {branch} → coppie [row,col]

      var logLines = new List<string>();
      Action<string, string> LOG = (lvl, msg) => logLines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {lvl}: {msg}");
      Action<int, string> WARN = (br, msg) => { warnTree.Append(new GH_String(msg), new GH_Path(br)); LOG("WARN", msg); };
      Action<int, string> ERR = (br, msg) => { errTree.Append(new GH_String(msg), new GH_Path(br)); LOG("ERROR", msg); };
      Action<int, int, int> AddErrRC = (br, row, col) =>
      {
        var path = new GH_Path(br);
        errRC.Append(new GH_Integer(row), path);
        errRC.Append(new GH_Integer(col), path);
      };

      object repo; string hub;
      if (!ServiceHub.TryGetMetadataRepository(out repo, out hub))
        throw new InvalidOperationException("RHINO repository not available.");

      // controlli extra coerenti con Excel
      const int NAME_MAX = 128;
      const int DESC_MAX = 512;
      Func<string, bool> IsPrintable = s => string.IsNullOrEmpty(s) || s.All(ch => ch >= 32 && ch != 127);
      Func<string, bool> IsHttpUrl = s => Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");
      Func<string, bool> IsAbsPath = s => !string.IsNullOrEmpty(s) && Path.IsPathRooted(s);

      int metaRows = 0, metaOk = 0, metaWarn = 0, metaErr = 0;
      int varRows = 0, varOk = 0, varWarn = 0, varErr = 0;

      using (var cn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db};Mode=ReadOnly"))
      {
        cn.Open();

        // Verifica schema
        Func<string, bool> HasTable = name =>
        {
          using (var chk = cn.CreateCommand())
          {
            chk.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@n";
            chk.Parameters.AddWithValue("@n", name);
            var r = chk.ExecuteScalar();
            return r != null && r != DBNull.Value;
          }
        };

        if (!HasTable("Metadata") || !HasTable("Variables"))
        {
          ERR(0, "SQLite schema not found (Metadata/Variables). Did export fail previously?");
          string bad = db + ".import.log.txt";
          try { File.WriteAllLines(bad, logLines, Encoding.UTF8); } catch { bad = ""; }
          return (db, bad, warnTree, errTree, counts, errRC, "Error: missing tables in SQLite database.");
        }

        // ===== METADATA =====
        using (var cmd = cn.CreateCommand())
        {
          cmd.CommandText = "SELECT Id, By, Description, LM FROM Metadata ORDER BY Id";
          using (var rd = cmd.ExecuteReader())
          {
            int row = 0; // 1-based “visuale”
            while (rd.Read())
            {
              row++; metaRows++;
              int id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
              string by = rd.IsDBNull(1) ? "" : rd.GetString(1);
              string ds = rd.IsDBNull(2) ? "" : rd.GetString(2);

              // controlli extra
              if (by.Length > NAME_MAX || !IsPrintable(by))
              { var msg = $"[Meta R{row}] BY invalid (len/charset)"; (strict ? ERR : WARN)(0, msg); AddErrRC(0, row, 2); if (strict) { metaErr++; continue; } else { metaWarn++; } }

              if (ds.Length > DESC_MAX || !IsPrintable(ds))
              { var msg = $"[Meta R{row}] DESCRIPTION invalid (len/charset)"; (strict ? ERR : WARN)(0, msg); AddErrRC(0, row, 3); if (strict) { metaErr++; continue; } else { metaWarn++; } }

              // refs: leggi e valida
              string refsJoined = "";
              using (var cmdR = cn.CreateCommand())
              {
                cmdR.CommandText = "SELECT Ref FROM Refs WHERE MetaId=@id";
                cmdR.Parameters.AddWithValue("@id", id);
                using (var rr = cmdR.ExecuteReader())
                {
                  var refs = new List<string>();
                  while (rr.Read())
                  {
                    string rfs = rr.IsDBNull(0) ? "" : rr.GetString(0);
                    if (!string.IsNullOrWhiteSpace(rfs))
                    {
                      if (!(IsHttpUrl(rfs) || IsAbsPath(rfs)))
                      { var msg = $"[Meta R{row}] REF invalid: {rfs}"; (strict ? ERR : WARN)(0, msg); AddErrRC(0, row, 5); if (strict) { metaErr++; refs.Clear(); break; } else { metaWarn++; } }
                      refs.Add(rfs);
                    }
                  }
                  refsJoined = refs.Count > 0 ? string.Join("|", refs) : "";
                }
              }

              if (!dryRun)
              {
                var payload = new { id = id, by = by ?? "", info = ds ?? "", rf = refsJoined, sn = "" };
                object persisted; string upInfo;
                bool ok = MetadataRepositoryCompatExtensions.TryUpsert(repo, payload, out persisted, out upInfo);
                if (!ok) { ERR(0, $"[Meta R{row}] import failed: {upInfo ?? "unknown"}"); metaErr++; continue; }
              }

              metaOk++;
            }
          }
        }

        // ===== VARIABLES =====
        using (var cmd = cn.CreateCommand())
        {
          cmd.CommandText = "SELECT Id, Name, Value, MetaId, Assumption FROM Variables ORDER BY Id";
          using (var rd = cmd.ExecuteReader())
          {
            int row = 0;
            while (rd.Read())
            {
              row++; varRows++;
              int id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
              string nm = rd.IsDBNull(1) ? "" : rd.GetString(1);
              string vl = rd.IsDBNull(2) ? "" : rd.GetString(2);
              int mid = rd.IsDBNull(3) ? 0 : rd.GetInt32(3);
              bool ass = !rd.IsDBNull(4) && (rd.GetInt32(4) != 0);

              // extra: NAME
              if (nm.Length > NAME_MAX || !IsPrintable(nm))
              { var msg = $"[Var R{row}] NAME invalid (len/charset)"; (strict ? ERR : WARN)(1, msg); AddErrRC(1, row, 2); if (strict) { varErr++; continue; } else { varWarn++; } }

              // depends
              int[] dep = Array.Empty<int>();
              using (var cmdD = cn.CreateCommand())
              {
                cmdD.CommandText = "SELECT DepId FROM VariableDepends WHERE VarId=@id ORDER BY DepId";
                cmdD.Parameters.AddWithValue("@id", id);
                using (var rr = cmdD.ExecuteReader())
                {
                  var tmp = new List<int>(); while (rr.Read()) tmp.Add(rr.GetInt32(0)); dep = tmp.ToArray();
                }
              }

              // metaId: se specificato deve esistere (preview = controllo; run = controllo + eventuale dedupe lato compat)
              if (mid > 0)
              {
                object dummy; string lookupInfo;
                bool okMeta = MetadataRepositoryCompatExtensions.TryGetByHashThenId(repo, "", mid, out dummy, out lookupInfo);
                if (!okMeta)
                { var msg = $"[Var R{row}] METAID not found: {mid}"; (strict ? ERR : WARN)(1, msg); AddErrRC(1, row, 4); if (strict) { varErr++; continue; } else { varWarn++; mid = 0; } }
              }

              if (!dryRun)
              {
                var payload = new
                {
                  id = id,
                  name = nm ?? "",
                  value = vl ?? "",
                  unit = "",
                  by = "",
                  isAssumption = ass,
                  mid = (mid > 0 ? mid.ToString(CultureInfo.InvariantCulture) : ""),
                  depends = (object)dep
                };
                object persisted; string upInfo;
                bool ok = MetadataRepositoryCompatExtensions.TryUpsertVariable(repo, payload, out persisted, out upInfo);
                if (!ok) { ERR(1, $"[Var R{row}] import failed: {upInfo ?? "unknown"}"); varErr++; continue; }
              }

              varOk++;
            }
          }
        }
      }

      // counts + log
      counts.Append(new GH_String($"Meta rows={metaRows} ok={metaOk} warn={metaWarn} err={metaErr}"), new GH_Path(0));
      counts.Append(new GH_String($"Vars rows={varRows} ok={varOk} warn={varWarn} err={varErr}"), new GH_Path(1));

      string logPath = db + ".import.log.txt";
      try { File.WriteAllLines(logPath, logLines, Encoding.UTF8); } catch { logPath = ""; }

      string prefix = dryRun ? "PREVIEW " : "OK ";
      string info = $"{prefix}ImportSqlite ← {db} | Meta {metaOk}/{metaRows} (warn:{metaWarn}, err:{metaErr}) | " +
                    $"Vars {varOk}/{varRows} (warn:{varWarn}, err:{varErr}) | Log: {(string.IsNullOrWhiteSpace(logPath) ? "-" : logPath)}";

      return (db, logPath, warnTree, errTree, counts, errRC, info);
    }

    private static (Dictionary<string, HashSet<string>> varAliases,
                    Dictionary<string, HashSet<string>> metaAliases)
      BuildAliasMaps(string mapJson)
    {
      var varA = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
      {
        ["ID"] = new HashSet<string>(new[] { "ID", "IDVAR", "VARID" }, StringComparer.OrdinalIgnoreCase),
        ["HASH"] = new HashSet<string>(new[] { "HASH", "DIGEST", "SHA" }, StringComparer.OrdinalIgnoreCase),
        ["NAME"] = new HashSet<string>(new[] { "NAME", "VAR", "VARIABLE", "NOME", "FIELD" }, StringComparer.OrdinalIgnoreCase),
        ["VALUE"] = new HashSet<string>(new[] { "VALUE", "VAL", "VALORE" }, StringComparer.OrdinalIgnoreCase),
        ["VALC"] = new HashSet<string>(new[] { "VALC", "VALUECANONICAL", "VAL_CANONICAL", "CANONICAL" }, StringComparer.OrdinalIgnoreCase),
        ["METAID"] = new HashSet<string>(new[] { "METAID", "MID", "METADATAID", "META_ID" }, StringComparer.OrdinalIgnoreCase),
        ["DEPENDS"] = new HashSet<string>(new[] { "DEPENDS", "DEPENDENCIES", "DEPS", "DEP", "PARENT_IDS" }, StringComparer.OrdinalIgnoreCase),
        ["ASSUMPTION"] = new HashSet<string>(new[] { "ASSUMPTION", "ASS", "ISASSUMPTION", "ASSUME" }, StringComparer.OrdinalIgnoreCase)
      };
      var metaA = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
      {
        ["ID"] = new HashSet<string>(new[] { "ID", "METAID" }, StringComparer.OrdinalIgnoreCase),
        ["HASH"] = new HashSet<string>(new[] { "HASH", "DIGEST" }, StringComparer.OrdinalIgnoreCase),
        ["BY"] = new HashSet<string>(new[] { "BY", "AUTHOR", "CREATEDBY", "CREATED_BY", "OWNER" }, StringComparer.OrdinalIgnoreCase),
        ["DESCRIPTION"] = new HashSet<string>(new[] { "DESCRIPTION", "DESC", "DESCR", "INFO", "NOTE", "NOTES" }, StringComparer.OrdinalIgnoreCase),
        ["REFS"] = new HashSet<string>(new[] { "REFS", "REF", "REFERENCE", "REFERENCES", "URLS", "LINKS" }, StringComparer.OrdinalIgnoreCase),
        ["SNIPS"] = new HashSet<string>(new[] { "SNIPS", "SNIP", "ATTACHMENTS", "IMAGES" }, StringComparer.OrdinalIgnoreCase),
        ["LM"] = new HashSet<string>(new[] { "LM", "LASTMODIFIED", "LAST_MODIFIED", "UPDATED", "LASTUPDATE", "LAST_UPDATE" }, StringComparer.OrdinalIgnoreCase)
      };
      if (!string.IsNullOrWhiteSpace(mapJson))
      {
        try
        {
          var j = JObject.Parse(mapJson);
          if (j["Variables"] is JObject vj) MergeAliases(vj, varA);
          if (j["Metadata"] is JObject mj) MergeAliases(mj, metaA);
        }
        catch { /* ignore malformed */ }
      }
      return (varA, metaA);

      static void MergeAliases(JObject obj, Dictionary<string, HashSet<string>> target)
      {
        foreach (var prop in obj.Properties())
        {
          var key = NormalizeKey(prop.Name);
          if (string.IsNullOrEmpty(key)) continue;
          if (!target.ContainsKey(key)) target[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
          if (prop.Value is JArray arr)
          {
            foreach (var t in arr)
            {
              var s = t?.ToString();
              if (!string.IsNullOrWhiteSpace(s))
                target[key].Add(NormalizeKey(s));
            }
          }
        }
      }
    }

    private static string NormalizeKey(string s)
    {
      if (string.IsNullOrEmpty(s)) return "";
      var up = s.Trim().ToUpperInvariant();
      var buf = new StringBuilder(up.Length);
      for (int i = 0; i < up.Length; i++) if (char.IsLetterOrDigit(up[i])) buf.Append(up[i]);
      return buf.ToString();
    }

    private static IXLWorksheet TryGetWorksheet(IXLWorkbook wb, params string[] names)
    {
      foreach (var n in names)
        foreach (var ws in wb.Worksheets)
          if (string.Equals(ws.Name, n, StringComparison.OrdinalIgnoreCase))
            return ws;
      return null;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws, out int firstRow, out int lastRow)
    {
      var used = ws.RangeUsed();
      if (used == null)
      {
        firstRow = 1; lastRow = 0;
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      }
      firstRow = used.RangeAddress.FirstAddress.RowNumber;
      lastRow = used.RangeAddress.LastAddress.RowNumber;

      var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var hdr = ws.Row(firstRow);
      foreach (var c in hdr.CellsUsed())
      {
        var key = NormalizeKey(c.GetString());
        if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
          map[key] = c.Address.ColumnNumber;
      }
      return map;
    }

    private static Dictionary<string, int> ResolveColumns(Dictionary<string, int> header, Dictionary<string, HashSet<string>> aliases)
    {
      var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      foreach (var kv in aliases)
      {
        string canonical = kv.Key;
        if (header.TryGetValue(canonical, out int col)) { result[canonical] = col; continue; }
        foreach (var alt in kv.Value) if (header.TryGetValue(alt, out col)) { result[canonical] = col; break; }
      }
      return result;
    }

    private static List<string> MissingRequired(Dictionary<string, int> map, IEnumerable<string> required)
    {
      var miss = new List<string>();
      foreach (var r in required) if (!map.ContainsKey(r)) miss.Add(r);
      return miss;
    }

    private static string ReadCell(IXLWorksheet ws, int row, Dictionary<string, int> map, string key)
    {
      if (!map.TryGetValue(key, out int col)) return "";
      var cell = ws.Cell(row, col);

      var s = cell.GetString();
      if (!string.IsNullOrWhiteSpace(s))
        return s;

      s = cell.GetFormattedString();
      if (!string.IsNullOrWhiteSpace(s))
        return s;

      try { return cell.Value.ToString(); }
      catch { return ""; }
    }


    private static int ReadIntCell(IXLWorksheet ws, int row, Dictionary<string, int> map, string key, int defaultValue = 0)
    {
      var s = ReadCell(ws, row, map, key);
      if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        return v;
      return defaultValue;
    }

    private static int[] ReadIntArrayCsvCell(IXLWorksheet ws, int row, Dictionary<string, int> map, string key)
    {
      var s = ReadCell(ws, row, map, key);
      if (string.IsNullOrWhiteSpace(s))
        return Array.Empty<int>();

      var parts = s.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(p => p.Trim())
                   .Where(p => p.Length > 0);

      var ids = new List<int>();
      foreach (var p in parts)
      {
        if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
          ids.Add(id);
      }

      return ids.Distinct().OrderBy(x => x).ToArray();
    }
    private static string ImportClustersFromExcel(string xlsxPath, StringTable table, Action<string> log, bool strict)
    {
      if (string.IsNullOrWhiteSpace(xlsxPath) || !File.Exists(xlsxPath))
        return "ImportClusters: file non trovato.";

      using var wb = new XLWorkbook(xlsxPath);

      var ws = TryGetWorksheet(wb, "ProgesiClusters", "Clusters");
      if (ws == null)
        return "ImportClusters: foglio 'ProgesiClusters' non trovato.";

      int r0 = 1, rN = 0;
      var map = BuildHeaderMap(ws, out r0, out rN);

      // rN da RangeUsed può essere “buggato” quando aggiungi righe in Excel.
      // Usiamo LastRowUsed come riferimento primario.
      int lastRow = ws.LastRowUsed()?.RowNumber() ?? rN;

      if (lastRow < r0 + 1)
        return "ImportClusters: foglio vuoto.";

      int imported = 0, skipped = 0, rows = 0;
    

      for (int r = r0 + 1; r <= lastRow; r++)
      {
        rows++;

        int id = ToInt(ReadCellAny(ws, r, map, "ID", "CLUSTERID", "CID"));
        if (id <= 0)
        {
          skipped++;
          continue;
        }
        string name = ReadCellAny(ws, r, map, "NAME", "CLUSTERNAME");
        if (string.IsNullOrWhiteSpace(name))
          name = $"Cluster-{id}";

        string desc = ReadCellAny(ws, r, map, "DESCRIPTION", "DESC", "INFO");

        var rawVarIds = ReadCellAny(ws, r, map,
  "VARIABLEIDS",
  "VARIDS",
  "IDS",
  "VARS");

        int[] varIds = ProgesiCore.ClusterImportParser.ParseVariableIds(rawVarIds);

        if (varIds.Length == 0)
        {
          if (strict)
          {
            skipped++;
            log($"[Clusters R{r}] ERROR: empty VariableIds raw='{rawVarIds}' (Id={id})");
            continue;
          }
          else
          {
            log($"[Clusters R{r}] WARNING: empty VariableIds raw='{rawVarIds}' (Id={id}) → imported with empty list");
            // import lenient
          }
        }


        string hash = ReadCellAny(ws, r, map, "HASH", "CHASH");

        var dto = new ClusterDto
        {
          Id = id,
          Name = name,
          Description = desc,
          VariableIds = varIds, // può essere vuoto
          Hashtag = string.IsNullOrWhiteSpace(hash) ? null : hash
        };

        string json = JsonConvert.SerializeObject(dto);

        table.SetString(
          "Progesi.Cluster",
          "cluster:" + id.ToString(CultureInfo.InvariantCulture),
          json);

        // Manteniamo __next__ coerente
        int next = ReadCounter(table, "Progesi.Cluster");
        if (id + 1 > next)
          table.SetString("Progesi.Cluster", "__next__", (id + 1).ToString(CultureInfo.InvariantCulture));

        imported++;
      }
      return $"OK ImportClusters: rows={rows}, imported={imported}, skipped={skipped}";
    }

    private static string ReadCellAny(
    IXLWorksheet ws,
    int row,
    Dictionary<string, int> map,
    params string[] keys)
      {
        foreach (var k in keys)
        {
          var s = ReadCell(ws, row, map, k);
          if (!string.IsNullOrWhiteSpace(s))
            return s;
        }
        return string.Empty;
      }

    private static int ExtractInt(string s, string marker)
    {
      if (string.IsNullOrWhiteSpace(s) || string.IsNullOrWhiteSpace(marker)) return 0;
      int i = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
      if (i < 0) return 0;
      i += marker.Length;
      int j = i;
      while (j < s.Length && char.IsDigit(s[j])) j++;
      var chunk = s.Substring(i, j - i);
      int v; return int.TryParse(chunk, out v) ? v : 0;
    }

    private static bool IsBlank(string s) => string.IsNullOrWhiteSpace(s);

    private static int ToInt(string s)
    {
      int n; return int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
    }

    private static bool ToBool(string s)
    {
      var t = (s ?? "").Trim();
      if (t == "1") return true;
      if (t == "0") return false;
      bool b; return bool.TryParse(t, out b) && b;
    }

    private static int[] ParseDepends(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return Array.Empty<int>();
      var tokens = s.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
      var list = new List<int>();
      foreach (var t in tokens)
      {
        int n; if (int.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0) list.Add(n);
      }
      list.Sort();
      return list.ToArray();
    }

    private static void ReadPersistedId(object persisted, ref int target)
    {
      if (persisted == null) return;
      var pi = persisted.GetType().GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(persisted, null);
      if (v == null) return;
      int n; if (int.TryParse(v.ToString(), out n)) target = n;
    }

    private static int ReadCounter(StringTable table, string scope)
    {
      string s = table.GetValue(scope, "__next__");
      int n; return int.TryParse(s, out n) ? n : 1;
    }

    // ----------------------- Lettura StringTable (export) --------------------
    private sealed class VarDto
    {
      public int Id { get; set; }
      public string Name { get; set; }
      public string Value { get; set; }
      public string ValueType { get; set; }
      public int? MetadataId { get; set; }
      public int[] Depends { get; set; }
      public bool? IsAssumption { get; set; }
    }

    private sealed class MetaDto
    {
      public int Id { get; set; }
      public DateTime LastModified { get; set; }
      public string CreatedBy { get; set; }
      public string AdditionalInfo { get; set; }
      public string[] References { get; set; }
      public object[] Snips { get; set; }
    }
 #nullable enable

    private sealed class ClusterDto
    {
      public int Id { get; set; }
      public string? Name { get; set; }
      public string? Description { get; set; }
      public int[]? VariableIds { get; set; }
      public string? Hashtag { get; set; }
    }

    private sealed class ClusterRow
    {
      public int Id { get; set; }
      public string Hash { get; set; } = "";
      public string Name { get; set; } = "";
      public string Description { get; set; } = "";
      public int[] VariableIds { get; set; } = Array.Empty<int>();
    }
#nullable disable

    private sealed class VarRow
    {
      public int Id;
      public string Hash;
      public string Name;
      public string Value;
      public string ValC;
      public int MetaId;
      public int[] Depends;
      public bool Assumption;
    }

    private sealed class MetaRow
    {
      public int Id;
      public string Hash;
      public string By;
      public string Description;
      public string[] Refs;
      public string LM;
    }

    private static int[] EnumerateVarIdsFromTable(StringTable table)
    {
      // Legge tutti i nomi entry nella sezione Progesi.Var
      var names = table.GetEntryNames("Progesi.Var") ?? Array.Empty<string>();
      var ids = new List<int>();

      foreach (var n in names)
      {
        if (string.IsNullOrWhiteSpace(n)) continue;

        // Entry attese: "var:<id>"
        if (!n.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
          continue;

        var tail = n.Substring(4).Trim();
        if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
          ids.Add(id);
      }

      ids.Sort();
      return ids.Distinct().ToArray();
    }

    private static int[] EnumerateMetaIdsFromTable(StringTable table)
    {
      var names = table.GetEntryNames("Progesi.Meta") ?? Array.Empty<string>();
      var ids = new List<int>();

      foreach (var n in names)
      {
        if (string.IsNullOrWhiteSpace(n)) continue;
        if (!n.StartsWith("meta:", StringComparison.OrdinalIgnoreCase)) continue;

        var tail = n.Substring(5).Trim();
        int id;
        if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id > 0)
          ids.Add(id);
      }

      ids.Sort();
      return ids.Distinct().ToArray();
    }

    private static int[] EnumerateClusterIdsFromTable(StringTable table)
    {
      var names = table.GetEntryNames("Progesi.Cluster") ?? Array.Empty<string>();
      var ids = new List<int>();

      foreach (var n in names)
      {
        if (string.IsNullOrWhiteSpace(n)) continue;
        if (!n.StartsWith("cluster:", StringComparison.OrdinalIgnoreCase)) continue;

        var tail = n.Substring(8).Trim();
        int id;
        if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id > 0)
          ids.Add(id);
      }

      ids.Sort();
      return ids.Distinct().ToArray();
    }


    private static VarRow[] ReadAllVarsFromTable(StringTable table)
    {
      var list = new List<VarRow>();

      // FIX: non usiamo __next__ perché può essere non aggiornato → enumeriamo le entry reali
      var ids = EnumerateVarIdsFromTable(table);

      foreach (var id in ids)
      {
        string json = table.GetValue("Progesi.Var", "var:" + id.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(json)) continue;

        VarDto dto;
        try { dto = JsonConvert.DeserializeObject<VarDto>(json); }
        catch { continue; }

        if (dto == null) continue;

        object typed = ParseValue(dto.Value, dto.ValueType ?? "string");
        string valc = ProgesiHash.CanonicalValue(typed);
        int[] deps = dto.Depends ?? Array.Empty<int>();
        bool ass = dto.IsAssumption ?? false;

        var pv = new ProgesiVariable(id, dto.Name ?? "", typed, deps, dto.MetadataId, ass);
        string hash = ProgesiHash.Compute(pv);

        list.Add(new VarRow
        {
          Id = id,
          Hash = hash,
          Name = dto.Name ?? "",
          Value = dto.Value ?? "",
          ValC = valc,
          MetaId = dto.MetadataId ?? 0,
          Depends = deps,
          Assumption = ass
        });
      }

      // Ordine stabile
      list.Sort((a, b) => a.Id.CompareTo(b.Id));
      return list.ToArray();
    }
    private static MetaRow[] ReadAllMetasFromTable(StringTable table)
    {
      var list = new List<MetaRow>();

      foreach (var id in EnumerateMetaIdsFromTable(table))
      {
        string json = table.GetValue("Progesi.Meta", "meta:" + id.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(json)) continue;

        MetaDto dto; try { dto = JsonConvert.DeserializeObject<MetaDto>(json); } catch { continue; }
        if (dto == null) continue;

        string by = dto.CreatedBy ?? "";
        string desc = dto.AdditionalInfo ?? "";

        var meta = ProgesiMetadata.Create(by, desc, null, null, dto.LastModified, id);
        string hash = ProgesiHash.Compute(meta);

        list.Add(new MetaRow
        {
          Id = id,
          Hash = hash,
          By = by,
          Description = desc,
          Refs = dto.References ?? Array.Empty<string>(),
          LM = dto.LastModified.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
      }

      return list.ToArray();
    }
    private static ClusterRow[] ReadAllClustersFromTable(StringTable table)
    {
      var list = new List<ClusterRow>();

      foreach (var id in EnumerateClusterIdsFromTable(table))
      {
        string json = table.GetValue("Progesi.Cluster", "cluster:" + id.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(json)) continue;

        ClusterDto dto;
        try { dto = JsonConvert.DeserializeObject<ClusterDto>(json); }
        catch { continue; }

        if (dto == null) continue;

        var varIds = dto.VariableIds ?? Array.Empty<int>();

        // Hash coerente con dominio (Id|Name|Ids)
        var cluster = ProgesiVariableCluster.Rehydrate(
          id,
          dto.Name ?? "",
          varIds,
          dto.Description,
          dto.Hashtag);

        list.Add(new ClusterRow
        {
          Id = id,
          Hash = cluster.Hashtag ?? "",
          Name = dto.Name ?? "",
          Description = dto.Description ?? "",
          VariableIds = varIds
        });
      }

      list.Sort((a, b) => a.Id.CompareTo(b.Id));
      return list.ToArray();
    }

    private static object ParseValue(string value, string valueType)
    {
      string vt = (valueType ?? "string").Trim().ToLowerInvariant();
      if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || vt == "null") return null;
      try
      {
        switch (vt)
        {
          case "string": return value ?? "";
          case "int": return int.Parse(value ?? "0", CultureInfo.InvariantCulture);
          case "long": return long.Parse(value ?? "0", CultureInfo.InvariantCulture);
          case "double": return double.Parse(value ?? "0", CultureInfo.InvariantCulture);
          case "bool": return string.Equals((value ?? "").Trim(), "true", StringComparison.OrdinalIgnoreCase);
          default:
            var t = Type.GetType(valueType, false);
            if (t == null) return value ?? "";
            return JsonConvert.DeserializeObject(value ?? "null", t);
        }
      }
      catch { return value ?? ""; }
    }
  }

}
