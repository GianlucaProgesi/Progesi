using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Progesi.Api.Dtos;

namespace Progesi.Api.Tests;

[Collection(nameof(ProgesiApiTestCollection))]
public sealed class VariablesApiTests
{
  private readonly HttpClient _client;

  public VariablesApiTests(ProgesiApiWebApplicationFactory factory)
  {
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task Variables_Crud_RoundTrip_Works()
  {
    var create = new VariableUpsertDto
    {
      Id = 10,
      Name = "Span",
      Value = System.Text.Json.JsonSerializer.SerializeToElement(12.5),
      DependsFrom = new[] { 1, 2 },
      MetadataIds = new[] { 3 }
    };

    var post = await _client.PostAsJsonAsync("/api/variables", create);
    post.StatusCode.Should().Be(HttpStatusCode.Created);

    var get = await _client.GetAsync("/api/variables/10");
    get.StatusCode.Should().Be(HttpStatusCode.OK);
    var loaded = await get.Content.ReadFromJsonAsync<VariableDto>();
    loaded!.Name.Should().Be("Span");
    loaded.Value.GetDouble().Should().Be(12.5);

    var update = new VariableUpsertDto
    {
      Id = 10,
      Name = "SpanUpdated",
      Value = System.Text.Json.JsonSerializer.SerializeToElement(99.0),
      DependsFrom = new[] { 1, 2 },
      MetadataIds = new[] { 3 }
    };
    var put = await _client.PutAsJsonAsync("/api/variables/10", update);
    put.StatusCode.Should().Be(HttpStatusCode.OK);
    var updated = await put.Content.ReadFromJsonAsync<VariableDto>();
    updated!.Name.Should().Be("SpanUpdated");

    var delete = await _client.DeleteAsync("/api/variables/10");
    delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var missing = await _client.GetAsync("/api/variables/10");
    missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Variables_Invalid_Create_Returns_400()
  {
    var invalid = new VariableUpsertDto
    {
      Id = 11,
      Name = "",
      Value = System.Text.Json.JsonSerializer.SerializeToElement(1)
    };

    var response = await _client.PostAsJsonAsync("/api/variables", invalid);
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Variables_Missing_Returns_404()
  {
    var response = await _client.GetAsync("/api/variables/9999");
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }
}
