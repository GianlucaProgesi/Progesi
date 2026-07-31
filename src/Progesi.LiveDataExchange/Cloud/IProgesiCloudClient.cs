using System.Threading;
using System.Threading.Tasks;

namespace Progesi.LiveDataExchange.Cloud
{
  public interface IProgesiCloudClient
  {
    Task<CloudSnapshot> GetCloudSnapshotAsync(CancellationToken ct = default);
    Task UpsertVariableAsync(CloudVariableRecord record, CancellationToken ct = default);
    Task UpsertMetadataAsync(CloudMetadataRecord record, CancellationToken ct = default);
    Task UpsertClusterAsync(CloudClusterRecord record, CancellationToken ct = default);
  }

  public interface ICloudSyncLocalApplier
  {
    Task ApplyVariableAsync(CloudVariableRecord record, CancellationToken ct = default);
    Task ApplyMetadataAsync(CloudMetadataRecord record, CancellationToken ct = default);
    Task ApplyClusterAsync(CloudClusterRecord record, CancellationToken ct = default);
  }
}
