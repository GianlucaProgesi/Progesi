using System.Diagnostics;
using FluentAssertions;
using ProgesiCore;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

public sealed class VariableDedupHashtagStressTests
{
  private readonly ITestOutputHelper _output;

  public VariableDedupHashtagStressTests(ITestOutputHelper output) => _output = output;

  public static IEnumerable<object[]> StoreKinds() =>
    new[]
    {
      new object[] { VariableStoreKind.InMemory },
      new object[] { VariableStoreKind.Sqlite },
      new object[] { VariableStoreKind.Ef }
    };

  [SkippableTheory]
  [MemberData(nameof(StoreKinds))]
  public async Task DedupAndGetByHashtag_Stress(VariableStoreKind kind)
  {
    StressTestGate.RequireEnabled();
    var n = StressTestGate.ScaleN();

    using var store = StressVariableStore.Create(kind);
    var repo = store.Repository;
    var canonical = new ProgesiVariable(1, "dup", 42.0);
    var expectedHashtag = canonical.Hashtag;
    ProgesiVariable? survivor = null;

    var sw = Stopwatch.StartNew();
    for (var i = 1; i <= n; i++)
    {
      var saved = await repo.SaveAsync(new ProgesiVariable(i, "dup", 42.0));
      if (i == 1)
        survivor = saved;
    }
    sw.Stop();
    StressTestGate.LogTiming(_output, $"Dedup save ({kind})", sw, n);

    var all = await repo.GetAllAsync();
    switch (kind)
    {
      case VariableStoreKind.InMemory:
        all.Count.Should().Be(n, "InMemory stores by id without content dedup");
        survivor = all.First();
        break;
      case VariableStoreKind.Sqlite:
      case VariableStoreKind.Ef:
        all.Count.Should().Be(1, "SQLite/EF dedup identical content to one row");
        survivor.Should().NotBeNull();
        survivor!.Id.Should().Be(1);
        break;
    }

    sw.Restart();
    var byTag = await repo.GetByHashtagAsync(expectedHashtag);
    sw.Stop();
    StressTestGate.LogTiming(_output, $"Dedup GetByHashtag ({kind})", sw, 1);

    byTag.Should().NotBeNull();
    byTag!.Hashtag.Should().Be(expectedHashtag);
    byTag.Id.Should().Be(survivor!.Id);
  }
}
