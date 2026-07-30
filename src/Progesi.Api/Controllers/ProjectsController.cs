using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Progesi.Api.Auth;
using Progesi.Api.Dtos;
using Progesi.Api.Projects;

namespace Progesi.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
  private readonly IProjectProvisioningService _provisioningService;
  private readonly IProjectRegistry _registry;

  public ProjectsController(
      IProjectProvisioningService provisioningService,
      IProjectRegistry registry)
  {
    _provisioningService = provisioningService;
    _registry = registry;
  }

  [Authorize(Policy = AuthPolicies.Writer)]
  [HttpPost]
  public ActionResult<ProjectDto> Create([FromBody] CreateProjectRequest request)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
      return BadRequest("Project name is required.");

    var entry = _provisioningService.Provision(request.Name);
    var dto = ToDto(entry);
    return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
  }

  [Authorize(Policy = AuthPolicies.Writer)]
  [HttpGet]
  public ActionResult<IReadOnlyList<ProjectDto>> List()
  {
    var projects = _registry.GetAll().Select(ToDto).ToList();
    return Ok(projects);
  }

  [Authorize(Policy = AuthPolicies.Writer)]
  [HttpGet("{id}")]
  public ActionResult<ProjectDto> GetById(string id)
  {
    var entry = _registry.GetById(id);
    if (entry == null)
      return NotFound($"Project '{id}' was not found.");

    return Ok(ToDto(entry));
  }

  private static ProjectDto ToDto(ProjectEntry entry) => new()
  {
    Id = entry.Id,
    Name = entry.Name
  };
}
