using System.Threading;
using System.Threading.Tasks;
using Progesi.LiveDataExchange.Cloud;
using ProgesiRepositories.Rhino;
using Rhino;
using Rhino.DocObjects.Tables;

namespace ProgesiGrasshopperAssembly.Infrastructure.Cloud
{
  public sealed class RhinoSyncStateStore : ISyncStateStore
  {
    private const string Section = "Progesi.CloudSync";

    private readonly StringTable _table;

    public RhinoSyncStateStore(RhinoDoc doc)
    {
      if (doc == null) throw new System.ArgumentNullException(nameof(doc));
      _table = doc.Strings ?? throw new System.InvalidOperationException("RhinoDoc.Strings is null.");
    }

    public string GetLastSyncedHash(CloudSyncObjectType objectType, int id)
    {
      return _table.GetValue(Section, Key(objectType, id)) ?? string.Empty;
    }

    public void SetLastSyncedHash(CloudSyncObjectType objectType, int id, string contentHash)
    {
      _table.SetString(Section, Key(objectType, id), contentHash ?? string.Empty);
    }

    private static string Key(CloudSyncObjectType objectType, int id)
        => objectType + ":" + id;
  }

  public sealed class RhinoCloudSyncLocalApplier : ICloudSyncLocalApplier
  {
    private readonly RhinoDoc _doc;

    public RhinoCloudSyncLocalApplier(RhinoDoc doc)
    {
      _doc = doc ?? throw new System.ArgumentNullException(nameof(doc));
    }

    public Task ApplyVariableAsync(CloudVariableRecord record, CancellationToken ct = default)
    {
      var repo = new RhinoVariableRepository(_doc);
      return repo.SaveAsync(CloudSnapshotMapper.ToVariable(record), ct);
    }

    public Task ApplyMetadataAsync(CloudMetadataRecord record, CancellationToken ct = default)
    {
      var repo = new RhinoMetadataRepository(_doc);
      return repo.UpsertAsync(CloudSnapshotMapper.ToMetadata(record), ct);
    }

    public Task ApplyClusterAsync(CloudClusterRecord record, CancellationToken ct = default)
    {
      var repo = new RhinoVariableClusterRepository(_doc);
      return repo.SaveAsync(CloudSnapshotMapper.ToCluster(record), ct);
    }
  }
}
