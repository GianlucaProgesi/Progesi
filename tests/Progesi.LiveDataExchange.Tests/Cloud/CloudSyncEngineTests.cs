using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Progesi.LiveDataExchange.Cloud;
using Xunit;

namespace Progesi.LiveDataExchange.Tests.Cloud
{
  public sealed class CloudSyncEngineTests
  {
    private readonly CloudSyncEngine _engine = new CloudSyncEngine();

    [Fact]
    public async Task Unchanged_Object_Is_Skipped()
    {
      var sync = new InMemorySyncStateStore();
      var local = SnapshotWithVariable(1, "base");
      var cloud = SnapshotWithVariable(1, "base");
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal);

      result.VariablesApplied.Should().Be(0);
      result.Skipped.Should().Be(1);
      result.Conflicts.Should().BeEmpty();
      fakeCloud.UpsertedVariables.Should().BeEmpty();
    }

    [Fact]
    public async Task Push_Applies_Local_Only_Change()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = SnapshotWithVariable(1, "local-new");
      var cloud = SnapshotWithVariable(1, "base");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal);

      result.VariablesApplied.Should().Be(1);
      fakeCloud.UpsertedVariables.Should().HaveCount(1);
      fakeCloud.UpsertedVariables[0].ContentHash.Should().Be("local-new");
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().Be("local-new");
      result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Pull_Applies_Cloud_Only_Change()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = SnapshotWithVariable(1, "base");
      var cloud = SnapshotWithVariable(1, "cloud-new");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Pull,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal);

      result.VariablesApplied.Should().Be(1);
      fakeLocal.AppliedVariables.Should().HaveCount(1);
      fakeLocal.AppliedVariables[0].ContentHash.Should().Be("cloud-new");
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().Be("cloud-new");
    }

    [Fact]
    public async Task Both_Changed_Different_Hashes_Reports_Conflict()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = SnapshotWithVariable(1, "local-new");
      var cloud = SnapshotWithVariable(1, "cloud-new");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal);

      result.VariablesApplied.Should().Be(0);
      result.Conflicts.Should().HaveCount(1);
      result.Conflicts[0].LocalHash.Should().Be("local-new");
      result.Conflicts[0].CloudHash.Should().Be("cloud-new");
      fakeCloud.UpsertedVariables.Should().BeEmpty();
      fakeLocal.AppliedVariables.Should().BeEmpty();
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().Be("base");
    }

    [Fact]
    public async Task Converged_Local_Equals_Cloud_Updates_Base_Without_Apply()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = SnapshotWithVariable(1, "same");
      var cloud = SnapshotWithVariable(1, "same");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal);

      result.VariablesApplied.Should().Be(0);
      result.Conflicts.Should().BeEmpty();
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().Be("same");
      fakeCloud.UpsertedVariables.Should().BeEmpty();
    }

    private static CloudSnapshot SnapshotWithVariable(int id, string hash)
    {
      return new CloudSnapshot
      {
        Variables =
        {
          new CloudVariableRecord
          {
            Id = id,
            ContentHash = hash,
            Name = "Var" + id,
            ValueJson = "\"v\""
          }
        }
      };
    }
  }
}
