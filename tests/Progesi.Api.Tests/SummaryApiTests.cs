using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Progesi.Api.Dtos;

namespace Progesi.Api.Tests;

[Collection(nameof(ProgesiApiTestCollection))]
public sealed class SummaryApiTests
{
  private readonly ProgesiApiWebApplicationFactory _factory;

  public SummaryApiTests(ProgesiApiWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Summary_Reflects_Seeded_Project_Data()
  {
    using var provisioner = _factory.CreateClient().AsWriter();
    var project = await ProvisionAsync(provisioner, "Summary Seed");

    using var client = _factory.CreateClient().AsReaderForProject(project.Id);
    await SeedSummaryDataAsync(_factory.CreateClient().AsWriterForProject(project.Id));

    var response = await client.GetAsync("/api/summary");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var summary = await response.Content.ReadFromJsonAsync<SummaryDto>();
    summary.Should().NotBeNull();
    summary!.VariableCount.Should().Be(2);
    summary.MetadataCount.Should().Be(1);
    summary.ClusterCount.Should().Be(1);
    summary.VariablesWithMetadataCount.Should().Be(1);
    summary.MetadataCoveragePercent.Should().Be(50d);
    summary.ClusterMembership.DistinctVariablesReferenced.Should().Be(2);
    summary.ClusterMembership.AverageVariablesPerCluster.Should().Be(2d);
    summary.ValueTypeBreakdown.Double.Should().Be(1);
    summary.ValueTypeBreakdown.String.Should().Be(1);
  }

  [Fact]
  public async Task ValueTypes_Endpoint_Matches_Summary_Breakdown()
  {
    using var provisioner = _factory.CreateClient().AsWriter();
    var project = await ProvisionAsync(provisioner, "Value Types");
    await SeedSummaryDataAsync(_factory.CreateClient().AsWriterForProject(project.Id));

    using var client = _factory.CreateClient().AsReaderForProject(project.Id);
    var summary = await client.GetFromJsonAsync<SummaryDto>("/api/summary");
    var breakdown = await client.GetFromJsonAsync<ValueTypeBreakdownDto>("/api/summary/value-types");

    breakdown.Should().BeEquivalentTo(summary!.ValueTypeBreakdown);
  }

  [Fact]
  public async Task Summary_Unauthenticated_Returns_401()
  {
    using var client = _factory.CreateClient().AsUnauthenticated();
    var response = await client.GetAsync("/api/summary");
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Summary_Is_Project_Scoped()
  {
    using var provisioner = _factory.CreateClient().AsWriter();
    var projectA = await ProvisionAsync(provisioner, "Summary A");
    var projectB = await ProvisionAsync(provisioner, "Summary B");

    await SeedSummaryDataAsync(_factory.CreateClient().AsWriterForProject(projectA.Id));

    using var clientB = _factory.CreateClient().AsReaderForProject(projectB.Id);
    var summaryB = await clientB.GetFromJsonAsync<SummaryDto>("/api/summary");

    summaryB!.VariableCount.Should().Be(0);
    summaryB.MetadataCount.Should().Be(0);
    summaryB.ClusterCount.Should().Be(0);
  }

  private static async Task<ProjectDto> ProvisionAsync(HttpClient client, string name)
  {
    var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = name });
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
  }

  private static async Task SeedSummaryDataAsync(HttpClient client)
  {
    var metadata = new MetadataUpsertDto
    {
      Id = 1,
      CreatedBy = "summary-test",
      AdditionalInfo = "note"
    };
    (await client.PostAsJsonAsync("/api/metadata", metadata)).StatusCode.Should().Be(HttpStatusCode.Created);

    var withMetadata = new VariableUpsertDto
    {
      Id = 1,
      Name = "Width",
      Value = System.Text.Json.JsonSerializer.SerializeToElement(12.5),
      MetadataIds = new[] { 1 }
    };
    (await client.PostAsJsonAsync("/api/variables", withMetadata)).StatusCode.Should().Be(HttpStatusCode.Created);

    var withoutMetadata = new VariableUpsertDto
    {
      Id = 2,
      Name = "Label",
      Value = System.Text.Json.JsonSerializer.SerializeToElement("east")
    };
    (await client.PostAsJsonAsync("/api/variables", withoutMetadata)).StatusCode.Should().Be(HttpStatusCode.Created);

    var cluster = new ClusterUpsertDto
    {
      Id = 1,
      Name = "Pair",
      VariableIds = new[] { 2, 1 }
    };
    (await client.PostAsJsonAsync("/api/clusters", cluster)).StatusCode.Should().Be(HttpStatusCode.Created);
  }
}
