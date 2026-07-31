using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProgesiCore;

namespace Progesi.LiveDataExchange.Cloud
{
  public static class CloudSnapshotMapper
  {
    public static CloudSnapshot FromDomain(
        IEnumerable<ProgesiVariable> variables,
        IEnumerable<ProgesiMetadata> metadata,
        IEnumerable<ProgesiVariableCluster> clusters)
    {
      return new CloudSnapshot
      {
        Variables = variables.Select(FromVariable).ToList(),
        Metadata = metadata.Select(FromMetadata).ToList(),
        Clusters = clusters.Select(FromCluster).ToList()
      };
    }

    public static CloudVariableRecord FromVariable(ProgesiVariable variable)
    {
      if (variable == null) throw new ArgumentNullException(nameof(variable));

      return new CloudVariableRecord
      {
        Id = variable.Id,
        ContentHash = ProgesiHash.Compute(variable),
        Name = variable.Name,
        ValueJson = JsonConvert.SerializeObject(variable.Value),
        DependsFrom = variable.DependsFrom?.ToArray() ?? Array.Empty<int>(),
        MetadataIds = variable.MetadataIds?.ToArray() ?? Array.Empty<int>(),
        IsAssumption = variable.IsAssumption
      };
    }

    public static CloudMetadataRecord FromMetadata(ProgesiMetadata metadata)
    {
      if (metadata == null) throw new ArgumentNullException(nameof(metadata));

      return new CloudMetadataRecord
      {
        Id = metadata.Id,
        ContentHash = ProgesiHash.Compute(metadata),
        CreatedBy = metadata.CreatedBy,
        AdditionalInfo = metadata.AdditionalInfo,
        References = (metadata.References ?? Array.Empty<Uri>())
            .Select(u => u.ToString())
            .ToArray(),
        Snips = (metadata.Snips ?? Array.Empty<ProgesiSnip>())
            .Select(s => new CloudMetadataSnipRecord
            {
              Id = s.Id,
              MimeType = s.MimeType,
              Caption = s.Caption,
              Source = s.Source?.ToString() ?? string.Empty,
              ContentBase64 = Convert.ToBase64String(s.Content ?? Array.Empty<byte>())
            })
            .ToArray()
      };
    }

    public static CloudClusterRecord FromCluster(ProgesiVariableCluster cluster)
    {
      if (cluster == null) throw new ArgumentNullException(nameof(cluster));

      return new CloudClusterRecord
      {
        Id = cluster.Id,
        ContentHash = ProgesiHash.Compute(cluster),
        Name = cluster.Name,
        Description = cluster.Description ?? string.Empty,
        VariableIds = cluster.ProgesiVariableIds?.ToArray() ?? Array.Empty<int>()
      };
    }

    public static ProgesiVariable ToVariable(CloudVariableRecord record)
    {
      if (record == null) throw new ArgumentNullException(nameof(record));

      return new ProgesiVariable(
          record.Id,
          record.Name,
          ParseJsonValue(record.ValueJson),
          record.DependsFrom,
          record.MetadataIds,
          record.IsAssumption);
    }

    public static ProgesiMetadata ToMetadata(CloudMetadataRecord record)
    {
      if (record == null) throw new ArgumentNullException(nameof(record));

      var references = (record.References ?? Array.Empty<string>())
          .Where(r => !string.IsNullOrWhiteSpace(r))
          .Select(r => new Uri(r, UriKind.RelativeOrAbsolute));

      var snips = (record.Snips ?? Array.Empty<CloudMetadataSnipRecord>())
          .Select(s =>
          {
            var bytes = string.IsNullOrWhiteSpace(s.ContentBase64)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(s.ContentBase64);
            Uri source = null;
            if (!string.IsNullOrWhiteSpace(s.Source))
              Uri.TryCreate(s.Source, UriKind.RelativeOrAbsolute, out source);
            return ProgesiSnip.Create(bytes, s.MimeType, s.Caption, source);
          });

      return ProgesiMetadata.Create(
          record.CreatedBy,
          record.AdditionalInfo,
          references,
          snips,
          DateTime.UtcNow,
          record.Id > 0 ? record.Id : (int?)null);
    }

    public static ProgesiVariableCluster ToCluster(CloudClusterRecord record)
    {
      if (record == null) throw new ArgumentNullException(nameof(record));

      return ProgesiVariableCluster.Rehydrate(
          record.Id,
          record.Name,
          record.VariableIds ?? Array.Empty<int>(),
          record.Description);
    }

    private static object ParseJsonValue(string json)
    {
      if (string.IsNullOrWhiteSpace(json))
        return string.Empty;

      var token = JsonConvert.DeserializeObject(json);
      if (token is Newtonsoft.Json.Linq.JValue jValue)
        return jValue.Value ?? string.Empty;

      return token ?? string.Empty;
    }
  }
}
