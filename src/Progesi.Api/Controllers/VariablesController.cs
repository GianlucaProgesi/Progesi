using Microsoft.AspNetCore.Mvc;
using Progesi.Api.Dtos;
using Progesi.Api.Mapping;
using ProgesiCore;

namespace Progesi.Api.Controllers;

[ApiController]
[Route("api/variables")]
public sealed class VariablesController : ControllerBase
{
  private readonly IVariableRepository _repository;

  public VariablesController(IVariableRepository repository)
  {
    _repository = repository;
  }

  [HttpGet]
  public async Task<ActionResult<IReadOnlyList<VariableDto>>> GetAll(CancellationToken ct)
  {
    var variables = await _repository.GetAllAsync(ct);
    return Ok(variables.Select(DomainMapping.ToDto).ToList());
  }

  [HttpGet("{id:int}")]
  public async Task<ActionResult<VariableDto>> GetById(int id, CancellationToken ct)
  {
    var variable = await _repository.GetByIdAsync(id, ct);
    if (variable is null)
      return NotFound($"Variable {id} was not found.");

    return Ok(DomainMapping.ToDto(variable));
  }

  [HttpPost]
  public async Task<ActionResult<VariableDto>> Create([FromBody] VariableUpsertDto dto, CancellationToken ct)
  {
    var validationError = DomainMapping.ValidateVariableUpsert(dto);
    if (validationError != null)
      return BadRequest(validationError);

    var existing = await _repository.GetByIdAsync(dto.Id, ct);
    if (existing is not null)
      return BadRequest($"Variable {dto.Id} already exists. Use PUT to update.");

    var saved = await _repository.SaveAsync(DomainMapping.ToDomain(dto), ct);
    return CreatedAtAction(nameof(GetById), new { id = saved.Id }, DomainMapping.ToDto(saved));
  }

  [HttpPut("{id:int}")]
  public async Task<ActionResult<VariableDto>> Update(int id, [FromBody] VariableUpsertDto dto, CancellationToken ct)
  {
    if (dto.Id != id)
      return BadRequest("Route id and body id must match.");

    var validationError = DomainMapping.ValidateVariableUpsert(dto);
    if (validationError != null)
      return BadRequest(validationError);

    var existing = await _repository.GetByIdAsync(id, ct);
    if (existing is null)
      return NotFound($"Variable {id} was not found.");

    var saved = await _repository.SaveAsync(DomainMapping.ToDomain(dto), ct);
    return Ok(DomainMapping.ToDto(saved));
  }

  [HttpDelete("{id:int}")]
  public async Task<IActionResult> Delete(int id, CancellationToken ct)
  {
    var deleted = await _repository.DeleteAsync(id, ct);
    if (!deleted)
      return NotFound($"Variable {id} was not found.");

    return NoContent();
  }
}
