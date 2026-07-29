using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Progesi.GhExcelReadContract;

namespace Progesi.LiveDataExchange
{
  public static class LiveExchangeExcelImporter
  {
    public static LiveExchangeImportResult ImportValidated(
      string inPath,
      bool strict,
      bool failOnError,
      int maxErrors,
      string mapJson,
      bool dryRun,
      ILiveExchangeImportSink sink,
      IGeometryValueCodec geometryCodec)
    {
      if (sink == null) throw new ArgumentNullException(nameof(sink));
      if (geometryCodec == null) throw new ArgumentNullException(nameof(geometryCodec));

      string p = (inPath ?? "").Trim();
      if (string.IsNullOrEmpty(p)) throw new ArgumentException("Path .xlsx not specified.");
      if (!File.Exists(p)) throw new FileNotFoundException("File not found", p);

      var result = new LiveExchangeImportResult { SourcePath = p };
      void LOG(string lvl, string msg) => result.LogLines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {lvl}: {msg}");
      void WARN(int br, string msg) { result.Warnings.Add(new LiveExchangeMessage { Branch = br, Text = msg }); LOG("WARN", msg); }
      void ERR(int br, string msg) { result.Errors.Add(new LiveExchangeMessage { Branch = br, Text = msg }); LOG("ERROR", msg); }
      void AddErrRC(int br, int row, int col) => result.ErrorCoordinates.Add(new LiveExchangeErrorCoordinate { Branch = br, Row = row, Col = col });

      var (varAliases, metaAliases) = GhExcelAliasMaps.Build(mapJson);

      const int NAME_MAX = 128;
      const int DESC_MAX = 512;
      Func<string, bool> IsPrintable = s => string.IsNullOrEmpty(s) || s.All(ch => ch >= 32 && ch != 127);
      Func<string, bool> IsHttpUrl = s => Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");
      Func<string, bool> IsAbsPath = s => !string.IsNullOrEmpty(s) && Path.IsPathRooted(s);

      int metaRows = 0, metaOk = 0, metaWarn = 0, metaErr = 0, maxMetaId = 0;
      int varRows = 0, varOk = 0, varWarn = 0, varErr = 0, maxVarId = 0;
      int clusterRows = 0, clusterOk = 0, clusterWarn = 0, clusterErr = 0;

      using (var wb = new XLWorkbook(p))
      {
        ImportMetadata(wb, strict, dryRun, sink, metaAliases, IsPrintable, IsHttpUrl, IsAbsPath, NAME_MAX, DESC_MAX,
          ref metaRows, ref metaOk, ref metaWarn, ref metaErr, ref maxMetaId, WARN, ERR, AddErrRC);

        ImportVariables(wb, strict, dryRun, sink, geometryCodec, varAliases, IsPrintable, NAME_MAX,
          ref varRows, ref varOk, ref varWarn, ref varErr, ref maxVarId, WARN, ERR, AddErrRC);

        ImportClusters(wb, strict, dryRun, sink, IsPrintable, IsHttpUrl, IsAbsPath, NAME_MAX, DESC_MAX,
          ref clusterRows, ref clusterOk, ref clusterWarn, ref clusterErr, WARN, ERR, AddErrRC);
      }

      if (!dryRun)
        sink.UpdateIdCounters(maxMetaId, maxVarId);

      string logPath = p + ".import.log.txt";
      try { File.WriteAllLines(logPath, result.LogLines, Encoding.UTF8); } catch { logPath = ""; }
      result.LogPath = logPath;

      result.Counts.Add(new LiveExchangeCountSummary { Branch = 0, Text = $"Meta rows={metaRows} ok={metaOk} warn={metaWarn} err={metaErr}" });
      result.Counts.Add(new LiveExchangeCountSummary { Branch = 1, Text = $"Vars rows={varRows} ok={varOk} warn={varWarn} err={varErr}" });
      result.Counts.Add(new LiveExchangeCountSummary { Branch = 2, Text = $"Clusters rows={clusterRows} ok={clusterOk} warn={clusterWarn} err={clusterErr}" });

      string prefix = dryRun ? "PREVIEW " : "OK ";
      result.Info = $"{prefix}ImportExcel ← {p} | Meta {metaOk}/{metaRows} (warn:{metaWarn}, err:{metaErr}) | " +
                    $"Vars {varOk}/{varRows} (warn:{varWarn}, err:{varErr}) | " +
                    $"Clusters {clusterOk}/{clusterRows} (warn:{clusterWarn}, err:{clusterErr}) | " +
                    $"Log: {(string.IsNullOrWhiteSpace(logPath) ? "-" : logPath)}";

      return result;
    }

