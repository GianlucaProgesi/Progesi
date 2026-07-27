using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProgesiCore;
using Progesi.Infrastructure.EF;
using Progesi.Infrastructure.EF.Internal;
using Progesi.Infrastructure.EF.Repositories;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

public sealed class EfRobustnessStressTests
{
  private readonly ITestOutputHelper _output;

  public EfRobustnessStressTests(ITestOutputHelper output) => _output = output;

  [SkippableFact]
  public async Task BatchSaveInTransaction_Stress()
  {
    StressTestGate.RequireEnabled();
    var n = Math.Min(StressTestGate.ScaleN(), 500);
    var cs = EfTestBootstrap.CreateTempFileConnectionString();

    try
    {
      var sw = Stopwatch.StartNew();
      await using (var ctx = ProgesiDbContextFactory.Create(cs, resetSchema: true))
      await using (var tx = await ctx.Database.BeginTransactionAsync())
      {
        for (var i = 1; i <= n; i++)
        {
          ctx.Variables.Add(new Progesi.Infrastructure.EF.Entities.VariableEntity
          {
            Id = i,
            Name = $"tx{i}",
            ValueType = "double",
            Value = i.ToString(),
            DependsJson = "[]",
            MetadataIdsJson = "[]",
            ContentHash = $"hash-tx-{i}"
          });
        }
        await ctx.SaveChangesAsync();
        await tx.CommitAsync();
      }
      sw.Stop();
      StressTestGate.LogTiming(_output, "EF batch transaction save", sw, n);

      var repo = new EfVariableRepository(cs, resetSchema: false);
      (await repo.GetAllAsync()).Count.Should().Be(n);
    }
    finally
    {
      EfTestBootstrap.TryDeleteFile(cs);
    }
  }

  [SkippableFact]
  public async Task ReopenContextReadBack_Stress()
  {
    StressTestGate.RequireEnabled();
    var n = Math.Min(StressTestGate.ScaleN(), 200);
    var cs = EfTestBootstrap.CreateTempFileConnectionString();

    try
    {
      var sw = Stopwatch.StartNew();
      {
        var repo = new EfVariableRepository(cs, resetSchema: true);
        for (var i = 1; i <= n; i++)
          await repo.SaveAsync(new ProgesiVariable(i, $"reopen{i}", i));
      }

      var repo2 = new EfVariableRepository(cs, resetSchema: false);
      var all = await repo2.GetAllAsync();
      sw.Stop();
      StressTestGate.LogTiming(_output, "EF reopen read-back", sw, n);

      all.Count.Should().Be(n);
      all.Select(v => v.Id).Should().BeEquivalentTo(Enumerable.Range(1, n));
    }
    finally
    {
      EfTestBootstrap.TryDeleteFile(cs);
    }
  }

  [SkippableFact]
  public async Task LargeValuePayload_Stress()
  {
    StressTestGate.RequireEnabled();
    var cs = EfTestBootstrap.CreateTempFileConnectionString();
    var payload = new string('X', 256 * 1024);

    try
    {
      var sw = Stopwatch.StartNew();
      var repo = new EfVariableRepository(cs, resetSchema: true);
      await repo.SaveAsync(new ProgesiVariable(1, "large", payload));
      var loaded = await repo.GetByIdAsync(1);
      sw.Stop();
      StressTestGate.LogTiming(_output, "EF large payload round-trip", sw, 1);

      loaded.Should().NotBeNull();
      loaded!.Value.Should().Be(payload);
    }
    finally
    {
      EfTestBootstrap.TryDeleteFile(cs);
    }
  }

  [SkippableFact]
  public async Task ParallelSaves_Stress()
  {
    StressTestGate.RequireEnabled();
    var n = Math.Min(StressTestGate.ScaleN(), 200);
    var cs = EfTestBootstrap.CreateTempFileConnectionString();

    try
    {
      _ = new EfVariableRepository(cs, resetSchema: true);

      var sw = Stopwatch.StartNew();
      var tasks = Enumerable.Range(1, n).Select(i => Task.Run(async () =>
      {
        var repo = new EfVariableRepository(cs, resetSchema: false);
        await repo.SaveAsync(new ProgesiVariable(i, $"par{i}", i));
      }));
      await Task.WhenAll(tasks);

      var verify = new EfVariableRepository(cs, resetSchema: false);
      var all = await verify.GetAllAsync();
      sw.Stop();
      StressTestGate.LogTiming(_output, "EF parallel saves", sw, n);

      all.Count.Should().Be(n);
      all.Select(v => v.Id).Should().OnlyHaveUniqueItems();
    }
    finally
    {
      EfTestBootstrap.TryDeleteFile(cs);
    }
  }
}
