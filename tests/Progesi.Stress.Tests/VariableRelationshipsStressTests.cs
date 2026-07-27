using System.Diagnostics;
using FluentAssertions;
using ProgesiCore;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

public sealed class VariableRelationshipsStressTests
{
  private readonly ITestOutputHelper _output;

  public VariableRelationshipsStressTests(ITestOutputHelper output) => _output = output;

  public static IEnumerable<object[]> StoreKinds() =>
    new[]
    {
      new object[] { VariableStoreKind.InMemory },
      new object[] { VariableStoreKind.Sqlite },
      new object[] { VariableStoreKind.Ef }
    };

  [SkippableTheory]
  [MemberData(nameof(StoreKinds))]
  public async Task LargeMetadataIdsAndDependsFrom_RoundTrip_Stress(VariableStoreKind kind)
  {
    StressTestGate.RequireEnabled();
    var n = Math.Min(StressTestGate.ScaleN(), 500);

    var depends = Enumerable.Range(1, n).ToArray();
    var metadataIds = Enumerable.Range(10001, n).ToArray();

    using var store = StressVariableStore.Create(kind);
    var repo = store.Repository;

    var original = new ProgesiVariable(999, "rel", 1.0, depends, metadataIds);

    var sw = Stopwatch.StartNew();
    await repo.SaveAsync(original);
    var loaded = await repo.GetByIdAsync(999);
    sw.Stop();
    StressTestGate.LogTiming(_output, $"Relationships round-trip ({kind})", sw, 1);

    loaded.Should().NotBeNull();
    loaded!.DependsFrom.Should().BeEquivalentTo(depends);
    loaded.MetadataIds.Should().BeEquivalentTo(metadataIds);
  }
}
