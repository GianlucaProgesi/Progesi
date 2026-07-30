namespace Progesi.Api.Projects;

public interface IProjectRegistry
{
  ProjectEntry? GetById(string projectId);
  IReadOnlyList<ProjectEntry> GetAll();
  void Add(ProjectEntry entry);
  void EnsureDefaultProject();
}
