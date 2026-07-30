using System.Text.Json;
using Progesi.Api.Dtos;
using ProgesiCore;

namespace Progesi.Api.Mapping;

public static class DomainMapping
{
  public static VariableDto ToDto(ProgesiVariable variable)
  {
    return new VariableDto
    {
      Id = variable.Id,
      Name = variable.Name,
      Value = JsonSerializer.SerializeToElement(variable.Value),
      DependsFrom = variable.DependsFrom.ToArray(),
      MetadataIds = variable.MetadataIds.ToArray(),
      IsAssumption = variable.IsAssumption,
      Hashtag = variable.Hashtag
    };
  }

  public static ProgesiVariable ToDomain(VariableUpsertDto dto)
  {
    return new ProgesiVariable(
      dto.Id,
      dto.Name,
      ParseJsonValue(dto.Value),
      dto.DependsFrom,
      dto.MetadataIds,
      dto.IsAssumption);
  }

  public static MetadataDto ToDto(ProgesiMetadata metadata)
  {
    return new MetadataDto
    {
      Id = metadata.Id,
      LastModifiedUtc = metadata.LastModified.ToUniversalTime(),
      CreatedBy = metadata.CreatedBy,
      AdditionalInfo = metadata.AdditionalInfo,
      References = metadata.References.Select(u => u.ToString()).ToArray(),
      Snips = metadata.Snips.Select(s => new MetadataSnipDto
      {
        Id = s.Id,
        MimeType = s.MimeType,
        Caption = s.Caption,
        Source = s.Source,
        ContentBase64 = Convert.ToBase64String(s.Content)
      }).ToArray(),
      Hashtag = metadata.Hashtag
    };
  }

  public static ProgesiMetadata ToDomain(MetadataUpsertDto dto)
  {
    var references = dto.References
      .Where(r => !string.IsNullOrWhiteSpace(r))
      .Select(r => new Uri(r, UriKind.RelativeOrAbsolute));

    var snips = dto.Snips.Select(s =>
    {
      var bytes = string.IsNullOrWhiteSpace(s.ContentBase64)
        ? Array.Empty<byte>()
        : Convert.FromBase64String(s.ContentBase64);
      return ProgesiSnip.Create(
        bytes,
        s.MimeType,
        s.Caption,
        string.IsNullOrWhiteSpace(s.Source) ? null : new Uri(s.Source, UriKind.RelativeOrAbsolute));
    });

    return ProgesiMetadata.Create(
      dto.CreatedBy,
      dto.AdditionalInfo,
      references,
      snips,
      DateTime.UtcNow,
      dto.Id > 0 ? dto.Id : null);
  }

  public static ClusterDto ToDto(ProgesiVariableCluster cluster)
  {
    return new ClusterDto
    {
      Id = cluster.Id,
      Name = cluster.Name,
      Description = cluster.Description,
      VariableIds = cluster.ProgesiVariableIds.ToArray(),
      Hashtag = cluster.Hashtag
    };
  }

  public static ProgesiVariableCluster ToDomain(ClusterUpsertDto dto)
  {
    return ProgesiVariableCluster.Rehydrate(
      dto.Id,
      dto.Name,
      dto.VariableIds,
      dto.Description,
      null);
  }

  public static string? ValidateVariableUpsert(VariableUpsertDto dto)
  {
    if (dto.Id <= 0)
      return "Variable id must be positive.";
    if (string.IsNullOrWhiteSpace(dto.Name))
      return "Variable name is required.";
    if (dto.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
      return "Variable value is required.";

    try
    {
      _ = ToDomain(dto);
    }
    catch (Exception ex)
    {
      return ex.Message;
    }

    return null;
  }

  public static string? ValidateMetadataUpsert(MetadataUpsertDto dto)
  {
    if (string.IsNullOrWhiteSpace(dto.CreatedBy))
      return "Metadata createdBy is required.";

    try
    {
      _ = ToDomain(dto);
    }
    catch (Exception ex)
    {
      return ex.Message;
    }

    return null;
  }

  public static string? ValidateClusterUpsert(ClusterUpsertDto dto)
  {
    if (dto.Id <= 0)
      return "Cluster id must be positive.";
    if (string.IsNullOrWhiteSpace(dto.Name))
      return "Cluster name is required.";
    if (dto.VariableIds == null || dto.VariableIds.All(id => id <= 0))
      return "Cluster must contain at least one positive variable id.";

    try
    {
      _ = ToDomain(dto);
    }
    catch (Exception ex)
    {
      return ex.Message;
    }

    return null;
  }

  private static object ParseJsonValue(JsonElement element)
  {
    return element.ValueKind switch
    {
      JsonValueKind.String => element.GetString() ?? string.Empty,
      JsonValueKind.Number when element.TryGetInt32(out var i) => i,
      JsonValueKind.Number => element.GetDouble(),
      JsonValueKind.True => true,
      JsonValueKind.False => false,
      JsonValueKind.Null => string.Empty,
      _ => element.GetRawText()
    };
  }
}
