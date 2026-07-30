using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Progesi.Api.Dtos;

namespace Progesi.Api.Tests;

[Collection(nameof(ProgesiApiTestCollection))]
public sealed class ProjectProvisioningTests
{
  private readonly ProgesiApiWebApplicationFactory _factory;

  public ProjectProvisioningTests(ProgesiApiWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Writer_Can_Provision_Two_Projects()
  {
    using var client = _factory.CreateClient().AsWriter();

    var projectA = await ProvisionAsync(client, "Project A");
    var projectB = await ProvisionAsync(client, "Project B");

    projectA.Id.Should().NotBe(projectB.Id);

    var listResponse = await client.GetAsync("/api/projects");
    listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var projects = await listResponse.Content.ReadFromJsonAsync<List<ProjectDto>>();
    projects.Should().Contain(p => p.Id == projectA.Id);
    projects.Should().Contain(p => p.Id == projectB.Id);
  }

  [Fact]
  public async Task Reader_Cannot_Provision_Projects()
  {
    using var client = _factory.CreateClient().AsReader();
    var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = "Blocked" });
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Variables_Are_Isolated_Between_Projects()
  {
    using var writer = _factory.CreateClient().AsWriter();
    var projectA = await ProvisionAsync(writer, "Isolation A");
    var projectB = await ProvisionAsync(writer, "Isolation B");

    var dto = new VariableUpsertDto
    {
      Id = 42,
      Name = "IsoVar",
      Value = System.Text.Json.JsonSerializer.SerializeToElement(7.5)
    };

    using (var clientA = _factory.CreateClient().AsWriterForProject(projectA.Id))
    {
      var post = await clientA.PostAsJsonAsync("/api/variables", dto);
      post.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    using (var clientB = _factory.CreateClient().AsReaderForProject(projectB.Id))
    {
      var getB = await clientB.GetAsync("/api/variables/42");
      getB.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    using (var clientA = _factory.CreateClient().AsReaderForProject(projectA.Id))
    {
      var getA = await clientA.GetAsync("/api/variables/42");
      getA.StatusCode.Should().Be(HttpStatusCode.OK);
    }
  }

  private static async Task<ProjectDto> ProvisionAsync(HttpClient client, string name)
  {
    var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = name });
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
    project.Should().NotBeNull();
    project!.Name.Should().Be(name);
    return project;
  }
}
