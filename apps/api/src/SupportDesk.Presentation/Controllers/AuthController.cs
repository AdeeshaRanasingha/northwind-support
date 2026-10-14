using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private const string AuthenticationFailureMessage = "Invalid email or password.";

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        [FromServices] IAgentAuthenticationService authentication,
        CancellationToken ct)
    {
        var agent = await authentication.AuthenticateAsync(request.Email, request.Password, ct);
        if (agent is null)
        {
            return Unauthorized(AuthenticationFailure());
        }

        var token = authentication.IssueAccessToken(agent);
        return Ok(new LoginResponse(
            token.Value,
            token.ExpiresAtUtc,
            agent.Id,
            agent.FullName,
            agent.Email));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentAgentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentAgentDto>> Me(
        [FromServices] IAgentAuthenticationService authentication,
        CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var agentId))
        {
            return Unauthorized();
        }

        var agent = await authentication.GetActiveAgentAsync(agentId, ct);
        return agent is null
            ? Unauthorized()
            : Ok(new CurrentAgentDto(agent.Id, agent.FullName, agent.Email));
    }

    private static ProblemDetails AuthenticationFailure() =>
        new()
        {
            Title = "Unauthorized",
            Detail = AuthenticationFailureMessage,
            Status = StatusCodes.Status401Unauthorized
        };
}
