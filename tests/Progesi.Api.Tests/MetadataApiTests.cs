using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Progesi.Api.Dtos;

namespace Progesi.Api.Tests;

[Collection(nameof(ProgesiApiTestCollection))]
public sealed class MetadataApiTests
{
  private readonly HttpClient _client;

  public MetadataApiTests(ProgesiApiWebApplicationFactory factory)
  {
    _client = factory.CreateClient().AsWriterForProject(factory.DefaultProjectId);
  }

  [Fact]
  public async Task Metadata_Crud_RoundTrip_Works()
  {
    var create = new MetadataUpsertDto
    {
      Id = 20,
      CreatedBy = "tester",
      AdditionalInfo = "note",
      References = new[] { "https://example.com/doc" },
      Snips = Array.Empty<MetadataSnipDto>()
    };

    var post = await _client.PostAsJsonAsync("/api/metadata", create);
    post.StatusCode.Should().Be(HttpStatusCode.Created);

    var get = await _client.GetAsync("/api/metadata/20");
    get.StatusCode.Should().Be(HttpStatusCode.OK);
    var loaded = await get.Content.ReadFromJsonAsync<MetadataDto>();
    loaded!.CreatedBy.Should().Be("tester");

    var update = new MetadataUpsertDto
    {
      Id = 20,
      CreatedBy = "tester",
      AdditionalInfo = "updated",
      References = new[] { "https://example.com/doc" },
      Snips = Array.Empty<MetadataSnipDto>()
    };
    var put = await _client.PutAsJsonAsync("/api/metadata/20", update);
    put.StatusCode.Should().Be(HttpStatusCode.OK);
    var updated = await put.Content.ReadFromJsonAsync<MetadataDto>();
    updated!.AdditionalInfo.Should().Be("updated");

    var delete = await _client.DeleteAsync("/api/metadata/20");
    delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var missing = await _client.GetAsync("/api/metadata/20");
    missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Metadata_Invalid_Create_Returns_400()
  {
    var invalid = new MetadataUpsertDto
    {
      Id = 21,
      CreatedBy = "",
      AdditionalInfo = "x"
    };

    var response = await _client.PostAsJsonAsync("/api/metadata", invalid);
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Metadata_Missing_Returns_404()
  {
    var response = await _client.GetAsync("/api/metadata/9999");
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }
}
