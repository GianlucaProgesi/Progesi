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
