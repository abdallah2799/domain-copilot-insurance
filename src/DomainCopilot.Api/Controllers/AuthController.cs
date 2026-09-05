using DomainCopilot.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

/// <summary>FR-8's only unauthenticated surface — every other controller requires a valid bearer
/// token by default (see Program.cs's fallback authorization policy).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Username, request.Password, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    public sealed record LoginRequest(string Username, string Password);
}
