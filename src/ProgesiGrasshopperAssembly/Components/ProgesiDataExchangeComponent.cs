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
using ProgesiCore;
using Progesi.GhExcelReadContract;
using Progesi.LiveDataExchange;
using ProgesiRepositories.Rhino;
using ProgesiGrasshopperAssembly.Infrastructure; // ServiceHub, ProgesiIcons, MetadataRepositoryCompatExtensions
using Rhino;
using Rhino.DocObjects.Tables;
using Rhino.Geometry;
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


// in cima al file, tra gli using:

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiDataExchangeComponent : GH_Component
  {
    private static readonly IGeometryValueCodec GeometryCodec = new RhinoGeometryValueCodecAdapter();

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
          bool strict = string.Equals((mode ?? "").Trim(), "STRICT", StringComparison.OrdinalIgnoreCase);
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
      return LiveExchangeExcelExporter.Export(ReadSnapshotFromTable(), inPath, overwrite);
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
      object repo; string hubInfo;
      if (!ServiceHub.TryGetMetadataRepository(out repo, out hubInfo))
        throw new InvalidOperationException("RHINO repository not available.");

      StringTable clusterTable = null;
      if (!dryRun)
      {
        var clusterDoc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
        clusterTable = clusterDoc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
      }

      var sink = new RhinoLiveExchangeImportSink(repo, clusterTable);
      var result = LiveExchangeExcelImporter.ImportValidated(
        inPath, strict, failOnError, maxErrors, mapJson, dryRun, sink, GeometryCodec);
      return MapImportResult(result);
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

    private static (string path, string info) ExportSqlite(string inPath, bool overwrite)
    {
      return LiveExchangeSqliteExporter.Export(ReadSnapshotFromTable(), inPath, overwrite);
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
      object repo; string hub;
      if (!ServiceHub.TryGetMetadataRepository(out repo, out hub))
        throw new InvalidOperationException("RHINO repository not available.");

      StringTable clusterTable = null;
      if (!dryRun)
      {
        var clusterDoc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
        clusterTable = clusterDoc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
      }

      var sink = new RhinoLiveExchangeImportSink(repo, clusterTable);
      var result = LiveExchangeSqliteImporter.ImportValidated(inDbPath, strict, dryRun, sink);
      return MapImportResult(result);
    }

    private static LiveExchangeSnapshot ReadSnapshotFromTable()
    {
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      StringTable table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
      return new LiveExchangeSnapshot
      {
        Variables = ReadAllVarsFromTable(table),
        Metadata = ReadAllMetasFromTable(table),
        Clusters = ReadAllClustersFromTable(table)
      };
    }

    private static (string srcPath,
                    string logPath,
                    GH_Structure<GH_String> warnOut,
                    GH_Structure<GH_String> errOut,
                    GH_Structure<GH_String> countsOut,
                    GH_Structure<GH_Integer> errRcOut,
                    string info)
    MapImportResult(LiveExchangeImportResult result)
    {
      var warnTree = new GH_Structure<GH_String>();
      var errTree = new GH_Structure<GH_String>();
      var counts = new GH_Structure<GH_String>();
      var errRC = new GH_Structure<GH_Integer>();

      foreach (var w in result.Warnings)
        warnTree.Append(new GH_String(w.Text), new GH_Path(w.Branch));
      foreach (var e in result.Errors)
        errTree.Append(new GH_String(e.Text), new GH_Path(e.Branch));
      foreach (var c in result.Counts)
        counts.Append(new GH_String(c.Text), new GH_Path(c.Branch));
      foreach (var rc in result.ErrorCoordinates)
      {
        var path = new GH_Path(rc.Branch);
        errRC.Append(new GH_Integer(rc.Row), path);
        errRC.Append(new GH_Integer(rc.Col), path);
      }

      return (result.SourcePath, result.LogPath, warnTree, errTree, counts, errRC, result.Info);
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
      public int[] MetadataIds { get; set; }
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
#nullable disable

    private static ClusterExportRow[] ReadAllClustersFromTable(StringTable table)
    {
      var list = new List<ClusterExportRow>();

      foreach (var id in EnumerateIds(table, "Progesi.Cluster", "cluster:"))
      {
        string json = table.GetValue("Progesi.Cluster", "cluster:" + id.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(json)) continue;

        ClusterDto dto;
        try { dto = JsonConvert.DeserializeObject<ClusterDto>(json); }
        catch { continue; }

        if (dto == null) continue;

        var varIds = dto.VariableIds ?? Array.Empty<int>();

        string name = dto.Name ?? "";
        string desc = dto.Description ?? "";

        var cluster = ProgesiVariableCluster.Rehydrate(
          id,
          name,
          varIds,
          desc,
          dto.Hashtag);

        list.Add(new ClusterExportRow
        {
          Id = id,
          Hash = cluster.Hashtag ?? "",
          Name = name,
          Description = desc,
          VariableIds = varIds
        });
      }

      list.Sort((a, b) => a.Id.CompareTo(b.Id));
      return list.ToArray();
    }

    private static VariableExportRow[] ReadAllVarsFromTable(StringTable table)
    {
      var list = new List<VariableExportRow>();
      foreach (var id in EnumerateIds(table, "Progesi.Var", "var:"))
      {
        string json = table.GetValue("Progesi.Var", "var:" + id.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(json)) continue;

        VarDto dto; try { dto = JsonConvert.DeserializeObject<VarDto>(json); } catch { continue; }
        if (dto == null) continue;

        object typed = ParseValue(dto.Value, dto.ValueType ?? "string");
        int[] deps = dto.Depends ?? Array.Empty<int>();
        bool ass = dto.IsAssumption ?? false;
        var metadataIds = ReadVarDtoMetadataIds(dto);

        list.Add(LiveExchangeVariableExportBuilder.Build(
          id,
          dto.Name ?? "",
          typed,
          dto.Value ?? "",
          dto.ValueType ?? "string",
          deps,
          metadataIds,
          ass,
          GeometryCodec));
      }
      return list.ToArray();
    }

    private static MetadataExportRow[] ReadAllMetasFromTable(StringTable table)
    {
      var list = new List<MetadataExportRow>();
      foreach (var id in EnumerateIds(table, "Progesi.Meta", "meta:"))
      {
        string json = table.GetValue("Progesi.Meta", "meta:" + id.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(json)) continue;

        MetaDto dto; try { dto = JsonConvert.DeserializeObject<MetaDto>(json); } catch { continue; }
        if (dto == null) continue;

        string by = dto.CreatedBy ?? "";
        string desc = dto.AdditionalInfo ?? "";

        var meta = ProgesiMetadata.Create(by, desc, null, null, dto.LastModified, id);
        string hash = ProgesiHash.Compute(meta);

        list.Add(new MetadataExportRow
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

    private static int[] EnumerateIds(StringTable table, string section, string prefix)
    {
      var names = table.GetEntryNames(section) ?? Array.Empty<string>();
      var ids = new List<int>();

      foreach (var n in names)
      {
        if (string.IsNullOrWhiteSpace(n)) continue;
        if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

        var tail = n.Substring(prefix.Length).Trim();
        if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
          ids.Add(id);
      }

      ids.Sort();
      return ids.Distinct().ToArray();
    }

    private static object ParseValue(string value, string valueType)
    {
      string vt = (valueType ?? "string").Trim();
      if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
          || string.Equals(vt, "null", StringComparison.OrdinalIgnoreCase))
        return null;

      if (ProgesiGeometryValueCodec.IsGeometryValueType(vt)
          && ProgesiGeometryValueCodec.TryDecode(value, out var geometry)
          && geometry != null)
        return geometry;

      try
      {
        switch (vt.ToLowerInvariant())
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

    private static int[] ReadVarDtoMetadataIds(VarDto dto)
    {
      if (dto?.MetadataIds != null && dto.MetadataIds.Length > 0)
        return dto.MetadataIds;

      if (dto?.MetadataId.HasValue == true && dto.MetadataId.Value > 0)
        return new[] { dto.MetadataId.Value };

      return Array.Empty<int>();
    }

    private sealed class RhinoGeometryValueCodecAdapter : IGeometryValueCodec
    {
      public bool IsGeometryValueType(string valueType) =>
        ProgesiGeometryValueCodec.IsGeometryValueType(valueType);

      public bool TryGetShortTypeName(object value, out string objectType)
      {
        if (value is GeometryBase geometry)
        {
          objectType = ProgesiGeometryValueCodec.GetShortTypeName(geometry);
          return true;
        }

        objectType = null;
        return false;
      }

      public bool TryEncode(object value, out string objectType, out string payloadJson)
      {
        if (value is GeometryBase geometry)
        {
          objectType = ProgesiGeometryValueCodec.GetShortTypeName(geometry);
          payloadJson = ProgesiGeometryValueCodec.Encode(geometry);
          return true;
        }

        objectType = null;
        payloadJson = null;
        return false;
      }

      public bool TryDecode(string payloadJson, out object geometry)
      {
        if (ProgesiGeometryValueCodec.TryDecode(payloadJson, out var decoded) && decoded != null)
        {
          geometry = decoded;
          return true;
        }

        geometry = null;
        return false;
      }
    }

    private sealed class RhinoLiveExchangeImportSink : ILiveExchangeImportSink
    {
      private readonly object _repo;
      private readonly StringTable _clusterTable;
      private int _maxMetaId;
      private int _maxVarId;

      public RhinoLiveExchangeImportSink(object repo, StringTable clusterTable)
      {
        _repo = repo;
        _clusterTable = clusterTable;
      }

      public bool TryUpsertMetadata(int id, string by, string description, string refsPipeSeparated, out int persistedId, out string error)
      {
        persistedId = 0;
        var payload = new { id, by, info = description, rf = refsPipeSeparated, sn = "" };
        object persisted;
        bool ok = MetadataRepositoryCompatExtensions.TryUpsert(_repo, payload, out persisted, out error);
        if (ok)
        {
          ReadPersistedId(persisted, ref persistedId);
          if (persistedId > _maxMetaId) _maxMetaId = persistedId;
        }

        return ok;
      }

      public bool TryGetMetadataById(int id, out string error)
      {
        object dummy;
        return MetadataRepositoryCompatExtensions.TryGetByHashThenId(_repo, "", id, out dummy, out error);
      }

      public bool TryUpsertVariable(LiveExchangeVariableImportPayload payload, out int persistedId, out string error)
      {
        persistedId = 0;
        var repoPayload = new
        {
          id = payload.Id,
          name = payload.Name,
          value = payload.GeometryJson ?? payload.Value,
          geometryJson = payload.GeometryJson ?? "",
          unit = "",
          by = "",
          isAssumption = payload.IsAssumption,
          metadataIds = (object)payload.MetadataIds,
          mid = LiveExchangeMetadataIds.FormatForExcel(payload.MetadataIds),
          depends = (object)payload.Depends
        };

        object persisted;
        bool ok = MetadataRepositoryCompatExtensions.TryUpsertVariable(_repo, repoPayload, out persisted, out error);
        if (ok)
        {
          ReadPersistedId(persisted, ref persistedId);
          if (persistedId > _maxVarId) _maxVarId = persistedId;
        }

        return ok;
      }

      public bool TryPersistCluster(LiveExchangeClusterImportPayload cluster)
      {
        if (_clusterTable == null) return false;

        var dto = new ClusterDto
        {
          Id = cluster.Id,
          Name = cluster.Name,
          Description = cluster.Description,
          VariableIds = cluster.VariableIds,
          Hashtag = cluster.Hashtag
        };

        string json = JsonConvert.SerializeObject(dto);
        _clusterTable.SetString("Progesi.Cluster", "cluster:" + cluster.Id.ToString(CultureInfo.InvariantCulture), json);

        int next = ReadCounter(_clusterTable, "Progesi.Cluster");
        if (cluster.Id + 1 > next)
          _clusterTable.SetString("Progesi.Cluster", "__next__", (cluster.Id + 1).ToString(CultureInfo.InvariantCulture));

        return true;
      }

      public void UpdateIdCounters(int maxMetaId, int maxVarId)
      {
        var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
        StringTable table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
        int curMetaNext = ReadCounter(table, "Progesi.Meta");
        int curVarNext = ReadCounter(table, "Progesi.Var");
        int metaTarget = Math.Max(maxMetaId, _maxMetaId);
        int varTarget = Math.Max(maxVarId, _maxVarId);
        if (metaTarget > 0)
          table.SetString("Progesi.Meta", "__next__", Math.Max(curMetaNext, metaTarget + 1).ToString(CultureInfo.InvariantCulture));
        if (varTarget > 0)
          table.SetString("Progesi.Var", "__next__", Math.Max(curVarNext, varTarget + 1).ToString(CultureInfo.InvariantCulture));
      }
    }
  }

}
