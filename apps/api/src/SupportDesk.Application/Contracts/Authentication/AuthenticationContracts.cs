namespace SupportDesk.Application.Contracts.Authentication;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    int AgentId,
    string FullName,
    string Email);

public sealed record CurrentAgentDto(int Id, string FullName, string Email);

public sealed record AuthenticatedAgent(int Id, string FullName, string Email);

public sealed record IssuedAccessToken(string Value, DateTime ExpiresAtUtc);
