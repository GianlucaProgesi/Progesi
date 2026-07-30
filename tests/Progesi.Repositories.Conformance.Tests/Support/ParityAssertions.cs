using FluentAssertions;
using ProgesiCore;

namespace Progesi.Repositories.Conformance.Tests.Support;

internal static class ParityAssertions
{
  public static void VariablesShouldMatch(ProgesiVariable sqlite, ProgesiVariable ef, ProgesiVariable original)
  {
    sqlite.Should().NotBeNull();
    ef.Should().NotBeNull();

    sqlite!.Name.Should().Be(ef!.Name);
    sqlite.Value.Should().Be(ef.Value);
    sqlite.DependsFrom.Should().BeEquivalentTo(ef.DependsFrom);
    sqlite.MetadataIds.Should().Equal(ef.MetadataIds);

    ProgesiHash.Compute(sqlite).Should().Be(ProgesiHash.Compute(ef));
    ProgesiHash.Compute(sqlite).Should().Be(ProgesiHash.Compute(original));
    sqlite.Hashtag.Should().Be(ef.Hashtag);
  }

  public static void ClustersShouldMatch(
      ProgesiVariableCluster sqlite,
      ProgesiVariableCluster ef,
      ProgesiVariableCluster original)
  {
    sqlite.Should().NotBeNull();
    ef.Should().NotBeNull();

    sqlite!.Id.Should().Be(ef!.Id);
    sqlite.Name.Should().Be(ef.Name);
    sqlite.Description.Should().Be(ef.Description);
    sqlite.ProgesiVariableIds.Should().Equal(ef.ProgesiVariableIds);
    sqlite.Hashtag.Should().Be(ef.Hashtag);

    ProgesiHash.Compute(sqlite).Should().Be(ProgesiHash.Compute(ef));
    ProgesiHash.Compute(sqlite).Should().Be(ProgesiHash.Compute(original));
  }

  public static void AxisShouldMatch(
      ProgesiAxisVariable sqlite,
      ProgesiAxisVariable ef,
      ProgesiAxisVariable original)
  {
    sqlite.Should().NotBeNull();
    ef.Should().NotBeNull();

    sqlite!.Id.Should().Be(ef!.Id);
    sqlite.AxisName.Should().Be(ef.AxisName);
    sqlite.Name.Should().Be(ef.Name);
    sqlite.ValueTypeKey.Should().Be(ef.ValueTypeKey);
    sqlite.AxisLength.Should().Be(ef.AxisLength);
    sqlite.CurvePayload.Should().Be(ef.CurvePayload);
    sqlite.Mode.Should().Be(ef.Mode);
    sqlite.KeyPoints.Should().Equal(ef.KeyPoints);
    sqlite.RuleId.Should().Be(ef.RuleId);
    sqlite.Hashtag.Should().Be(ef.Hashtag);

    AssertFunctionRefEquivalent(sqlite.FunctionRef, ef.FunctionRef);

    var sqliteStations = sqlite.EnumerateAll()
        .OrderBy(t => t.positionNormalized)
        .ThenBy(t => t.variableId)
        .Select(t => (t.positionNormalized, t.variableId))
        .ToArray();
    var efStations = ef.EnumerateAll()
        .OrderBy(t => t.positionNormalized)
        .ThenBy(t => t.variableId)
        .Select(t => (t.positionNormalized, t.variableId))
        .ToArray();
    sqliteStations.Should().Equal(efStations);

    ProgesiHash.Compute(sqlite).Should().Be(ProgesiHash.Compute(ef));
    ProgesiHash.Compute(sqlite).Should().Be(ProgesiHash.Compute(original));
  }

  private static void AssertFunctionRefEquivalent(ProgesiFunctionRef sqlite, ProgesiFunctionRef ef)
  {
    sqlite.FunctionId.Should().Be(ef.FunctionId);
    sqlite.FunctionHashtag.Should().Be(ef.FunctionHashtag);

    if (sqlite.Embedded == null && ef.Embedded == null)
      return;

    sqlite.Embedded.Should().NotBeNull();
    ef.Embedded.Should().NotBeNull();
    ProgesiHash.Compute(sqlite.Embedded!).Should().Be(ProgesiHash.Compute(ef.Embedded!));
  }

  public static void MetadataShouldMatch(ProgesiMetadata? sqlite, ProgesiMetadata? ef, ProgesiMetadata original)
  {
    sqlite.Should().NotBeNull();
    ef.Should().NotBeNull();

    sqlite!.CreatedBy.Should().Be(ef!.CreatedBy);
    sqlite.AdditionalInfo.Should().Be(ef.AdditionalInfo);
    sqlite.Hashtag.Should().Be(ef.Hashtag);
    sqlite.Hashtag.Should().Be(original.Hashtag);

    var sqliteRefs = (sqlite.References ?? Array.Empty<Uri>()).Select(u => u.ToString()).OrderBy(s => s).ToArray();
    var efRefs = (ef.References ?? Array.Empty<Uri>()).Select(u => u.ToString()).OrderBy(s => s).ToArray();
    sqliteRefs.Should().Equal(efRefs);

    sqlite.Snips.Should().HaveCount(ef.Snips?.Count ?? 0);
    if (sqlite.Snips != null && ef.Snips != null)
    {
      for (int i = 0; i < sqlite.Snips.Count; i++)
      {
        sqlite.Snips[i].MimeType.Should().Be(ef.Snips[i].MimeType);
        sqlite.Snips[i].Caption.Should().Be(ef.Snips[i].Caption);
        sqlite.Snips[i].Content.Should().Equal(ef.Snips[i].Content);
      }
    }
  }
}
