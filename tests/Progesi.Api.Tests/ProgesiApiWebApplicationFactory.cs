using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Progesi.Api.Tests;

public sealed class ProgesiApiWebApplicationFactory : WebApplicationFactory<Program>
{
  private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_api_{Guid.NewGuid():N}.sqlite");

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
    builder.UseSetting("ConnectionStrings:ProgesiDb", $"Data Source={_dbPath}");
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

    try
    {
      if (File.Exists(_dbPath))
        File.Delete(_dbPath);
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
