using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Authentication;

public sealed class AgentAuthenticationService(
    SupportDbContext db,
    IPasswordHasher<Agent> passwordHasher,
    JwtSettings settings,
    IClock clock)
    : IAgentAuthenticationService
{
    public async Task<AuthenticatedAgent?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        var normalizedEmail = email.Trim();
        var agent = await db.Set<Agent>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == normalizedEmail, ct);

        if (agent is null
            || !agent.IsActive
            || string.IsNullOrWhiteSpace(agent.PasswordHash)
            || passwordHasher.VerifyHashedPassword(agent, agent.PasswordHash, password)
                == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return ToAuthenticatedAgent(agent);
    }

    public async Task<AuthenticatedAgent?> GetActiveAgentAsync(int agentId, CancellationToken ct)
    {
        var agent = await db.Set<Agent>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == agentId && candidate.IsActive, ct);

        return agent is null ? null : ToAuthenticatedAgent(agent);
    }

    public IssuedAccessToken IssueAccessToken(AuthenticatedAgent agent)
    {
        var issuedAtUtc = clock.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(settings.ExpirationMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
            new(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new(ClaimTypes.Name, agent.FullName),
            new(ClaimTypes.Email, agent.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            credentials);

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }

    public static TokenValidationParameters CreateValidationParameters(JwtSettings settings) =>
        new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

    private static AuthenticatedAgent ToAuthenticatedAgent(Agent agent) =>
        new(agent.Id, agent.FullName, agent.Email);
}
