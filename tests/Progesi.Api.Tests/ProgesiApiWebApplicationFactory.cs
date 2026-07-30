using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Progesi.Api.Projects;

namespace Progesi.Api.Tests;

public sealed class ProgesiApiWebApplicationFactory : WebApplicationFactory<Program>
{
  private readonly string _projectsDirectory = Path.Combine(
      Path.GetTempPath(),
      $"progesi_projects_{Guid.NewGuid():N}");

  public string DefaultProjectId => "default";

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
    builder.UseSetting("Progesi:ProjectsDirectory", _projectsDirectory);
    builder.UseSetting("Progesi:DefaultProjectId", DefaultProjectId);
    builder.UseSetting("Progesi:ResetSchemaOnStartup", "true");
    builder.UseSetting("Progesi:UseTestAuthentication", "true");

    builder.ConfigureTestServices(services =>
    {
      services.AddAuthentication(TestAuthHandler.SchemeName)
          .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
              TestAuthHandler.SchemeName,
              _ => { });
    });
  }

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);

    if (!disposing)
      return;

    TryDeleteDirectory(_projectsDirectory);
  }

  private static void TryDeleteDirectory(string path)
  {
    try
    {
      if (Directory.Exists(path))
        Directory.Delete(path, recursive: true);
    }
    catch
    {
      // best-effort cleanup
    }
  }
}

[CollectionDefinition(nameof(ProgesiApiTestCollection))]
public sealed class ProgesiApiTestCollection : ICollectionFixture<ProgesiApiWebApplicationFactory>
{
}
