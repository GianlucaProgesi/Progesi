using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelVariableValueWorkbookTests
  {
    [Fact]
    public void Primitive_Value_Written_To_Workbook_RoundTrips_For_Import_Check()
    {
      using var wb = new XLWorkbook();
      var ws = wb.Worksheets.Add(GhExcelSheetNames.Variables);
      ws.Cell(1, 1).Value = "Name";
      ws.Cell(1, 2).Value = "Value";
      ws.Cell(2, 1).Value = "Width";
      ws.Cell(2, 2).Value = GhExcelVariableValueSupport.FormatExportValue("string", "12.5", "12.5");

      var exported = ws.Cell(2, 2).GetString();
      GhExcelVariableValueSupport.IsImportSupported(exported, out var kind, out _)
        .Should().BeTrue();
      kind.Should().Be(GhExcelVariableValueKind.Primitive);
      exported.Should().Be("12.5");
    }

    [Fact]
    public void Unsupported_Export_Marker_Is_Rejected_On_Import_Check()
    {
      var marker = GhExcelVariableValueSupport.FormatExportValue(
        "Rhino.Geometry.Point3d, RhinoCommon",
        "{\"X\":1}",
        "1");

      GhExcelVariableValueSupport.IsImportSupported(marker, out var kind, out var detail)
        .Should().BeFalse();
      kind.Should().Be(GhExcelVariableValueKind.UnsupportedMarker);
      detail.Should().Be("RhinoObject");
    }

    [Fact]
    public void Referenced_Brep_Export_Marker_Is_Rejected_On_Import_Check()
    {
      var marker = GhExcelVariableValueSupport.FormatExportValue(
        "string",
        "Referenced Brep",
        "Referenced Brep");

      marker.Should().Be("@UNSUPPORTED:RhinoObject");
      GhExcelVariableValueSupport.IsImportSupported(marker, out var kind, out var detail)
        .Should().BeFalse();
      kind.Should().Be(GhExcelVariableValueKind.UnsupportedMarker);
      detail.Should().Be("RhinoObject");
    }

    [Fact]
    public void Legacy_Referenced_Display_String_Is_Rejected_On_Import_Check()
    {
      GhExcelVariableValueSupport.IsImportSupported("Referenced Planar Curve", out var kind, out var detail)
        .Should().BeFalse();
      kind.Should().Be(GhExcelVariableValueKind.ReferencedObjectDisplayString);
      detail.Should().Be("RhinoObject");
    }
  }
}
