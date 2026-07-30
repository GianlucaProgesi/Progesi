using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Progesi.Api.Dtos;

namespace Progesi.Api.Tests;

[Collection(nameof(ProgesiApiTestCollection))]
public sealed class ApiAuthorizationTests
{
  private readonly ProgesiApiWebApplicationFactory _factory;

  public ApiAuthorizationTests(ProgesiApiWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Theory]
  [InlineData("/api/variables")]
  [InlineData("/api/metadata")]
  [InlineData("/api/clusters")]
  public async Task Unauthenticated_Get_Returns_401(string route)
  {
    using var client = _factory.CreateClient().AsUnauthenticated();
    var response = await client.GetAsync(route);
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Theory]
  [InlineData("/api/variables")]
  [InlineData("/api/metadata")]
  [InlineData("/api/clusters")]
  public async Task Reader_Get_Returns_200(string route)
  {
    using var client = _factory.CreateClient().AsReaderForProject(_factory.DefaultProjectId);
    var response = await client.GetAsync(route);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task Variables_Reader_Post_Returns_403()
  {
    using var client = _factory.CreateClient().AsReaderForProject(_factory.DefaultProjectId);
    var dto = new VariableUpsertDto
    {
      Id = 9001,
      Name = "AuthTest",
      Value = System.Text.Json.JsonSerializer.SerializeToElement(1.0)
    };

    var response = await client.PostAsJsonAsync("/api/variables", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Metadata_Reader_Post_Returns_403()
  {
    using var client = _factory.CreateClient().AsReaderForProject(_factory.DefaultProjectId);
    var dto = new MetadataUpsertDto
    {
      Id = 9002,
      CreatedBy = "auth",
      AdditionalInfo = "note"
    };

    var response = await client.PostAsJsonAsync("/api/metadata", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Clusters_Reader_Post_Returns_403()
  {
    using var client = _factory.CreateClient().AsReaderForProject(_factory.DefaultProjectId);
    var dto = new ClusterUpsertDto
    {
      Id = 9003,
      Name = "AuthCluster",
      VariableIds = new[] { 1 }
    };

    var response = await client.PostAsJsonAsync("/api/clusters", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Variables_Writer_Post_Returns_201()
  {
    using var client = _factory.CreateClient().AsWriterForProject(_factory.DefaultProjectId);
    var dto = new VariableUpsertDto
    {
      Id = 9010,
      Name = "WriterVar",
      Value = System.Text.Json.JsonSerializer.SerializeToElement(2.0)
    };

    var response = await client.PostAsJsonAsync("/api/variables", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
  }

  [Fact]
  public async Task Metadata_Writer_Post_Returns_201()
  {
    using var client = _factory.CreateClient().AsWriterForProject(_factory.DefaultProjectId);
    var dto = new MetadataUpsertDto
    {
      Id = 9011,
      CreatedBy = "writer",
      AdditionalInfo = "note"
    };

    var response = await client.PostAsJsonAsync("/api/metadata", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
  }

  [Fact]
  public async Task Clusters_Writer_Post_Returns_201()
  {
    using var client = _factory.CreateClient().AsWriterForProject(_factory.DefaultProjectId);
    var dto = new ClusterUpsertDto
    {
      Id = 9012,
      Name = "WriterCluster",
      VariableIds = new[] { 1 }
    };

    var response = await client.PostAsJsonAsync("/api/clusters", dto);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
  }
}
