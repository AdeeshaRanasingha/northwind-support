using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.UnitTests.Presentation.Authentication;

public sealed class HealthIntegrationTests
{
    [Fact]
    public async Task Health_WithReachableDatabase_IsAnonymousAndBusinessApisStayProtected()
    {
        await using var factory = await AuthorizationApiFactory.CreateAsync();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task Health_WithUnavailableDatabase_Returns503WithoutSensitiveDetails()
    {
        await using var factory = await AuthorizationApiFactory.CreateAsync();
        using var unavailable = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            var missingDatabase = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db");
            services.AddDbContext<SupportDbContext>(options =>
                options.UseSqlite($"Data Source={missingDatabase};Mode=ReadOnly"));
        }));

        var response = await unavailable.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
    }
}
