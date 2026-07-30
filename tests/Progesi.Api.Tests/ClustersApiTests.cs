using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Progesi.Api.Dtos;

namespace Progesi.Api.Tests;

[Collection(nameof(ProgesiApiTestCollection))]
public sealed class ClustersApiTests
{
  private readonly HttpClient _client;

  public ClustersApiTests(ProgesiApiWebApplicationFactory factory)
  {
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task Clusters_Crud_RoundTrip_Works()
  {
    var create = new ClusterUpsertDto
    {
      Id = 30,
      Name = "ClusterA",
      Description = "desc",
      VariableIds = new[] { 2, 1 }
    };

    var post = await _client.PostAsJsonAsync("/api/clusters", create);
    post.StatusCode.Should().Be(HttpStatusCode.Created);

    var get = await _client.GetAsync("/api/clusters/30");
    get.StatusCode.Should().Be(HttpStatusCode.OK);
    var loaded = await get.Content.ReadFromJsonAsync<ClusterDto>();
    loaded!.Name.Should().Be("ClusterA");
    loaded.VariableIds.Should().Equal(1, 2);

    var update = new ClusterUpsertDto
    {
      Id = 30,
      Name = "ClusterB",
      Description = "desc",
      VariableIds = new[] { 3, 1 }
    };
    var put = await _client.PutAsJsonAsync("/api/clusters/30", update);
    put.StatusCode.Should().Be(HttpStatusCode.OK);
    var updated = await put.Content.ReadFromJsonAsync<ClusterDto>();
    updated!.Name.Should().Be("ClusterB");

    var delete = await _client.DeleteAsync("/api/clusters/30");
    delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var missing = await _client.GetAsync("/api/clusters/30");
    missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Clusters_Invalid_Create_Returns_400()
  {
    var invalid = new ClusterUpsertDto
    {
      Id = 31,
      Name = "EmptyCluster",
      VariableIds = Array.Empty<int>()
    };

    var response = await _client.PostAsJsonAsync("/api/clusters", invalid);
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Clusters_Missing_Returns_404()
  {
    var response = await _client.GetAsync("/api/clusters/9999");
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }
}
