using Microsoft.EntityFrameworkCore;
using Progesi.Infrastructure.EF;

namespace Progesi.Api.Projects;

public interface IProjectContext
{
  string ProjectId { get; }
  ProgesiDbContext DbContext { get; }
}

public sealed class ProjectContext : IProjectContext, IDisposable
{
  public string ProjectId { get; }
  public ProgesiDbContext DbContext { get; }

  public ProjectContext(
      IHttpContextAccessor httpContextAccessor,
      IProjectRegistry registry,
      IConfiguration configuration)
  {
    ProjectId = ResolveProjectId(httpContextAccessor, configuration);
    var entry = registry.GetById(ProjectId)
                ?? throw new ProjectNotFoundException(ProjectId);

    var options = ProgesiDbContextOptionsBuilder.Build(entry.ConnectionString, configuration);
    DbContext = new ProgesiDbContext(options);
  }

  public void Dispose()
  {
    DbContext.Dispose();
  }

  internal static string ResolveProjectId(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
  {
    var defaultProjectId = configuration["Progesi:DefaultProjectId"] ?? "default";
    var httpContext = httpContextAccessor.HttpContext;
    if (httpContext == null)
      return defaultProjectId;

    if (httpContext.Request.Headers.TryGetValue(ProjectHeaders.ProjectId, out var headerValues))
    {
      var headerValue = headerValues.FirstOrDefault();
      if (!string.IsNullOrWhiteSpace(headerValue))
        return headerValue.Trim();
    }

    return defaultProjectId;
  }
}
