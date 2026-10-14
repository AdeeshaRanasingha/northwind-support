using SupportDesk.Application.Contracts.Authentication;

namespace SupportDesk.Application.Abstractions;

public interface IAgentAuthenticationService
{
    Task<AuthenticatedAgent?> AuthenticateAsync(string email, string password, CancellationToken ct);

    Task<AuthenticatedAgent?> GetActiveAgentAsync(int agentId, CancellationToken ct);

    IssuedAccessToken IssueAccessToken(AuthenticatedAgent agent);
}
