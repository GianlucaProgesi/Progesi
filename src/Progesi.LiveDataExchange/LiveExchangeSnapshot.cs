using System;

namespace Progesi.LiveDataExchange
{
  public sealed class LiveExchangeSnapshot
  {
    public VariableExportRow[] Variables { get; set; } = Array.Empty<VariableExportRow>();
    public MetadataExportRow[] Metadata { get; set; } = Array.Empty<MetadataExportRow>();
    public ClusterExportRow[] Clusters { get; set; } = Array.Empty<ClusterExportRow>();
  }

  public sealed class VariableExportRow
  {
    public int Id;
    public string Hash;
    public string Name;
    public string Value;
    public string ValC;
    public int MetaId;
    public int[] MetadataIds = Array.Empty<int>();
    public int[] Depends = Array.Empty<int>();
    public bool Assumption;
    public bool IsExcelUnsupported;
    public string ObjectType = "";
    public string ObjectPayloadJson = "";
  }

  public sealed class MetadataExportRow
  {
    public int Id;
    public string Hash;
    public string By;
    public string Description;
    public string[] Refs = Array.Empty<string>();
    public string LM;
  }

  public sealed class ClusterExportRow
  {
    public int Id;
    public string Hash = "";
    public string Name = "";
    public string Description = "";
    public int[] VariableIds = Array.Empty<int>();
  }
}
