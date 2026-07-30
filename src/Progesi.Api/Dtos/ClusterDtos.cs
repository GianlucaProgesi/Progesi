namespace Progesi.Api.Dtos;

public sealed class ClusterDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public int[] VariableIds { get; set; } = Array.Empty<int>();
  public string Hashtag { get; set; } = string.Empty;
}

public sealed class ClusterUpsertDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public int[] VariableIds { get; set; } = Array.Empty<int>();
}
