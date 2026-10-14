using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

public sealed class AgentCredentialsTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private SupportDbContext db = null!;
    private readonly PasswordHasher<Agent> passwordHasher = new();

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public void PasswordHasher_UsesSaltAndVerifiesOnlyTheCorrectPassword()
    {
        var first = CreateAgent("first@example.test");
        var second = CreateAgent("second@example.test");

        first.SetPasswordHash(passwordHasher.HashPassword(first, "CorrectPassword1!"));
        second.SetPasswordHash(passwordHasher.HashPassword(second, "CorrectPassword1!"));

        Assert.NotEqual(first.PasswordHash, second.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(first, first.PasswordHash!, "CorrectPassword1!"));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            passwordHasher.VerifyHashedPassword(first, first.PasswordHash!, "WrongPassword1!"));
    }

    [Fact]
    public async Task DevelopmentSeeder_BackfillsMissingCredentialsIdempotently()
    {
        var agent = CreateAgent("inactive@example.test");
        agent.Deactivate();
        var customer = new Customer(
            "Credential Test Customer",
            "customer@example.test",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow);
        var category = new Category("Credential Test Category", false, false);
        db.AddRange(agent, customer, category);
        await db.SaveChangesAsync();
        db.Add(Ticket.Raise(
            "TCK-9901",
            "Credential seeding test",
            "Ensures existing development agents receive credentials.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            FixedClock.DefaultNow));
        await db.SaveChangesAsync();

        var seeder = new SupportDbSeeder(
            db,
            new FixedClock(),
            passwordHasher,
            NullLogger<SupportDbSeeder>.Instance);

        await seeder.SeedAsync();
        var firstHash = agent.PasswordHash;

        Assert.NotNull(firstHash);
        Assert.DoesNotContain(SupportDbSeeder.DevelopmentPassword, firstHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(
                agent,
                firstHash,
                SupportDbSeeder.DevelopmentPassword));

        await seeder.SeedAsync();

        Assert.Equal(firstHash, agent.PasswordHash);
    }

    [Fact]
    public void Model_ConfiguresAnOptionalBoundedPasswordHash()
    {
        var property = db.Model.FindEntityType(typeof(Agent))!
            .FindProperty(nameof(Agent.PasswordHash))!;

        Assert.True(property.IsNullable);
        Assert.Equal(Agent.MaxPasswordHashLength, property.GetMaxLength());
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    private static Agent CreateAgent(string email) =>
        new("Credential Test Agent", email, 5, FixedClock.DefaultNow);
}
