using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Progesi.Api.Tests;

/// <summary>
/// Test-only authentication handler. Activated when <c>Progesi:UseTestAuthentication</c> is true.
/// Supply roles via the <see cref="RolesHeaderName"/> request header (comma-separated).
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
  public const string SchemeName = "Test";
  public const string RolesHeaderName = "X-Test-Roles";

  public TestAuthHandler(
      IOptionsMonitor<AuthenticationSchemeOptions> options,
      ILoggerFactory logger,
      UrlEncoder encoder)
      : base(options, logger, encoder)
  {
  }

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(RolesHeaderName, out var roleHeader) ||
        string.IsNullOrWhiteSpace(roleHeader))
    {
      return Task.FromResult(AuthenticateResult.NoResult());
    }

    var claims = new List<Claim>
    {
      new(ClaimTypes.Name, "test-user"),
      new(ClaimTypes.NameIdentifier, "test-user-id")
    };

    foreach (var role in roleHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      claims.Add(new Claim(ClaimTypes.Role, role));
    }

    var identity = new ClaimsIdentity(claims, SchemeName);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, SchemeName);
    return Task.FromResult(AuthenticateResult.Success(ticket));
  }
}
