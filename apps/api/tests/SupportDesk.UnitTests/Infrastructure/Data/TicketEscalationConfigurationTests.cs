using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

public sealed class TicketEscalationConfigurationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupportDbContext _db;

    public TicketEscalationConfigurationTests()
    {
        _connection.Open();
        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public void Model_ConfiguresRequiredFieldsLengthsIndexAndRestrictedForeignKeys()
    {
        var entity = _db.Model.FindEntityType(typeof(TicketEscalation));

        Assert.NotNull(entity);
        Assert.False(entity.FindProperty(nameof(TicketEscalation.TicketId))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(TicketEscalation.Reason))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(TicketEscalation.EscalatedBy))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(TicketEscalation.FromAgentId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(TicketEscalation.ToAgentId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(TicketEscalation.FromDueAtUtc))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(TicketEscalation.ToDueAtUtc))!.IsNullable);
        Assert.Equal(500, entity.FindProperty(nameof(TicketEscalation.Reason))!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty(nameof(TicketEscalation.EscalatedBy))!.GetMaxLength());
        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(TicketEscalation.TicketId));
        Assert.All(entity.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Equal(3, entity.GetForeignKeys().Count());
    }

    [Fact]
    public async Task Save_WithNullableAgentAndDueDateValues_RoundTripsAuditRecord()
    {
        var ticket = await SeedTicketAsync();
        var escalation = new TicketEscalation(
            ticket.Id,
            TicketPriority.Low,
            TicketPriority.High,
            fromAgentId: null,
            toAgentId: null,
            fromDueAtUtc: null,
            toDueAtUtc: null,
            "Customer impact increased.",
            "team.lead@example.com",
            FixedClock.DefaultNow);

        _db.Add(escalation);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var saved = await _db.Set<TicketEscalation>().SingleAsync();
        Assert.Equal(ticket.Id, saved.TicketId);
        Assert.Null(saved.FromAgentId);
        Assert.Null(saved.ToAgentId);
        Assert.Null(saved.FromDueAtUtc);
        Assert.Null(saved.ToDueAtUtc);
        Assert.Equal("Customer impact increased.", saved.Reason);
        Assert.Equal("team.lead@example.com", saved.EscalatedBy);
    }

    [Theory]
    [InlineData(501, false)]
    [InlineData(101, true)]
    public void Constructor_RejectsTextBeyondConfiguredMaximum(int length, bool escalatedBy)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TicketEscalation(
            ticketId: 1,
            TicketPriority.Low,
            TicketPriority.High,
            null,
            null,
            null,
            null,
            escalatedBy ? "Reason" : new string('r', length),
            escalatedBy ? new string('u', length) : "team.lead@example.com",
            FixedClock.DefaultNow));

        Assert.NotNull(exception);
    }

    [Fact]
    public async Task DeleteTicket_WithEscalationHistory_IsBlocked()
    {
        var ticket = await SeedTicketAsync();
        _db.Add(new TicketEscalation(
            ticket.Id,
            TicketPriority.Medium,
            TicketPriority.High,
            null,
            null,
            null,
            FixedClock.DefaultNow.AddHours(8),
            "Escalated for audit retention test.",
            "team.lead@example.com",
            FixedClock.DefaultNow));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var persistedTicket = await _db.Set<Ticket>().SingleAsync(candidate => candidate.Id == ticket.Id);
        _db.Remove(persistedTicket);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Ticket> SeedTicketAsync()
    {
        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow);
        var category = new Category("Technical", false, false);
        _db.AddRange(customer, category);
        await _db.SaveChangesAsync();
        var ticket = Ticket.Raise(
            "TCK-9001",
            "Audit history test",
            "A sufficiently detailed ticket description.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            FixedClock.DefaultNow);
        _db.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }
}
