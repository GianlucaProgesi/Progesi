using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Progesi.LiveDataExchange.Cloud;
using Xunit;

namespace Progesi.LiveDataExchange.Tests.Cloud
{
  public sealed class CloudSyncDeletionTests
  {
    private readonly CloudSyncEngine _engine = new CloudSyncEngine();

    [Fact]
    public async Task PropagateDeletions_False_Ignores_Local_Delete()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = new CloudSnapshot();
      var cloud = SnapshotWithVariable(1, "base");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal,
          propagateDeletions: false);

      result.VariablesDeleted.Should().Be(0);
      fakeCloud.DeletedVariables.Should().BeEmpty();
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().Be("base");
    }

    [Fact]
    public async Task Push_Local_Delete_Cloud_Unchanged_Deletes_Cloud_And_Prunes_Base()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = new CloudSnapshot();
      var cloud = SnapshotWithVariable(1, "base");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal,
          propagateDeletions: true);

      result.VariablesDeleted.Should().Be(1);
      fakeCloud.DeletedVariables.Should().Equal(1);
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().BeEmpty();
      result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Pull_Cloud_Delete_Local_Unchanged_Deletes_Local_And_Prunes_Base()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = SnapshotWithVariable(1, "base");
      var cloud = new CloudSnapshot();
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();
      fakeLocal.Snapshot.Variables.Add(local.Variables[0]);

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Pull,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal,
          propagateDeletions: true);

      result.VariablesDeleted.Should().Be(1);
      fakeLocal.DeletedVariables.Should().Equal(1);
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().BeEmpty();
    }

    [Fact]
    public async Task Push_Local_Delete_Cloud_Edited_Reports_DeleteEdit_Conflict()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = new CloudSnapshot();
      var cloud = SnapshotWithVariable(1, "cloud-edited");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal,
          propagateDeletions: true);

      result.VariablesDeleted.Should().Be(0);
      fakeCloud.DeletedVariables.Should().BeEmpty();
      result.Conflicts.Should().HaveCount(1);
      result.Conflicts[0].Kind.Should().Be(CloudSyncConflictKind.DeleteEdit);
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().Be("base");
    }

    [Fact]
    public async Task Pull_Cloud_Delete_Local_Edited_Reports_DeleteEdit_Conflict()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = SnapshotWithVariable(1, "local-edited");
      var cloud = new CloudSnapshot();
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Pull,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal,
          propagateDeletions: true);

      result.VariablesDeleted.Should().Be(0);
      fakeLocal.DeletedVariables.Should().BeEmpty();
      result.Conflicts.Should().HaveCount(1);
      result.Conflicts[0].Kind.Should().Be(CloudSyncConflictKind.DeleteEdit);
    }

    [Fact]
    public async Task Deleted_Both_Sides_Prunes_Base_Without_Error()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          new CloudSnapshot(),
          new CloudSnapshot(),
          sync,
          new FakeCloudClient(),
          new FakeLocalApplier(),
          propagateDeletions: true);

      result.Conflicts.Should().BeEmpty();
      result.VariablesDeleted.Should().Be(0);
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().BeEmpty();
      result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task Pull_Cloud_Delete_Skips_Other_Side_Only_Deletion()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Variable, 1, "base");

      var local = SnapshotWithVariable(1, "base");
      var cloud = new CloudSnapshot();
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal,
          propagateDeletions: true);

      result.VariablesDeleted.Should().Be(0);
      fakeLocal.DeletedVariables.Should().BeEmpty();
      sync.GetLastSyncedHash(CloudSyncObjectType.Variable, 1).Should().Be("base");
    }

    [Fact]
    public async Task Push_Local_Delete_Skips_Other_Side_Only_Deletion_On_Pull_Direction()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Metadata, 2, "meta-base");

      var local = new CloudSnapshot();
      var cloud = SnapshotWithMetadata(2, "meta-base");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };
      var fakeLocal = new FakeLocalApplier();

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Pull,
          local,
          cloud,
          sync,
          fakeCloud,
          fakeLocal,
          propagateDeletions: true);

      result.MetadataDeleted.Should().Be(0);
      fakeCloud.DeletedMetadata.Should().BeEmpty();
      sync.GetLastSyncedHash(CloudSyncObjectType.Metadata, 2).Should().Be("meta-base");
    }

    [Fact]
    public async Task Push_Cluster_Local_Delete_Propagates_To_Cloud()
    {
      var sync = new InMemorySyncStateStore();
      sync.SetLastSyncedHash(CloudSyncObjectType.Cluster, 3, "cluster-base");

      var local = new CloudSnapshot();
      var cloud = SnapshotWithCluster(3, "cluster-base");
      var fakeCloud = new FakeCloudClient { Snapshot = cloud };

      var result = await _engine.ExecuteAsync(
          CloudSyncDirection.Push,
          local,
          cloud,
          sync,
          fakeCloud,
          new FakeLocalApplier(),
          propagateDeletions: true);

      result.ClustersDeleted.Should().Be(1);
      fakeCloud.DeletedClusters.Should().Equal(3);
      sync.GetLastSyncedHash(CloudSyncObjectType.Cluster, 3).Should().BeEmpty();
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

    private static CloudSnapshot SnapshotWithMetadata(int id, string hash)
    {
      return new CloudSnapshot
      {
        Metadata =
        {
          new CloudMetadataRecord
          {
            Id = id,
            ContentHash = hash,
            CreatedBy = "tester"
          }
        }
      };
    }

    private static CloudSnapshot SnapshotWithCluster(int id, string hash)
    {
      return new CloudSnapshot
      {
        Clusters =
        {
          new CloudClusterRecord
          {
            Id = id,
            ContentHash = hash,
            Name = "Cluster" + id
          }
        }
      };
    }
  }
}
