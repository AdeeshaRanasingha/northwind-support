using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Authentication;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.UnitTests.Presentation.Authentication;

public sealed class AuthorizationIntegrationTests
{
    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/agents")]
    [InlineData("/api/customers")]
    public async Task ProtectedApis_WithoutToken_Return401(string path)
    {
        await using var factory = await AuthorizationApiFactory.CreateAsync();

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginAndSwagger_RemainPubliclyAccessible()
    {
        await using var factory = await AuthorizationApiFactory.CreateAsync();
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthorizationApiFactory.AgentEmail, AuthorizationApiFactory.Password));
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    [Fact]
    public async Task ValidToken_AllowsMultipleProtectedApisAndMe()
    {
        await using var factory = await AuthorizationApiFactory.CreateAsync();
        var client = factory.CreateClient();
        await AuthenticateAsync(client);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/agents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task MeWithoutToken_AndInvalidOrExpiredTokens_Return401()
    {
        await using var factory = await AuthorizationApiFactory.CreateAsync();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.value");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agents")).StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateExpiredToken());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customers")).StatusCode);
    }

    [Fact]
    public async Task Escalation_UsesAuthenticatedNameAndIgnoresSpoofedActor()
    {
        await using var factory = await AuthorizationApiFactory.CreateAsync();
        var client = factory.CreateClient();
        await AuthenticateAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{factory.TicketId}/escalate",
            new { reason = "Customer impact increased", escalatedBy = "Spoofed Actor" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var recordedActor = result.RootElement
            .GetProperty("escalation")
            .GetProperty("escalatedBy")
            .GetString();
        Assert.Equal(AuthorizationApiFactory.AgentName, recordedActor);
        Assert.NotEqual("Spoofed Actor", recordedActor);

        await using var scope = factory.Services.CreateAsyncScope();
        var record = await scope.ServiceProvider.GetRequiredService<SupportDbContext>()
            .Set<TicketEscalation>()
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(AuthorizationApiFactory.AgentName, record.EscalatedBy);
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthorizationApiFactory.AgentEmail, AuthorizationApiFactory.Password));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }
}

internal sealed class AuthorizationApiFactory : WebApplicationFactory<Program>
{
    internal const string AgentName = "Authenticated Agent";
    internal const string AgentEmail = "authenticated.agent@example.test";
    internal const string Password = "IntegrationPassword1!";
    private const string Issuer = "SupportDesk.IntegrationTests";
    private const string Audience = "SupportDesk.IntegrationTests.Client";
    private const string SigningKey = "integration-test-signing-key-at-least-32-characters";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public int TicketId { get; private set; }

    internal static async Task<AuthorizationApiFactory> CreateAsync()
    {
        var factory = new AuthorizationApiFactory();
        await factory.InitializeAsync();
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("ConnectionStrings:Default", "Server=unused");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:ExpirationMinutes", "60");
        builder.UseSetting("Sla:BaseWindowHours:Critical", "4");
        builder.UseSetting("Sla:BaseWindowHours:High", "8");
        builder.UseSetting("Sla:BaseWindowHours:Medium", "24");
        builder.UseSetting("Sla:BaseWindowHours:Low", "72");
        builder.UseSetting("Sla:PremiumCustomerMultiplier", "0.5");
        builder.UseSetting("Sla:MinimumWindowHours", "1");
        builder.UseSetting("Sla:AtRiskThresholdPercent", "25");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    internal string CreateExpiredToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [new Claim(ClaimTypes.NameIdentifier, "1")],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private async Task InitializeAsync()
    {
        _connection.Open();
        _ = Server;

        await using var scope = Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<SupportDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
        var agent = new Agent(AgentName, AgentEmail, maxOpenTickets: 10, now);
        agent.SetPasswordHash(
            services.GetRequiredService<IPasswordHasher<Agent>>().HashPassword(agent, Password));
        var customer = new Customer(
            "Integration Customer",
            "customer@example.test",
            phone: null,
            CustomerTier.Standard,
            now);
        var category = new Category(
            "General Support",
            requiresSpecialist: false,
            forcesCriticalPriority: false);
        db.AddRange(agent, customer, category);
        await db.SaveChangesAsync();

        var ticket = Ticket.Raise(
            "TCK-9001",
            "Integration ticket",
            "Verifies authenticated escalation over HTTP.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            now,
            now.AddHours(72),
            agent.Id);
        db.Add(ticket);
        await db.SaveChangesAsync();
        TicketId = ticket.Id;
    }
}
