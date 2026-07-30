using Microsoft.AspNetCore.Mvc;
using Progesi.Api.Dtos;
using Progesi.Api.Mapping;
using ProgesiCore;

namespace Progesi.Api.Controllers;

[ApiController]
[Route("api/clusters")]
public sealed class ClustersController : ControllerBase
{
  private readonly IProgesiVariableClusterRepository _repository;

  public ClustersController(IProgesiVariableClusterRepository repository)
  {
    _repository = repository;
  }

  [HttpGet]
  public async Task<ActionResult<IReadOnlyList<ClusterDto>>> GetAll(CancellationToken ct)
  {
    var clusters = await _repository.GetAllAsync(ct);
    return Ok(clusters.Select(DomainMapping.ToDto).ToList());
  }

  [HttpGet("{id:int}")]
  public async Task<ActionResult<ClusterDto>> GetById(int id, CancellationToken ct)
  {
    var cluster = await _repository.GetByIdAsync(id, ct);
    if (cluster is null)
      return NotFound($"Cluster {id} was not found.");

    return Ok(DomainMapping.ToDto(cluster));
  }

  [HttpPost]
  public async Task<ActionResult<ClusterDto>> Create([FromBody] ClusterUpsertDto dto, CancellationToken ct)
  {
    var validationError = DomainMapping.ValidateClusterUpsert(dto);
    if (validationError != null)
      return BadRequest(validationError);

    var existing = await _repository.GetByIdAsync(dto.Id, ct);
    if (existing is not null)
      return BadRequest($"Cluster {dto.Id} already exists. Use PUT to update.");

    var saved = await _repository.SaveAsync(DomainMapping.ToDomain(dto), ct);
    return CreatedAtAction(nameof(GetById), new { id = saved.Id }, DomainMapping.ToDto(saved));
  }

  [HttpPut("{id:int}")]
  public async Task<ActionResult<ClusterDto>> Update(int id, [FromBody] ClusterUpsertDto dto, CancellationToken ct)
  {
    if (dto.Id != id)
      return BadRequest("Route id and body id must match.");

    var validationError = DomainMapping.ValidateClusterUpsert(dto);
    if (validationError != null)
      return BadRequest(validationError);

    var existing = await _repository.GetByIdAsync(id, ct);
    if (existing is null)
      return NotFound($"Cluster {id} was not found.");

    var saved = await _repository.SaveAsync(DomainMapping.ToDomain(dto), ct);
    return Ok(DomainMapping.ToDto(saved));
  }

  [HttpDelete("{id:int}")]
  public async Task<IActionResult> Delete(int id, CancellationToken ct)
  {
    var deleted = await _repository.DeleteAsync(id, ct);
    if (!deleted)
      return NotFound($"Cluster {id} was not found.");

    return NoContent();
  }
}
