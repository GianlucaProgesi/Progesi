using System.Diagnostics;
using FluentAssertions;
using ProgesiCore;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

public sealed class VariableScaleCrudStressTests
{
  private readonly ITestOutputHelper _output;

  public VariableScaleCrudStressTests(ITestOutputHelper output) => _output = output;

  public static IEnumerable<object[]> StoreKinds() =>
    new[]
    {
      new object[] { VariableStoreKind.InMemory },
      new object[] { VariableStoreKind.Sqlite },
      new object[] { VariableStoreKind.Ef }
    };

  [SkippableTheory]
  [MemberData(nameof(StoreKinds))]
  public async Task ScaleCrud_Stress(VariableStoreKind kind)
  {
    StressTestGate.RequireEnabled();
    var n = StressTestGate.ScaleN();

    using var store = StressVariableStore.Create(kind);
    var repo = store.Repository;
    var sw = Stopwatch.StartNew();

    for (var i = 1; i <= n; i++)
      await repo.SaveAsync(new ProgesiVariable(i, $"v{i}", i * 1.5));

    sw.Stop();
    StressTestGate.LogTiming(_output, $"ScaleCrud save ({kind})", sw, n);

    var all = await repo.GetAllAsync();
    all.Count.Should().Be(n);

    var spotCount = Math.Min(100, n);
    sw.Restart();
    for (var id = 1; id <= spotCount; id++)
    {
      var loaded = await repo.GetByIdAsync(id);
      loaded.Should().NotBeNull();
    }
    sw.Stop();
    StressTestGate.LogTiming(_output, $"ScaleCrud spot-read ({kind})", sw, spotCount);

    sw.Restart();
    var deleted = await repo.DeleteManyAsync(Enumerable.Range(1, n));
    sw.Stop();
    deleted.Should().Be(n);
    StressTestGate.LogTiming(_output, $"ScaleCrud delete ({kind})", sw, n);

    (await repo.GetAllAsync()).Should().BeEmpty();
  }
}
