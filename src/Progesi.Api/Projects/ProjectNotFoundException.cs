namespace Progesi.Api.Projects;

public sealed class ProjectNotFoundException : Exception
{
  public ProjectNotFoundException(string projectId)
      : base($"Project '{projectId}' was not found.")
  {
    ProjectId = projectId;
  }

  public string ProjectId { get; }
}
