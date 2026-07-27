using System.Diagnostics;
using FluentAssertions;
using ProgesiCore;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

public sealed class MetadataScaleStressTests
{
  private readonly ITestOutputHelper _output;
  private static readonly Random Rng = new(7);

  public MetadataScaleStressTests(ITestOutputHelper output) => _output = output;

  public static IEnumerable<object[]> StoreKinds() =>
    new[]
    {
      new object[] { "Sqlite" },
      new object[] { "Ef" }
    };

  [SkippableTheory]
  [MemberData(nameof(StoreKinds))]
  public async Task MetadataUpsertAndHashtagLookup_Stress(string kind)
  {
    StressTestGate.RequireEnabled();
    var n = StressTestGate.ScaleN();
    var sampleSize = Math.Min(50, n);

    using var store = kind == "Sqlite"
      ? StressMetadataStore.CreateSqlite()
      : StressMetadataStore.CreateEf();

    var repo = store.Repository;
    var hashtags = new string[n + 1];

    var sw = Stopwatch.StartNew();
    for (var i = 1; i <= n; i++)
    {
      var meta = ProgesiMetadata.Create($"user{i}", additionalInfo: $"info-{i}", id: i);
      await repo.UpsertAsync(meta);
      hashtags[i] = meta.Hashtag;
    }
    sw.Stop();
    StressTestGate.LogTiming(_output, $"Metadata upsert ({kind})", sw, n);

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
    _output.WriteLine($"Metadata hashtag lookup ({kind}): {sw.Elapsed.TotalMilliseconds:F0} ms, sample={sampleSize}, avg={avgMs:F3} ms/lookup");
  }
}
