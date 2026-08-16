using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Progesi.LiveDataExchange.Cloud;

namespace Progesi.LiveDataExchange.Tests.Cloud
{
  internal sealed class FakeCloudClient : IProgesiCloudClient
  {
    public CloudSnapshot Snapshot { get; set; } = new CloudSnapshot();
    public IList<CloudVariableRecord> UpsertedVariables { get; } = new List<CloudVariableRecord>();
    public IList<CloudMetadataRecord> UpsertedMetadata { get; } = new List<CloudMetadataRecord>();
    public IList<CloudClusterRecord> UpsertedClusters { get; } = new List<CloudClusterRecord>();
    public IList<int> DeletedVariables { get; } = new List<int>();
    public IList<int> DeletedMetadata { get; } = new List<int>();
    public IList<int> DeletedClusters { get; } = new List<int>();

    public Task<CloudSnapshot> GetCloudSnapshotAsync(CancellationToken ct = default)
        => Task.FromResult(CloneSnapshot(Snapshot));

    public Task UpsertVariableAsync(CloudVariableRecord record, CancellationToken ct = default)
    {
      UpsertedVariables.Add(record);
      UpsertIntoSnapshot(record);
      return Task.CompletedTask;
    }

    public Task UpsertMetadataAsync(CloudMetadataRecord record, CancellationToken ct = default)
    {
      UpsertedMetadata.Add(record);
      UpsertIntoSnapshot(record);
      return Task.CompletedTask;
    }

    public Task UpsertClusterAsync(CloudClusterRecord record, CancellationToken ct = default)
    {
      UpsertedClusters.Add(record);
      UpsertIntoSnapshot(record);
      return Task.CompletedTask;
    }

    public Task DeleteVariableAsync(int id, CancellationToken ct = default)
    {
      DeletedVariables.Add(id);
      RemoveFromSnapshot(Snapshot.Variables, id);
      return Task.CompletedTask;
    }

    public Task DeleteMetadataAsync(int id, CancellationToken ct = default)
    {
      DeletedMetadata.Add(id);
      RemoveFromSnapshot(Snapshot.Metadata, id);
      return Task.CompletedTask;
    }

    public Task DeleteClusterAsync(int id, CancellationToken ct = default)
    {
      DeletedClusters.Add(id);
      RemoveFromSnapshot(Snapshot.Clusters, id);
      return Task.CompletedTask;
    }

    private static void RemoveFromSnapshot<T>(IList<T> list, int id) where T : class
    {
      var existing = list.FirstOrDefault(v => GetId(v) == id);
      if (existing != null)
        list.Remove(existing);
    }

    private static int GetId(object record)
    {
      switch (record)
      {
        case CloudVariableRecord variable:
          return variable.Id;
        case CloudMetadataRecord metadata:
          return metadata.Id;
        case CloudClusterRecord cluster:
          return cluster.Id;
        default:
          return 0;
      }
    }

    private void UpsertIntoSnapshot(CloudVariableRecord record)
    {
      var existing = Snapshot.Variables.FirstOrDefault(v => v.Id == record.Id);
      if (existing == null)
        Snapshot.Variables.Add(record);
      else
      {
        existing.ContentHash = record.ContentHash;
        existing.Name = record.Name;
        existing.ValueJson = record.ValueJson;
      }
    }

    private void UpsertIntoSnapshot(CloudMetadataRecord record)
    {
      var existing = Snapshot.Metadata.FirstOrDefault(v => v.Id == record.Id);
      if (existing == null)
        Snapshot.Metadata.Add(record);
      else
      {
        existing.ContentHash = record.ContentHash;
        existing.CreatedBy = record.CreatedBy;
      }
    }

    private void UpsertIntoSnapshot(CloudClusterRecord record)
    {
      var existing = Snapshot.Clusters.FirstOrDefault(v => v.Id == record.Id);
      if (existing == null)
        Snapshot.Clusters.Add(record);
      else
      {
        existing.ContentHash = record.ContentHash;
        existing.Name = record.Name;
      }
    }

    private static CloudSnapshot CloneSnapshot(CloudSnapshot source)
    {
      return new CloudSnapshot
      {
        Variables = source.Variables.ToList(),
        Metadata = source.Metadata.ToList(),
        Clusters = source.Clusters.ToList()
      };
    }
  }

  internal sealed class FakeLocalApplier : ICloudSyncLocalApplier
  {
    public CloudSnapshot Snapshot { get; } = new CloudSnapshot();
    public IList<CloudVariableRecord> AppliedVariables { get; } = new List<CloudVariableRecord>();
    public IList<int> DeletedVariables { get; } = new List<int>();
    public IList<int> DeletedMetadata { get; } = new List<int>();
    public IList<int> DeletedClusters { get; } = new List<int>();

    public Task ApplyVariableAsync(CloudVariableRecord record, CancellationToken ct = default)
    {
      AppliedVariables.Add(record);
      var existing = Snapshot.Variables.FirstOrDefault(v => v.Id == record.Id);
      if (existing == null)
        Snapshot.Variables.Add(record);
      else
      {
        existing.ContentHash = record.ContentHash;
        existing.Name = record.Name;
        existing.ValueJson = record.ValueJson;
      }

      return Task.CompletedTask;
    }

    public Task ApplyMetadataAsync(CloudMetadataRecord record, CancellationToken ct = default)
    {
      var existing = Snapshot.Metadata.FirstOrDefault(v => v.Id == record.Id);
      if (existing == null)
        Snapshot.Metadata.Add(record);
      else
        existing.ContentHash = record.ContentHash;
      return Task.CompletedTask;
    }

    public Task ApplyClusterAsync(CloudClusterRecord record, CancellationToken ct = default)
    {
      var existing = Snapshot.Clusters.FirstOrDefault(v => v.Id == record.Id);
      if (existing == null)
        Snapshot.Clusters.Add(record);
      else
        existing.ContentHash = record.ContentHash;
      return Task.CompletedTask;
    }

    public Task DeleteVariableAsync(int id, CancellationToken ct = default)
    {
      DeletedVariables.Add(id);
      var existing = Snapshot.Variables.FirstOrDefault(v => v.Id == id);
      if (existing != null)
        Snapshot.Variables.Remove(existing);
      return Task.CompletedTask;
    }

    public Task DeleteMetadataAsync(int id, CancellationToken ct = default)
    {
      DeletedMetadata.Add(id);
      var existing = Snapshot.Metadata.FirstOrDefault(v => v.Id == id);
      if (existing != null)
        Snapshot.Metadata.Remove(existing);
      return Task.CompletedTask;
    }

    public Task DeleteClusterAsync(int id, CancellationToken ct = default)
    {
      DeletedClusters.Add(id);
      var existing = Snapshot.Clusters.FirstOrDefault(v => v.Id == id);
      if (existing != null)
        Snapshot.Clusters.Remove(existing);
      return Task.CompletedTask;
    }
  }
}
