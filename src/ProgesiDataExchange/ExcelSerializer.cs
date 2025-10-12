using System;
using System.Collections.Generic;
using ClosedXML.Excel;

namespace Progesi.DataExchange
{
  public static class ExcelSerializer
  {
    public static (List<ProgesiVariableDto> vars, List<ProgesiMetadataDto> mets, List<ProgesiAxisVariableDto> axis)
      Read(string xlsxPath)
    {
      var vars = new List<ProgesiVariableDto>();
      var mets = new List<ProgesiMetadataDto>();
      var axis = new List<ProgesiAxisVariableDto>();

      using var wb = new XLWorkbook(xlsxPath);
      if (wb.TryGetWorksheet("ProgesiVariable", out var shV)) ReadSheet(shV, vars);
      if (wb.TryGetWorksheet("ProgesiMetadata", out var shM)) ReadSheet(shM, mets);
      if (wb.TryGetWorksheet("ProgesiAxisVariable", out var shA)) ReadSheet(shA, axis);
      return (vars, mets, axis);
    }

    public static void Write(
      string xlsxPath,
      IEnumerable<ProgesiVariableDto> vars,
      IEnumerable<ProgesiMetadataDto> mets,
      IEnumerable<ProgesiAxisVariableDto> axis)
    {
      using var wb = new XLWorkbook();
      WriteSheet(wb.Worksheets.Add("ProgesiVariable"), vars,
        new[] { "Id", "Hash", "Name", "Value", "Unit", "By", "Ref", "LastModifiedUtc" });
      WriteSheet(wb.Worksheets.Add("ProgesiMetadata"), mets,
        new[] { "Id", "Hash", "Info", "By", "Ref", "LastModifiedUtc" });
      WriteSheet(wb.Worksheets.Add("ProgesiAxisVariable"), axis,
        new[] { "Id", "Hash", "Name", "Unit", "AxisRef", "Stations", "Values", "By", "Ref", "LastModifiedUtc" });
      wb.SaveAs(xlsxPath);
    }

    static void ReadSheet(IXLWorksheet ws, List<ProgesiVariableDto> list)
    {
      var hdr = Header(ws);
      for (int r = 2; r <= ws.LastRowUsed().RowNumber(); r++)
      {
        var d = new ProgesiVariableDto();
        foreach (var (name, col) in hdr) Assign(d, name, ws.Cell(r, col).GetString());
        list.Add(d);
      }
    }
    static void ReadSheet(IXLWorksheet ws, List<ProgesiMetadataDto> list)
    {
      var hdr = Header(ws);
      for (int r = 2; r <= ws.LastRowUsed().RowNumber(); r++)
      {
        var d = new ProgesiMetadataDto();
        foreach (var (name, col) in hdr) Assign(d, name, ws.Cell(r, col).GetString());
        list.Add(d);
      }
    }
    static void ReadSheet(IXLWorksheet ws, List<ProgesiAxisVariableDto> list)
    {
      var hdr = Header(ws);
      for (int r = 2; r <= ws.LastRowUsed().RowNumber(); r++)
      {
        var d = new ProgesiAxisVariableDto();
        foreach (var (name, col) in hdr) Assign(d, name, ws.Cell(r, col).GetString());
        list.Add(d);
      }
    }

    static List<(string name, int col)> Header(IXLWorksheet ws)
    {
      var last = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
      var outp = new List<(string, int)>();
      for (int c = 1; c <= last; c++)
      {
        var name = ws.Cell(1, c).GetString().Trim();
        if (!string.IsNullOrEmpty(name)) outp.Add((name, c));
      }
      return outp;
    }

    static void WriteSheet<T>(IXLWorksheet ws, IEnumerable<T> items, string[] headers)
    {
      for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
      int r = 2;
      foreach (var it in items)
      {
        for (int i = 0; i < headers.Length; i++)
        {
          var v = it?.GetType().GetProperty(headers[i])?.GetValue(it, null) ?? "";
          ws.Cell(r, i + 1).Value = v?.ToString() ?? "";
        }
        r++;
      }
      ws.SheetView.FreezeRows(1);
      ws.Columns().AdjustToContents(1, Math.Min(headers.Length, 20));
    }

    static void Assign(ProgesiVariableDto t, string n, string v)
    {
      switch (n)
      {
        case "Id": t.Id = v; break;
        case "Hash": t.Hash = v; break;
        case "Name": t.Name = v; break;
        case "Value": t.Value = v; break;
        case "Unit": t.Unit = v; break;
        case "By": t.By = v; break;
        case "Ref": t.Ref = v; break;
        case "LastModifiedUtc": t.LastModifiedUtc = v; break;
      }
    }
    static void Assign(ProgesiMetadataDto t, string n, string v)
    {
      switch (n)
      {
        case "Id": t.Id = v; break;
        case "Hash": t.Hash = v; break;
        case "Info": t.Info = v; break;
        case "By": t.By = v; break;
        case "Ref": t.Ref = v; break;
        case "LastModifiedUtc": t.LastModifiedUtc = v; break;
      }
    }
    static void Assign(ProgesiAxisVariableDto t, string n, string v)
    {
      switch (n)
      {
        case "Id": t.Id = v; break;
        case "Hash": t.Hash = v; break;
        case "Name": t.Name = v; break;
        case "Unit": t.Unit = v; break;
        case "AxisRef": t.AxisRef = v; break;
        case "Stations": t.Stations = v; break;
        case "Values": t.Values = v; break;
        case "By": t.By = v; break;
        case "Ref": t.Ref = v; break;
        case "LastModifiedUtc": t.LastModifiedUtc = v; break;
      }
    }
  }
}
