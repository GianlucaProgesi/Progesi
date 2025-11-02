// -----------------------------------------------------------------------------
// File   : ProgesiDataExchangeComponent.cs
// Scope  : ProgesiGrasshopperAssembly / Components
// Desc   : DataEx (Export/Import Excel) con validazione & log – RHINO-only
// Target : .NET Framework 4.8 + Grasshopper + ClosedXML (>= 0.102) + Newtonsoft.Json
// -----------------------------------------------------------------------------
#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino;
using Rhino.DocObjects.Tables;

using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure; // ServiceHub, ProgesiIcons, MetadataRepositoryCompatExtensions

using ClosedXML.Excel;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiDataExchangeComponent : GH_Component
  {
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
      p.AddTextParameter("Action", "Act", "ExportExcel | ImportExcel", GH_ParamAccess.item, "ExportExcel");
      p.AddTextParameter("Path", "Path", "Percorso file .xlsx (Export: dest., Import: src).", GH_ParamAccess.item, "");
      p.AddBooleanParameter("Overwrite", "Ovr", "Se true, sovrascrive (Export).", GH_ParamAccess.item, true);
      p.AddBooleanParameter("DryRun", "Dry", "Se TRUE: valida e logga senza scrivere nel repository.", GH_ParamAccess.item, false);


      // Validazione Import
      p.AddTextParameter("Mode", "Mode", "Import: 'Strict' oppure 'Lenient'.", GH_ParamAccess.item, "Lenient");
      p.AddBooleanParameter("FailOnError", "Fail", "Stop import se errori ≥ MaxErr.", GH_ParamAccess.item, false);
      p.AddIntegerParameter("MaxErrors", "MaxErr", "Soglia errori per stop (default 1000).", GH_ParamAccess.item, 1000);
      p.AddTextParameter("Map", "Map", "JSON alias colonne (opzionale).", GH_ParamAccess.item, "");
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
      DA.GetData(7, ref mapJson);
      DA.GetData(8, ref dryRun);

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
        var act = (action ?? "").Trim().ToUpperInvariant();

        if (act == "EXPORTEXCEL")
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

        if (act == "IMPORTEXCEL")
        {
          bool strict = string.Equals((mode ?? "").Trim(), "STRICT", StringComparison.OrdinalIgnoreCase);
          var (src, logPath, wTree, eTree, cTree, errRcTree, info) =
              ImportExcelValidated(path, strict, fail, Math.Max(0, maxErrors), mapJson, dryRun);

          DA.SetData(0, info);
          DA.SetData(1, string.IsNullOrWhiteSpace(logPath) ? (src ?? "") : logPath);
          DA.SetDataTree(2, wTree);
          DA.SetDataTree(3, eTree);
          DA.SetDataTree(4, cTree);
          DA.SetDataTree(5, errRcTree); // nuovo output ErrRC
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

        wb.SaveAs(p);
      }

      string info = $"OK ExportExcel → {p} (Vars:{vars.Length}, Meta:{metas.Length})";
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
        var path = new GH_Path(br, errTree.get_Branch(br)?.Count ?? 0);
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

      string prefix = dryRun ? "PREVIEW " : "OK ";
      string info = $"{prefix}ImportExcel ← {p} | Meta {metaOk}/{metaRows} (warn:{metaWarn}, err:{metaErr}) | " +
                    $"Vars {varOk}/{varRows} (warn:{varWarn}, err:{varErr}) | Log: {(string.IsNullOrWhiteSpace(logPath) ? "-" : logPath)}";

      return (p, logPath, warnTree, errTree, counts, errRC, info);
    }


    // ----------------------- Helpers (Export / Import) --------------------

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
      return ws.Cell(row, col).GetString() ?? "";
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

    private static VarRow[] ReadAllVarsFromTable(StringTable table)
    {
      int next = ReadCounter(table, "Progesi.Var");
      var list = new List<VarRow>();
      for (int id = 1; id < next; id++)
      {
        string json = table.GetValue("Progesi.Var", "var:" + id.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(json)) continue;

        VarDto dto; try { dto = JsonConvert.DeserializeObject<VarDto>(json); } catch { continue; }
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
      return list.ToArray();
    }

    private static MetaRow[] ReadAllMetasFromTable(StringTable table)
    {
      int next = ReadCounter(table, "Progesi.Meta");
      var list = new List<MetaRow>();
      for (int id = 1; id < next; id++)
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
