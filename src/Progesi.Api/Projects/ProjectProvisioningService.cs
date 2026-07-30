namespace Progesi.Api.Projects;

public sealed class ProjectProvisioningService : IProjectProvisioningService
{
  private readonly IProjectRegistry _registry;
  private readonly IConfiguration _configuration;

  public ProjectProvisioningService(IProjectRegistry registry, IConfiguration configuration)
  {
    _registry = registry;
    _configuration = configuration;
  }

  public ProjectEntry Provision(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
      throw new ArgumentException("Project name is required.", nameof(name));

    var projectId = Guid.NewGuid().ToString("N");
    var connectionString = ProgesiDbContextOptionsBuilder.BuildConnectionStringForProject(
        projectId,
        _configuration);

    JsonFileProjectRegistry.MigrateEmptyDatabase(connectionString, _configuration);

    var entry = new ProjectEntry
    {
      Id = projectId,
      Name = name.Trim(),
      ConnectionString = connectionString
    };

    _registry.Add(entry);
    return entry;
  }
}
