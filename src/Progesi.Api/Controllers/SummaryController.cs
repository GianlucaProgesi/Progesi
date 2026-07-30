using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Progesi.Api.Auth;
using Progesi.Api.Dtos;
using Progesi.Api.Services;

namespace Progesi.Api.Controllers;

[ApiController]
[Route("api/summary")]
public sealed class SummaryController : ControllerBase
{
  private readonly IProjectSummaryService _summaryService;

  public SummaryController(IProjectSummaryService summaryService)
  {
    _summaryService = summaryService;
  }

  [Authorize(Policy = AuthPolicies.Reader)]
  [HttpGet]
  public async Task<ActionResult<SummaryDto>> GetSummary(CancellationToken ct)
  {
    var summary = await _summaryService.GetSummaryAsync(ct);
    return Ok(summary);
  }

  [Authorize(Policy = AuthPolicies.Reader)]
  [HttpGet("value-types")]
  public async Task<ActionResult<ValueTypeBreakdownDto>> GetValueTypeBreakdown(CancellationToken ct)
  {
    var breakdown = await _summaryService.GetValueTypeBreakdownAsync(ct);
    return Ok(breakdown);
  }
}
