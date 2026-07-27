using System.Diagnostics;
using FluentAssertions;
using ProgesiCore;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

public sealed class VariableHashtagLookupStressTests
{
  private readonly ITestOutputHelper _output;
  private static readonly Random Rng = new(42);

  public VariableHashtagLookupStressTests(ITestOutputHelper output) => _output = output;

  public static IEnumerable<object[]> StoreKinds() =>
    new[]
    {
      new object[] { VariableStoreKind.InMemory },
      new object[] { VariableStoreKind.Sqlite },
      new object[] { VariableStoreKind.Ef }
    };

  [SkippableTheory]
  [MemberData(nameof(StoreKinds))]
  public async Task HashtagLookupAtScale_Stress(VariableStoreKind kind)
  {
    StressTestGate.RequireEnabled();
    var n = StressTestGate.ScaleN();
    var sampleSize = Math.Min(100, n);

    using var store = StressVariableStore.Create(kind);
    var repo = store.Repository;
    var hashtags = new string[n + 1];

    var sw = Stopwatch.StartNew();
    for (var i = 1; i <= n; i++)
    {
      var v = new ProgesiVariable(i, $"v{i}", i);
      await repo.SaveAsync(v);
      hashtags[i] = v.Hashtag;
    }
    sw.Stop();
    StressTestGate.LogTiming(_output, $"HashtagLookup seed ({kind})", sw, n);

    var sampleIds = Enumerable.Range(1, n).OrderBy(_ => Rng.Next()).Take(sampleSize).ToArray();

    sw.Restart();
    foreach (var id in sampleIds)
    {
      var found = await repo.GetByHashtagAsync(hashtags[id]);
      found.Should().NotBeNull();
      found!.Id.Should().Be(id);
    }
    sw.Stop();

    var avgMs = sampleSize > 0 ? sw.Elapsed.TotalMilliseconds / sampleSize : 0;
    _output.WriteLine($"HashtagLookup sample ({kind}): {sw.Elapsed.TotalMilliseconds:F0} ms total, sample={sampleSize}, avg={avgMs:F3} ms/lookup");
  }
}
