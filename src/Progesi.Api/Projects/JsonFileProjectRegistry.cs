using System.Text.Json;
using Progesi.Infrastructure.EF;

namespace Progesi.Api.Projects;

public sealed class JsonFileProjectRegistry : IProjectRegistry
{
  private readonly string _registryPath;
  private readonly string _defaultProjectId;
  private readonly IConfiguration _configuration;
  private readonly object _sync = new();
  private RegistryDocument? _cache;

  public JsonFileProjectRegistry(IConfiguration configuration)
  {
    _configuration = configuration;
    _defaultProjectId = configuration["Progesi:DefaultProjectId"] ?? "default";
    var projectsDirectory = ProgesiDbContextOptionsBuilder.ResolveProjectsDirectory(configuration);
    Directory.CreateDirectory(projectsDirectory);
    _registryPath = Path.Combine(projectsDirectory, "projects.json");
  }

  public ProjectEntry? GetById(string projectId)
  {
    lock (_sync)
    {
      return LoadDocument().Projects.FirstOrDefault(p =>
          string.Equals(p.Id, projectId, StringComparison.OrdinalIgnoreCase));
    }
  }

  public IReadOnlyList<ProjectEntry> GetAll()
  {
    lock (_sync)
    {
      return LoadDocument().Projects
          .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
          .ToList();
    }
  }

  public void Add(ProjectEntry entry)
  {
    if (entry is null) throw new ArgumentNullException(nameof(entry));
    if (string.IsNullOrWhiteSpace(entry.Id)) throw new ArgumentException("Project id is required.", nameof(entry));
    if (string.IsNullOrWhiteSpace(entry.Name)) throw new ArgumentException("Project name is required.", nameof(entry));
    if (string.IsNullOrWhiteSpace(entry.ConnectionString))
      throw new ArgumentException("Connection string is required.", nameof(entry));

    lock (_sync)
    {
      var document = LoadDocument();
      if (document.Projects.Any(p => string.Equals(p.Id, entry.Id, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"Project '{entry.Id}' already exists.");

      document.Projects.Add(new ProjectEntry
      {
        Id = entry.Id.Trim(),
        Name = entry.Name.Trim(),
        ConnectionString = entry.ConnectionString
      });
      SaveDocument(document);
    }
  }

  public void EnsureDefaultProject()
  {
    lock (_sync)
    {
      var document = LoadDocument();
      if (document.Projects.Any(p => string.Equals(p.Id, _defaultProjectId, StringComparison.OrdinalIgnoreCase)))
        return;

      var connectionString = ProgesiDbContextOptionsBuilder.BuildConnectionStringForProject(
          _defaultProjectId,
          _configuration);

      MigrateEmptyDatabase(connectionString);

      document.Projects.Add(new ProjectEntry
      {
        Id = _defaultProjectId,
        Name = "Default",
        ConnectionString = connectionString
      });
      SaveDocument(document);
    }
  }

  internal static void MigrateEmptyDatabase(string connectionString, IConfiguration configuration)
  {
    var options = ProgesiDbContextOptionsBuilder.Build(connectionString, configuration);
    using var context = new ProgesiDbContext(options);
    ProgesiDbContextFactory.EnsureSchema(context, resetSchema: false);
  }

  private void MigrateEmptyDatabase(string connectionString)
      => MigrateEmptyDatabase(connectionString, _configuration);

  private RegistryDocument LoadDocument()
  {
    if (_cache != null)
      return _cache;

    if (!File.Exists(_registryPath))
    {
      _cache = new RegistryDocument();
      return _cache;
    }

    var json = File.ReadAllText(_registryPath);
    _cache = JsonSerializer.Deserialize<RegistryDocument>(json, SerializerOptions)
             ?? new RegistryDocument();
    return _cache;
  }

  private void SaveDocument(RegistryDocument document)
  {
    _cache = document;
    var json = JsonSerializer.Serialize(document, SerializerOptions);
    File.WriteAllText(_registryPath, json);
  }

  private sealed class RegistryDocument
  {
    public List<ProjectEntry> Projects { get; set; } = new();
  }

  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };
}
