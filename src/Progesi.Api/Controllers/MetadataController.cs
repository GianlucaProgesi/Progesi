using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Progesi.Api.Auth;
using Progesi.Api.Dtos;
using Progesi.Api.Mapping;
using ProgesiCore;

namespace Progesi.Api.Controllers;

[ApiController]
[Route("api/metadata")]
public sealed class MetadataController : ControllerBase
{
  private readonly IMetadataRepository _repository;

  public MetadataController(IMetadataRepository repository)
  {
    _repository = repository;
  }

  [Authorize(Policy = AuthPolicies.Reader)]
  [HttpGet]
  public async Task<ActionResult<IReadOnlyList<MetadataDto>>> GetAll(
    [FromQuery] int skip = 0,
    [FromQuery] int take = 100,
    CancellationToken ct = default)
  {
    if (take <= 0)
      return BadRequest("take must be positive.");

    var items = await _repository.ListAsync(skip, take, ct);
    return Ok(items.Select(DomainMapping.ToDto).ToList());
  }

  [Authorize(Policy = AuthPolicies.Reader)]
  [HttpGet("{id:int}")]
  public async Task<ActionResult<MetadataDto>> GetById(int id, CancellationToken ct)
  {
    var metadata = await _repository.GetAsync(id, ct);
    if (metadata is null)
      return NotFound($"Metadata {id} was not found.");

    return Ok(DomainMapping.ToDto(metadata));
  }

  [Authorize(Policy = AuthPolicies.Writer)]
  [HttpPost]
  public async Task<ActionResult<MetadataDto>> Create([FromBody] MetadataUpsertDto dto, CancellationToken ct)
  {
    var validationError = DomainMapping.ValidateMetadataUpsert(dto);
    if (validationError != null)
      return BadRequest(validationError);

    if (dto.Id <= 0)
      return BadRequest("Metadata id must be positive for create.");

    var existing = await _repository.GetAsync(dto.Id, ct);
    if (existing is not null)
      return BadRequest($"Metadata {dto.Id} already exists. Use PUT to update.");

    await _repository.UpsertAsync(DomainMapping.ToDomain(dto), ct);
    var saved = await _repository.GetAsync(dto.Id, ct);
    if (saved is null)
      return BadRequest("Metadata could not be persisted.");

    return CreatedAtAction(nameof(GetById), new { id = saved.Id }, DomainMapping.ToDto(saved));
  }

  [Authorize(Policy = AuthPolicies.Writer)]
  [HttpPut("{id:int}")]
  public async Task<ActionResult<MetadataDto>> Update(int id, [FromBody] MetadataUpsertDto dto, CancellationToken ct)
  {
    if (dto.Id != id)
      return BadRequest("Route id and body id must match.");

    var validationError = DomainMapping.ValidateMetadataUpsert(dto);
    if (validationError != null)
      return BadRequest(validationError);

    var existing = await _repository.GetAsync(id, ct);
    if (existing is null)
      return NotFound($"Metadata {id} was not found.");

    // EF metadata repo upsert only inserts when the id row is absent; replace in place for PUT.
    await _repository.DeleteAsync(id, ct);
    await _repository.UpsertAsync(DomainMapping.ToDomain(dto), ct);
    var saved = await _repository.GetAsync(id, ct);
    return Ok(DomainMapping.ToDto(saved!));
  }

  [Authorize(Policy = AuthPolicies.Writer)]
  [HttpDelete("{id:int}")]
  public async Task<IActionResult> Delete(int id, CancellationToken ct)
  {
    var deleted = await _repository.DeleteAsync(id, ct);
    if (!deleted)
      return NotFound($"Metadata {id} was not found.");

    return NoContent();
  }
}
