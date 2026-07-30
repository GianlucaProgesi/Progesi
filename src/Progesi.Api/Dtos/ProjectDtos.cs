namespace Progesi.Api.Dtos;

public sealed class ProjectDto
{
  public string Id { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
}

public sealed class CreateProjectRequest
{
  public string Name { get; set; } = string.Empty;
}
