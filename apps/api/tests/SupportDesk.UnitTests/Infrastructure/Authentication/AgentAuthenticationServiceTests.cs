using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Authentication;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Authentication;

public sealed class AgentAuthenticationServiceTests : IAsyncLifetime
{
    private const string Password = "CorrectPassword1!";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly PasswordHasher<Agent> passwordHasher = new();
    private readonly FixedClock clock = new();
    private SupportDbContext db = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task Authenticate_WithValidCredentials_ReturnsAgent()
    {
        var agent = await AddAgentAsync("agent@example.test", withPassword: true);
        var service = Service();

        var result = await service.AuthenticateAsync(agent.Email, Password, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(agent.Id, result.Id);
        Assert.Equal(agent.FullName, result.FullName);
        Assert.Equal(agent.Email, result.Email);
    }

    [Theory]
    [InlineData("agent@example.test", "WrongPassword1!", true, true)]
    [InlineData("unknown@example.test", Password, true, true)]
    [InlineData("agent@example.test", Password, false, true)]
    [InlineData("agent@example.test", Password, true, false)]
    public async Task Authenticate_WithInvalidCredentials_ReturnsNull(
        string email,
        string password,
        bool active,
        bool withPassword)
    {
        var agent = await AddAgentAsync("agent@example.test", withPassword);
        if (!active)
        {
            agent.Deactivate();
            await db.SaveChangesAsync();
        }

        var result = await Service().AuthenticateAsync(email, password, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void IssueAccessToken_ContainsIdentityAndConfiguredExpiration()
    {
        var service = Service();
        var agent = new AuthenticatedAgent(42, "Alex Turner", "alex@example.test");

        var issued = service.IssueAccessToken(agent);
        var validationParameters = AgentAuthenticationService.CreateValidationParameters(Settings());
        validationParameters.ValidateLifetime = false;
        var principal = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ValidateToken(issued.Value, validationParameters, out _);

        Assert.Equal(clock.UtcNow.AddMinutes(60), issued.ExpiresAtUtc);
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(agent.FullName, principal.Identity!.Name);
        Assert.Equal(agent.Email, principal.FindFirstValue(ClaimTypes.Email));
    }

    [Fact]
    public void Validation_RejectsExpiredAndIncorrectlySignedTokens()
    {
        var expiredClock = new FixedClock(DateTime.UtcNow.AddHours(-2));
        var expiredService = Service(expiredClock, expirationMinutes: 1);
        var token = expiredService.IssueAccessToken(
            new AuthenticatedAgent(42, "Alex Turner", "alex@example.test"));
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

        Assert.Throws<SecurityTokenExpiredException>(() =>
            handler.ValidateToken(
                token.Value,
                AgentAuthenticationService.CreateValidationParameters(Settings(expirationMinutes: 1)),
                out _));

        var wrongSettings = Settings();
        wrongSettings.SigningKey = "a-different-test-signing-key-of-32-characters";
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            handler.ValidateToken(
                Service().IssueAccessToken(
                    new AuthenticatedAgent(42, "Alex Turner", "alex@example.test")).Value,
                AgentAuthenticationService.CreateValidationParameters(wrongSettings),
                out _));
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    private async Task<Agent> AddAgentAsync(string email, bool withPassword)
    {
        var agent = new Agent("Alex Turner", email, 5, clock.UtcNow);
        if (withPassword)
        {
            agent.SetPasswordHash(passwordHasher.HashPassword(agent, Password));
        }

        db.Add(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    private AgentAuthenticationService Service(
        FixedClock? customClock = null,
        int expirationMinutes = 60) =>
        new(db, passwordHasher, Settings(expirationMinutes), customClock ?? clock);

    private static JwtSettings Settings(int expirationMinutes = 60) =>
        new()
        {
            Issuer = "SupportDesk.Tests",
            Audience = "SupportDesk.Tests.Client",
            SigningKey = "unit-test-signing-key-at-least-32-characters",
            ExpirationMinutes = expirationMinutes
        };
}
