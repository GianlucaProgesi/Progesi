using Microsoft.EntityFrameworkCore;
using Progesi.Infrastructure.EF;
using Progesi.Infrastructure.EF.Internal;

namespace Progesi.Api.Projects;

internal static class ProgesiDbContextOptionsBuilder
{
  public static DbContextOptions<ProgesiDbContext> Build(
      string connectionString,
      IConfiguration configuration)
  {
    var provider = configuration["Progesi:DbProvider"] ?? "Sqlite";
    var normalized = ProgesiDbContextFactory.NormalizeConnectionString(connectionString);
    var builder = new DbContextOptionsBuilder<ProgesiDbContext>();

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
      builder.UseSqlServer(normalized);
      return builder.Options;
    }

    builder.UseSqlite(
        normalized,
        sqlite => sqlite.ExecutionStrategy(deps => new SqliteBusyRetryExecutionStrategy(deps)));
    return builder.Options;
  }

  public static string BuildConnectionStringForProject(string projectId, IConfiguration configuration)
  {
    var provider = configuration["Progesi:DbProvider"] ?? "Sqlite";

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
      var template = configuration.GetConnectionString("SqlServerProjectTemplate");
      if (string.IsNullOrWhiteSpace(template))
      {
        throw new InvalidOperationException(
            "ConnectionStrings:SqlServerProjectTemplate is required when Progesi:DbProvider is SqlServer.");
      }

      return template.Replace("{ProjectId}", projectId, StringComparison.Ordinal);
    }

    var projectsDirectory = ResolveProjectsDirectory(configuration);
    Directory.CreateDirectory(projectsDirectory);
    var dbPath = Path.Combine(projectsDirectory, $"{projectId}.sqlite");
    return $"Data Source={dbPath}";
  }

  public static string ResolveProjectsDirectory(IConfiguration configuration)
  {
    var configured = configuration["Progesi:ProjectsDirectory"];
    if (string.IsNullOrWhiteSpace(configured))
      return Path.Combine(AppContext.BaseDirectory, "projects");

    return Path.IsPathRooted(configured)
        ? configured
        : Path.Combine(AppContext.BaseDirectory, configured);
  }
}
