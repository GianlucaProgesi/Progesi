using System.Collections.Generic;

namespace Progesi.LiveDataExchange.Cloud
{
  public sealed class CloudSyncConflict
  {
    public CloudSyncObjectType ObjectType { get; set; }
    public int Id { get; set; }
    public string LocalHash { get; set; } = string.Empty;
    public string CloudHash { get; set; } = string.Empty;
  }

  public sealed class CloudSyncResult
  {
    public int VariablesApplied { get; set; }
    public int MetadataApplied { get; set; }
    public int ClustersApplied { get; set; }
    public int Skipped { get; set; }
    public IList<CloudSyncConflict> Conflicts { get; set; } = new List<CloudSyncConflict>();
    public IList<string> Log { get; set; } = new List<string>();
  }
}
