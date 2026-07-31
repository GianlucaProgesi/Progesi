using System.Collections.Generic;

namespace Progesi.LiveDataExchange.Cloud
{
  public sealed class CloudVariableRecord
  {
    public int Id { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "null";
    public int[] DependsFrom { get; set; } = System.Array.Empty<int>();
    public int[] MetadataIds { get; set; } = System.Array.Empty<int>();
    public bool IsAssumption { get; set; }
  }

  public sealed class CloudMetadataRecord
  {
    public int Id { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string AdditionalInfo { get; set; } = string.Empty;
    public string[] References { get; set; } = System.Array.Empty<string>();
    public CloudMetadataSnipRecord[] Snips { get; set; } = System.Array.Empty<CloudMetadataSnipRecord>();
  }

  public sealed class CloudMetadataSnipRecord
  {
    public System.Guid Id { get; set; }
    public string MimeType { get; set; } = "image/png";
    public string Caption { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
  }

  public sealed class CloudClusterRecord
  {
    public int Id { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int[] VariableIds { get; set; } = System.Array.Empty<int>();
  }

  public sealed class CloudSnapshot
  {
    public IList<CloudVariableRecord> Variables { get; set; } = new List<CloudVariableRecord>();
    public IList<CloudMetadataRecord> Metadata { get; set; } = new List<CloudMetadataRecord>();
    public IList<CloudClusterRecord> Clusters { get; set; } = new List<CloudClusterRecord>();
  }
}