    private static void ImportMetadata(
      XLWorkbook wb, bool strict, bool dryRun, ILiveExchangeImportSink sink,
      Dictionary<string, HashSet<string>> metaAliases,
      Func<string, bool> isPrintable, Func<string, bool> isHttpUrl, Func<string, bool> isAbsPath,
      int nameMax, int descMax,
      ref int metaRows, ref int metaOk, ref int metaWarn, ref int metaErr, ref int maxMetaId,
      Action<int, string> warn, Action<int, string> err, Action<int, int, int> addErrRc)
    {
      var wsM = GhExcelWorksheetLocator.TryGetWorksheet(wb, GhExcelSheetNames.Metadata, GhExcelSheetNames.MetadataAlias);
      bool metaHeaderError = false;
      Dictionary<string, int> mapM = null;
      int r0M = 1, rNM = 0;

      if (wsM == null)
      {
        string m = "Sheet 'ProgesiMetadata' not found.";
        if (strict) { err(0, m); metaHeaderError = true; }
        else { warn(0, m); }
      }
      else
      {
        var headerM = GhExcelHeaderMap.Build(wsM, out r0M, out rNM);
        mapM = GhExcelColumnMap.ResolveColumns(headerM, metaAliases);
        var missingMeta = GhExcelColumnMap.MissingRequired(mapM, new[] { "BY", "DESCRIPTION" });
        if (missingMeta.Count > 0)
        {
          string m = "Missing headers (Meta): " + string.Join(",", missingMeta);
          if (strict) { err(0, m); metaHeaderError = true; addErrRc(0, r0M, -1); }
          else { warn(0, m); metaWarn += missingMeta.Count; }
        }
      }

      if (metaHeaderError || wsM == null)
        return;

      for (int r = r0M + 1; r <= rNM; r++)
      {
        string by = GhExcelCellReader.ReadCell(wsM, r, mapM, "BY");
        string desc = GhExcelCellReader.ReadCell(wsM, r, mapM, "DESCRIPTION");
        string refs = GhExcelCellReader.ReadCell(wsM, r, mapM, "REFS");
        int id = GhExcelValueParsing.ToInt(GhExcelCellReader.ReadCell(wsM, r, mapM, "ID"));

        if (GhExcelValueParsing.IsBlank(by) && GhExcelValueParsing.IsBlank(desc) && GhExcelValueParsing.IsBlank(refs))
        { warn(0, $"[Meta R{r}] empty row → skip"); metaWarn++; metaRows++; continue; }

        if (by.Length > nameMax || !isPrintable(by))
        { var msg = $"[Meta R{r}] BY invalid (len/charset)"; (strict ? err : warn)(0, msg); addErrRc(0, r, mapM.TryGetValue("BY", out var c) ? c : 0); if (strict) { metaErr++; continue; } else { metaWarn++; } }

        if (desc.Length > descMax || !isPrintable(desc))
        { var msg = $"[Meta R{r}] DESCRIPTION invalid (len/charset)"; (strict ? err : warn)(0, msg); addErrRc(0, r, mapM.TryGetValue("DESCRIPTION", out var c) ? c : 0); if (strict) { metaErr++; continue; } else { metaWarn++; } }

        if (!string.IsNullOrWhiteSpace(refs))
        {
          foreach (var token in refs.Split('|'))
          {
            var s = token?.Trim(); if (string.IsNullOrEmpty(s)) continue;
            if (!(isHttpUrl(s) || isAbsPath(s)))
            { var msg = $"[Meta R{r}] REF invalid: {s}"; (strict ? err : warn)(0, msg); addErrRc(0, r, mapM.TryGetValue("REFS", out var c) ? c : 0); if (strict) { metaErr++; goto NEXT_META; } else { metaWarn++; } }
          }
        }

        if (!dryRun)
        {
          if (!sink.TryUpsertMetadata(id, by ?? "", desc ?? "", refs ?? "", out var pid, out var upInfo))
          { var msg = $"[Meta R{r}] import failed: {upInfo ?? "unknown"}"; err(0, msg); addErrRc(0, r, 0); metaErr++; metaRows++; continue; }
          if (pid > 0 && pid > maxMetaId) maxMetaId = pid;
        }

        metaOk++; metaRows++;
        NEXT_META:;
      }
    }

