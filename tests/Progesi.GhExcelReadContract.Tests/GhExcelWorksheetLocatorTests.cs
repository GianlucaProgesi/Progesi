using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelWorksheetLocatorTests
  {
    [Fact]
    public void TryGetWorksheet_Matches_Case_Insensitive_And_Alias()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        wb.Worksheets.Add("progesivariables");
        wb.SaveAs(file.Path);
      }

      using (var wb = new XLWorkbook(file.Path))
      {
        var sheet = GhExcelWorksheetLocator.TryGetWorksheet(
          wb,
          GhExcelSheetNames.Variables,
          GhExcelSheetNames.VariablesAlias);

        sheet.Should().NotBeNull();
        sheet.Name.Should().Be("progesivariables");
      }
    }

    [Fact]
    public void TryGetWorksheet_Returns_Null_When_No_Match()
    {
      using var wb = new XLWorkbook();
      wb.Worksheets.Add("Other");

      GhExcelWorksheetLocator.TryGetWorksheet(wb, GhExcelSheetNames.Metadata).Should().BeNull();
    }
  }
}
