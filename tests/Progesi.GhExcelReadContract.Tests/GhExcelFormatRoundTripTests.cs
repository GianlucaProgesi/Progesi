using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using FluentAssertions;
using Progesi.GhExcelReadContract;
using Progesi.GhExcelReadContract.Tests.Support;
using ProgesiCore;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelFormatRoundTripTests
  {
    private static void AssertModelsEquivalent(CanonicalExchangeModel expected, CanonicalExchangeModel actual)
    {
      actual.Variables.Should().HaveCount(expected.Variables.Count);
      foreach (var exp in expected.Variables)
      {
        var act = actual.Variables.Single(v => v.Id == exp.Id);
        act.Hash.Should().Be(exp.Hash);
        act.Name.Should().Be(exp.Name);
        act.Value.Should().Be(exp.Value);
        act.ValC.Should().Be(exp.ValC);
        act.MetaIds.Should().Equal(exp.MetaIds);
        act.Depends.Should().Equal(exp.Depends);
        act.Assumption.Should().Be(exp.Assumption);

        if (!string.IsNullOrWhiteSpace(exp.ObjectPayloadJson))
        {
          GhExcelObjectSheet.TryParseObjectMarker(act.Value, out var objectType).Should().BeTrue();
          objectType.Should().Be(exp.ObjectType);
          act.ObjectPayloadJson.Should().Be(exp.ObjectPayloadJson);
          act.ObjectPayloadJson.Length.Should().BeGreaterThan(GhExcelObjectSheet.DefaultMaxChunkLength);
        }
      }

      actual.Metadata.Should().HaveCount(expected.Metadata.Count);
      foreach (var exp in expected.Metadata)
      {
        var act = actual.Metadata.Single(m => m.Id == exp.Id);
        act.Hash.Should().Be(exp.Hash);
        act.By.Should().Be(exp.By);
        act.Description.Should().Be(exp.Description);
        act.Refs.Should().Equal(exp.Refs);
        act.LastModified.Should().Be(exp.LastModified);
      }

      actual.Clusters.Should().HaveCount(expected.Clusters.Count);
      foreach (var exp in expected.Clusters)
      {
        var act = actual.Clusters.Single(c => c.Id == exp.Id);
        act.Name.Should().Be(exp.Name);
        act.Description.Should().Be(exp.Description);
        act.VariableIds.Should().Equal(exp.VariableIds);
        if (!string.IsNullOrWhiteSpace(exp.Hash))
          act.Hash.Should().Be(exp.Hash);
      }
    }

    [Fact]
    public void CanonicalWorkbook_StandardHeaders_RoundTrips_All_Sheets()
    {
      var expected = CanonicalExchangeModel.CreateFixedModel();
      using var file = new ExcelTestFile();
      CanonicalExchangeWorkbookBuilder.Write(file.Path, expected);

      using var wb = new XLWorkbook(file.Path);
      var actual = GhExcelWorkbookReader.Read(wb);

      AssertModelsEquivalent(expected, actual);
    }

    [Fact]
    public void CanonicalWorkbook_AliasHeaders_RoundTrips_All_Sheets()
    {
      var expected = CanonicalExchangeModel.CreateFixedModel();
      using var file = new ExcelTestFile();
      CanonicalExchangeWorkbookBuilder.Write(
        file.Path,
        expected,
        new CanonicalExchangeWorkbookBuilder.HeaderStyle { UseAliasHeaders = true });

      using var wb = new XLWorkbook(file.Path);
      var actual = GhExcelWorkbookReader.Read(wb);

      AssertModelsEquivalent(expected, actual);
    }

    [Fact]
    public void CanonicalWorkbook_CustomAliasMapJson_RoundTrips_Variables_And_Metadata()
    {
      var expected = CanonicalExchangeModel.CreateFixedModel();
      using var file = new ExcelTestFile();

      using (var wb = new XLWorkbook())
      {
        var wsV = wb.Worksheets.Add(GhExcelSheetNames.Variables);
        wsV.Cell(1, 1).Value = "IDVAR";
        wsV.Cell(1, 2).Value = "SHA";
        wsV.Cell(1, 3).Value = "NOME";
        wsV.Cell(1, 4).Value = "VALORE";
        wsV.Cell(1, 5).Value = "CANONICAL";
        wsV.Cell(1, 6).Value = "META_ID";
        wsV.Cell(1, 7).Value = "PARENT_IDS";
        wsV.Cell(1, 8).Value = "ASSUME";
        wsV.Cell(2, 1).Value = 1;
        wsV.Cell(2, 2).Value = "var-hash-1";
        wsV.Cell(2, 3).Value = "Span";
        wsV.Cell(2, 4).Value = "12.5";
        wsV.Cell(2, 5).Value = "12.5";
        wsV.Cell(2, 6).Value = "1";
        wsV.Cell(2, 7).Value = "";
        wsV.Cell(2, 8).Value = 0;

        var wsM = wb.Worksheets.Add(GhExcelSheetNames.Metadata);
        wsM.Cell(1, 1).Value = "METAID";
        wsM.Cell(1, 2).Value = "DIGEST";
        wsM.Cell(1, 3).Value = "CREATEDBY";
        wsM.Cell(1, 4).Value = "NOTE";
        wsM.Cell(1, 5).Value = "URLS";
        wsM.Cell(1, 6).Value = "UPDATED";
        wsM.Cell(2, 1).Value = 1;
        wsM.Cell(2, 2).Value = "meta-hash-1";
        wsM.Cell(2, 3).Value = "eng";
        wsM.Cell(2, 4).Value = "Primary metadata";
        wsM.Cell(2, 5).Value = "https://example.com/a|https://example.com/b";
        wsM.Cell(2, 6).Value = "2026-07-27T10:00:00Z";

        wb.SaveAs(file.Path);
      }

      const string mapJson = @"{
        ""Variables"": {
          ""ID"": [""IDVAR""],
          ""HASH"": [""SHA""],
          ""NAME"": [""NOME""],
          ""VALUE"": [""VALORE""],
          ""VALC"": [""CANONICAL""],
          ""METAID"": [""META_ID""],
          ""DEPENDS"": [""PARENT_IDS""],
          ""ASSUMPTION"": [""ASSUME""]
        },
        ""Metadata"": {
          ""ID"": [""METAID""],
          ""HASH"": [""DIGEST""],
          ""BY"": [""CREATEDBY""],
          ""DESCRIPTION"": [""NOTE""],
          ""REFS"": [""URLS""],
          ""LM"": [""UPDATED""]
        }
      }";

      using var wbRead = new XLWorkbook(file.Path);
      var actual = GhExcelWorkbookReader.Read(wbRead, mapJson);

      actual.Variables.Should().ContainSingle();
      actual.Variables[0].Name.Should().Be("Span");
      actual.Variables[0].MetaIds.Should().Equal(1);
      actual.Metadata.Should().ContainSingle();
      actual.Metadata[0].By.Should().Be("eng");
      actual.Metadata[0].Refs.Should().HaveCount(2);
    }

    [Fact]
    public void GeometryVariable_MultiChunk_Reassembles_From_VariableObjects_Sheet()
    {
      var expected = CanonicalExchangeModel.CreateFixedModel();
      var geometry = expected.Variables.Single(v => v.Id == 3);

      using var file = new ExcelTestFile();
      CanonicalExchangeWorkbookBuilder.Write(file.Path, expected);

      var chunks = GhExcelWorkbookReader.ReadObjectChunkRows(new XLWorkbook(file.Path));
      chunks.Where(c => c.VarId == 3).Should().HaveCountGreaterThan(1);
      chunks.Where(c => c.VarId == 3).Select(c => c.ChunkCount).Distinct().Should().Equal(geometry.ObjectPayloadJson.Length <= GhExcelObjectSheet.DefaultMaxChunkLength ? 1 : 2);

      GhExcelObjectSheet.TryReassemblePayload(chunks, 3, out var payload, out var type, out var error)
        .Should().BeTrue(error);

      payload.Should().Be(geometry.ObjectPayloadJson);
      type.Should().Be(geometry.ObjectType);
      geometry.Value.Should().Be(GhExcelObjectSheet.BuildObjectMarker(geometry.ObjectType));
    }

    [Fact]
    public void ClusterRows_Parse_VariableIds_And_Domain_Hashtag_When_Hash_Missing()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add(GhExcelSheetNames.Clusters);
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Hash";
        ws.Cell(1, 3).Value = "Name";
        ws.Cell(1, 4).Value = "Description";
        ws.Cell(1, 5).Value = "VariableIds";
        ws.Cell(2, 1).Value = 5;
        ws.Cell(2, 2).Value = "";
        ws.Cell(2, 3).Value = "C5";
        ws.Cell(2, 4).Value = "desc";
        ws.Cell(2, 5).Value = "3,1,2";
        wb.SaveAs(file.Path);
      }

      using var wbRead = new XLWorkbook(file.Path);
      var clusters = GhExcelWorkbookReader.ReadClusters(wbRead, GhExcelAliasMaps.CreateDefaultClusterAliases());

      clusters.Should().ContainSingle();
      clusters[0].VariableIds.Should().Equal(1, 2, 3);
      var expected = ProgesiVariableCluster.Rehydrate(5, "C5", new[] { 1, 2, 3 }, "desc");
      clusters[0].Hash.Should().Be(ProgesiHash.Compute(expected));
    }

    [Fact]
    public void GoldenFixture_Reads_Back_Canonical_Model()
    {
      var repoRoot = FindRepoRoot();
      var goldenPath = Path.Combine(repoRoot, "validation", "dataexchange", "golden", "r2c0-canonical-model.xlsx");
      File.Exists(goldenPath).Should().BeTrue("golden fixture must be committed at validation/dataexchange/golden/r2c0-canonical-model.xlsx");

      var expected = CanonicalExchangeModel.CreateFixedModel();
      using var wb = new XLWorkbook(goldenPath);
      var actual = GhExcelWorkbookReader.Read(wb);

      AssertModelsEquivalent(expected, actual);
    }

    [Fact]
    public void Generate_Golden_Fixture_File_When_Enabled()
    {
      if (Environment.GetEnvironmentVariable("PROGESI_WRITE_GOLDEN") != "1")
        return;

      var repoRoot = FindRepoRoot();
      var goldenDir = Path.Combine(repoRoot, "validation", "dataexchange", "golden");
      Directory.CreateDirectory(goldenDir);
      var goldenPath = Path.Combine(goldenDir, "r2c0-canonical-model.xlsx");

      CanonicalExchangeWorkbookBuilder.Write(goldenPath, CanonicalExchangeModel.CreateFixedModel());
      File.Exists(goldenPath).Should().BeTrue();
    }

    private static string FindRepoRoot()
    {
      var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
      while (dir != null)
      {
        if (File.Exists(Path.Combine(dir.FullName, "Progesi.sln")))
          return dir.FullName;
        dir = dir.Parent;
      }

      throw new InvalidOperationException("Could not locate repository root (Progesi.sln).");
    }
  }
}
