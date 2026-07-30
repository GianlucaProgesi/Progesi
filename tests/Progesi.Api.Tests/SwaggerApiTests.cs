using System.Net;
using FluentAssertions;

namespace Progesi.Api.Tests;

[Collection(nameof(ProgesiApiTestCollection))]
public sealed class SwaggerApiTests
{
  private readonly HttpClient _client;

  public SwaggerApiTests(ProgesiApiWebApplicationFactory factory)
  {
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task Swagger_OpenApi_Document_Loads_In_Development()
  {
    var response = await _client.GetAsync("/swagger/v1/swagger.json");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var body = await response.Content.ReadAsStringAsync();
    body.Should().Contain("Progesi.Api");
    body.Should().Contain("/api/variables");
    body.Should().Contain("/api/metadata");
    body.Should().Contain("/api/clusters");
    body.Should().Contain("/api/projects");
    body.Should().Contain("\"Bearer\"");
    body.Should().Contain("securitySchemes");
  }
}