    private static void ImportVariables(
      XLWorkbook wb, bool strict, bool dryRun, ILiveExchangeImportSink sink, IGeometryValueCodec geometryCodec,
      Dictionary<string, HashSet<string>> varAliases, Func<string, bool> isPrintable, int nameMax,
      ref int varRows, ref int varOk, ref int varWarn, ref int varErr, ref int maxVarId,
      Action<int, string> warn, Action<int, string> err, Action<int, int, int> addErrRc)
    {
      var wsV = GhExcelWorksheetLocator.TryGetWorksheet(wb, GhExcelSheetNames.Variables, GhExcelSheetNames.VariablesAlias);
      bool varHeaderError = false;
      Dictionary<string, int> mapV = null;
      int r0V = 1, rNV = 0;

      if (wsV == null)
      {
        string m = "Sheet 'ProgesiVariables' not found.";
        if (strict) { err(1, m); varHeaderError = true; }
        else { warn(1, m); }
      }
      else
      {
        var headerV = GhExcelHeaderMap.Build(wsV, out r0V, out rNV);
        mapV = GhExcelColumnMap.ResolveColumns(headerV, varAliases);
        var missingVar = GhExcelColumnMap.MissingRequired(mapV, new[] { "NAME", "VALUE" });
        if (missingVar.Count > 0)
        {
          string m = "Missing headers (Vars): " + string.Join(",", missingVar);
          if (strict) { err(1, m); varHeaderError = true; addErrRc(1, r0V, -1); }
          else { warn(1, m); varWarn += missingVar.Count; }
        }
      }

      if (varHeaderError || wsV == null)
        return;

      var objectChunkRows = LiveExchangeObjectChunkReader.ReadObjectChunkRows(wb);

      for (int r = r0V + 1; r <= rNV; r++)
      {
        string name = GhExcelCellReader.ReadCell(wsV, r, mapV, "NAME");
        string value = GhExcelCellReader.ReadCell(wsV, r, mapV, "VALUE");
        string deps = GhExcelCellReader.ReadCell(wsV, r, mapV, "DEPENDS");
        string asS = GhExcelCellReader.ReadCell(wsV, r, mapV, "ASSUMPTION");
        int id = GhExcelValueParsing.ToInt(GhExcelCellReader.ReadCell(wsV, r, mapV, "ID"));
        string metaCell = GhExcelCellReader.ReadCell(wsV, r, mapV, "METAID");
        int[] metaIds = LiveExchangeMetadataIds.Parse(metaCell);

        if (GhExcelValueParsing.IsBlank(name) && GhExcelValueParsing.IsBlank(value) && GhExcelValueParsing.IsBlank(deps) && GhExcelValueParsing.IsBlank(asS))
        { warn(1, $"[Var R{r}] empty row → skip"); varWarn++; varRows++; continue; }

        if (name.Length > nameMax || !isPrintable(name))
        { var msg = $"[Var R{r}] NAME invalid (len/charset)"; (strict ? err : warn)(1, msg); addErrRc(1, r, mapV.TryGetValue("NAME", out var c) ? c : 0); if (strict) { varErr++; continue; } else { varWarn++; } }

        if (metaIds.Length > 0)
        {
          var resolvedMetaIds = new List<int>();
          var droppedMetaIds = new List<int>();
          mapV.TryGetValue("METAID", out var metaIdCol);

          foreach (var mid in metaIds)
          {
            if (!sink.TryGetMetadataById(mid, out var lookupInfo))
            {
              var msg = $"[Var R{r}] METAID not found: {mid}";
              if (strict)
              {
                err(1, msg);
                addErrRc(1, r, metaIdCol);
                varErr++;
                metaIds = Array.Empty<int>();
                break;
              }

              warn(1, msg);
              addErrRc(1, r, metaIdCol);
              varWarn++;
              droppedMetaIds.Add(mid);
            }
            else
            {
              resolvedMetaIds.Add(mid);
            }
          }

          if (strict)
          {
            if (metaIds.Length == 0) { varRows++; continue; }
          }
          else
          {
            metaIds = resolvedMetaIds.ToArray();
            if (droppedMetaIds.Count > 0)
            {
              var keptText = metaIds.Length > 0 ? string.Join(",", metaIds) : "(none)";
              var droppedText = string.Join(",", droppedMetaIds);
              warn(1, $"[Var R{r}] METAID partial resolve: kept [{keptText}], dropped unresolved [{droppedText}]");
            }
          }
        }

        int[] depArr = GhExcelValueParsing.ParseDepends(deps);
        bool ass = GhExcelValueParsing.ToBool(asS);

        string geometryJson = null;
        if (GhExcelObjectSheet.TryParseObjectMarker(value, out _))
        {
          if (!GhExcelObjectSheet.TryReassemblePayload(objectChunkRows, id, out var payload, out var objectType, out var objErr))
          {
            var msg = $"[Var R{r}] object payload missing/invalid: {objErr}";
            if (strict) { err(1, msg); addErrRc(1, r, mapV.TryGetValue("VALUE", out var c) ? c : 0); varErr++; }
            else { warn(1, msg); addErrRc(1, r, mapV.TryGetValue("VALUE", out var c) ? c : 0); varWarn++; }
            varRows++;
            continue;
          }

          if (!geometryCodec.TryDecode(payload, out _))
          {
            var msg = $"[Var R{r}] object decode failed ({objectType})";
            if (strict) { err(1, msg); addErrRc(1, r, mapV.TryGetValue("VALUE", out var c) ? c : 0); varErr++; }
            else { warn(1, msg); addErrRc(1, r, mapV.TryGetValue("VALUE", out var c) ? c : 0); varWarn++; }
            varRows++;
            continue;
          }

          geometryJson = payload;
        }
        else if (!GhExcelVariableValueSupport.IsImportSupported(value, out var unsupportedKind, out var unsupportedDetail))
        {
          var msg = GhExcelVariableValueSupport.BuildImportSkipMessage(r, unsupportedKind, unsupportedDetail);
          if (strict) { err(1, msg); addErrRc(1, r, mapV.TryGetValue("VALUE", out var c) ? c : 0); varErr++; }
          else { warn(1, msg); addErrRc(1, r, mapV.TryGetValue("VALUE", out var c) ? c : 0); varWarn++; }
          varRows++;
          continue;
        }

        if (!dryRun)
        {
          var payload = new LiveExchangeVariableImportPayload
          {
            Id = id,
            Name = name ?? "",
            Value = geometryJson ?? (value ?? ""),
            GeometryJson = geometryJson ?? "",
            IsAssumption = ass,
            MetadataIds = metaIds,
            Depends = depArr
          };

          if (!sink.TryUpsertVariable(payload, out var pid, out var upInfo))
          { var msg = $"[Var R{r}] import failed: {upInfo ?? "unknown"}"; err(1, msg); addErrRc(1, r, 0); varErr++; varRows++; continue; }
          if (pid > 0 && pid > maxVarId) maxVarId = pid;
        }

        varOk++; varRows++;
      }
    }

