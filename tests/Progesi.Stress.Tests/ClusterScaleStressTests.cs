using System.Diagnostics;
using FluentAssertions;
using ProgesiCore;
using ProgesiCore.Services;
using ProgesiRepositories.InMemory;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

public sealed class ClusterScaleStressTests
{
  private readonly ITestOutputHelper _output;

  public ClusterScaleStressTests(ITestOutputHelper output) => _output = output;

  [SkippableFact]
  public async Task ClusterCreateDedupAndCascadeRemove_Stress()
  {
    StressTestGate.RequireEnabled();
    var n = StressTestGate.ScaleN();

    var varRepo = new InMemoryVariableRepository();
    var clusterRepo = new InMemoryVariableClusterRepository();
    var service = new ClusterService(clusterRepo, varRepo);

    var sw = Stopwatch.StartNew();
    for (var i = 1; i <= n; i++)
      await varRepo.SaveAsync(new ProgesiVariable(i, $"c{i}", i));
    sw.Stop();
    StressTestGate.LogTiming(_output, "Cluster seed variables", sw, n);

    var memberIds = Enumerable.Range(1, n).ToArray();

    sw.Restart();
    var first = await service.CreateOrGetClusterAsync("StressCluster", memberIds, "stress");
    sw.Stop();
    StressTestGate.LogTiming(_output, "Cluster create", sw, n);

    first.Should().NotBeNull();
    first!.ProgesiVariableIds.Should().BeEquivalentTo(memberIds);

    sw.Restart();
    var second = await service.CreateOrGetClusterAsync("StressCluster", memberIds.Reverse().ToArray(), "stress");
    sw.Stop();
    StressTestGate.LogTiming(_output, "Cluster re-create (dedup)", sw, 1);

    second.Id.Should().Be(first.Id);
    (await service.GetAllAsync()).Count.Should().Be(1);

    var removeId = memberIds[^1];
    sw.Restart();
    var affected = await service.CascadeRemoveVariableFromClustersAsync(removeId);
    sw.Stop();
    StressTestGate.LogTiming(_output, "Cluster cascade-remove", sw, 1);

    affected.Should().Be(1);
    var reloaded = await service.GetByIdAsync(first.Id);
    reloaded.Should().NotBeNull();
    reloaded!.ProgesiVariableIds.Should().NotContain(removeId);
    reloaded.ProgesiVariableIds.Count.Should().Be(n - 1);
  }
}
