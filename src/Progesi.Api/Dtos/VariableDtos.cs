using System.Text.Json;

namespace Progesi.Api.Dtos;

public sealed class VariableDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public JsonElement Value { get; set; }
  public int[] DependsFrom { get; set; } = Array.Empty<int>();
  public int[] MetadataIds { get; set; } = Array.Empty<int>();
  public bool IsAssumption { get; set; }
  public string Hashtag { get; set; } = string.Empty;
}

public sealed class VariableUpsertDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public JsonElement Value { get; set; }
  public int[] DependsFrom { get; set; } = Array.Empty<int>();
  public int[] MetadataIds { get; set; } = Array.Empty<int>();
  public bool IsAssumption { get; set; }
}