    private static void ImportClusters(
      XLWorkbook wb, bool strict, bool dryRun, ILiveExchangeImportSink sink,
      Func<string, bool> isPrintable, Func<string, bool> isHttpUrl, Func<string, bool> isAbsPath,
      int nameMax, int descMax,
      ref int clusterRows, ref int clusterOk, ref int clusterWarn, ref int clusterErr,
      Action<int, string> warn, Action<int, string> err, Action<int, int, int> addErrRc)
    {
      var wsC = GhExcelWorksheetLocator.TryGetWorksheet(wb, GhExcelSheetNames.Clusters, GhExcelSheetNames.ClustersAlias);
      bool clusterHeaderError = false;
      Dictionary<string, int> mapC = null;
      int r0C = 1, rNC = 0;

      if (wsC == null)
      {
        string m = "Sheet 'ProgesiClusters' not found.";
        if (strict) { err(2, m); clusterHeaderError = true; }
        else { warn(2, m); }
      }
      else
      {
        var headerC = GhExcelHeaderMap.Build(wsC, out r0C, out rNC);
        mapC = GhExcelClusterSheet.ResolveClusterColumns(headerC);
        var missingCluster = GhExcelColumnMap.MissingRequired(mapC, new[] { "ID" });
        if (missingCluster.Count > 0)
        {
          string m = "Missing headers (Clusters): " + string.Join(",", missingCluster);
          if (strict) { err(2, m); clusterHeaderError = true; addErrRc(2, r0C, -1); }
          else { warn(2, m); clusterWarn += missingCluster.Count; }
        }
      }

      if (clusterHeaderError || wsC == null)
        return;

      for (int r = r0C + 1; r <= rNC; r++)
      {
        if (GhExcelClusterImport.IsBlankDataRow(wsC, r, mapC))
        { warn(2, $"[Cluster R{r}] empty row → skip"); clusterWarn++; clusterRows++; continue; }

        if (!GhExcelClusterImport.TryBuildRowDto(wsC, r, mapC, out var rowDto, out var parseError))
        {
          var msg = $"[Cluster R{r}] {parseError}";
          if (strict) { err(2, msg); addErrRc(2, r, mapC.TryGetValue("ID", out var cidCol) ? cidCol : 0); clusterErr++; }
          else { warn(2, msg); clusterWarn++; }
          clusterRows++;
          continue;
        }

        if (!string.IsNullOrWhiteSpace(rowDto.ParseWarning))
        {
          var msg = $"[Cluster R{r}] {rowDto.ParseWarning}";
          if (strict) { err(2, msg); addErrRc(2, r, mapC.TryGetValue("VARIABLEIDS", out var vidCol) ? vidCol : 0); clusterErr++; clusterRows++; continue; }
          else { warn(2, msg); clusterWarn++; }
        }

        if (rowDto.VariableIds == null || rowDto.VariableIds.Length == 0)
        {
          warn(2, $"[Cluster R{r}] no VariableIds → skip persist");
          clusterWarn++;
          clusterRows++;
          continue;
        }

        string cName = rowDto.Name ?? "";
        if (cName.Length > nameMax || !isPrintable(cName))
        {
          var msg = $"[Cluster R{r}] NAME invalid (len/charset)";
          if (strict) { err(2, msg); addErrRc(2, r, mapC.TryGetValue("NAME", out var nameCol) ? nameCol : 0); clusterErr++; clusterRows++; continue; }
          else { warn(2, msg); clusterWarn++; }
        }

        string cDesc = rowDto.Description ?? "";
        if (cDesc.Length > descMax || !isPrintable(cDesc))
        {
          var msg = $"[Cluster R{r}] DESCRIPTION invalid (len/charset)";
          if (strict) { err(2, msg); addErrRc(2, r, mapC.TryGetValue("DESCRIPTION", out var descCol) ? descCol : 0); clusterErr++; clusterRows++; continue; }
          else { warn(2, msg); clusterWarn++; }
        }

        if (!dryRun)
        {
          var cluster = new LiveExchangeClusterImportPayload
          {
            Id = rowDto.Id,
            Name = cName,
            Description = cDesc,
            VariableIds = rowDto.VariableIds,
            Hashtag = rowDto.Hashtag
          };

          if (!sink.TryPersistCluster(cluster))
          {
            err(2, $"[Cluster R{r}] persist failed");
            clusterErr++;
            clusterRows++;
            continue;
          }
        }

        clusterOk++;
        clusterRows++;
      }
    }
  }
}
