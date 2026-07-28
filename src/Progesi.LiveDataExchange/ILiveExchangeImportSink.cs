namespace Progesi.LiveDataExchange
{
  public sealed class LiveExchangeVariableImportPayload
  {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string GeometryJson { get; set; } = "";
    public bool IsAssumption { get; set; }
    public int[] MetadataIds { get; set; } = System.Array.Empty<int>();
    public int[] Depends { get; set; } = System.Array.Empty<int>();
  }

  public sealed class LiveExchangeClusterImportPayload
  {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int[] VariableIds { get; set; } = System.Array.Empty<int>();
    public string Hashtag { get; set; } = "";
  }

  /// <summary>Host-supplied persistence for validated import rows (Rhino repo in GH).</summary>
  public interface ILiveExchangeImportSink
  {
    bool TryUpsertMetadata(int id, string by, string description, string refsPipeSeparated, out int persistedId, out string error);

    bool TryGetMetadataById(int id, out string error);

    bool TryUpsertVariable(LiveExchangeVariableImportPayload payload, out int persistedId, out string error);

    bool TryPersistCluster(LiveExchangeClusterImportPayload cluster);

    void UpdateIdCounters(int maxMetaId, int maxVarId);
  }
}
