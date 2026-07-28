using System.Collections.Generic;
using System.Linq;

namespace Progesi.LiveDataExchange.Tests.Support
{
  internal sealed class InMemoryImportSink : ILiveExchangeImportSink
  {
    public List<(int id, string by, string description, string refs)> Metadata { get; } =
      new List<(int, string, string, string)>();

    public List<LiveExchangeVariableImportPayload> Variables { get; } =
      new List<LiveExchangeVariableImportPayload>();

    public List<LiveExchangeClusterImportPayload> Clusters { get; } =
      new List<LiveExchangeClusterImportPayload>();

    public HashSet<int> KnownMetadataIds { get; } = new HashSet<int>();

    public bool TryUpsertMetadata(int id, string by, string description, string refsPipeSeparated, out int persistedId, out string error)
    {
      persistedId = id > 0 ? id : Metadata.Count + 1;
      Metadata.Add((persistedId, by, description, refsPipeSeparated));
      KnownMetadataIds.Add(persistedId);
      error = null;
      return true;
    }

    public bool TryGetMetadataById(int id, out string error)
    {
      error = null;
      return KnownMetadataIds.Contains(id);
    }

    public bool TryUpsertVariable(LiveExchangeVariableImportPayload payload, out int persistedId, out string error)
    {
      persistedId = payload.Id > 0 ? payload.Id : Variables.Count + 1;
      Variables.Add(new LiveExchangeVariableImportPayload
      {
        Id = persistedId,
        Name = payload.Name,
        Value = payload.Value,
        GeometryJson = payload.GeometryJson,
        IsAssumption = payload.IsAssumption,
        MetadataIds = payload.MetadataIds?.ToArray() ?? System.Array.Empty<int>(),
        Depends = payload.Depends?.ToArray() ?? System.Array.Empty<int>()
      });
      error = null;
      return true;
    }

    public bool TryPersistCluster(LiveExchangeClusterImportPayload cluster)
    {
      Clusters.Add(new LiveExchangeClusterImportPayload
      {
        Id = cluster.Id,
        Name = cluster.Name,
        Description = cluster.Description,
        VariableIds = cluster.VariableIds?.ToArray() ?? System.Array.Empty<int>(),
        Hashtag = cluster.Hashtag
      });
      return true;
    }

    public void UpdateIdCounters(int maxMetaId, int maxVarId)
    {
    }
  }
}
