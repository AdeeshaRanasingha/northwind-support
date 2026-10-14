using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

public sealed class TicketEscalationRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupportDbContext _db;

    public TicketEscalationRepositoryTests()
    {
        _connection.Open();
        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetByTicketIdAsync_ReturnsOnlyRequestedTicketNewestFirstWithIdTieBreaker()
    {
        var firstTicket = await SeedTicketAsync("TCK-9001");
        var otherTicket = await SeedTicketAsync("TCK-9002");
        var earlier = Escalation(firstTicket.Id, TicketPriority.Low, TicketPriority.Medium,
            FixedClock.DefaultNow, "Earlier", fromAgentId: null, toAgentId: null);
        var tiedOlderId = Escalation(firstTicket.Id, TicketPriority.Medium, TicketPriority.High,
            FixedClock.DefaultNow.AddHours(1), "Tied first", fromAgentId: null, toAgentId: null);
        var tiedNewerId = Escalation(firstTicket.Id, TicketPriority.High, TicketPriority.Critical,
            FixedClock.DefaultNow.AddHours(1), "Tied second", fromAgentId: null, toAgentId: null);
        _db.AddRange(
            earlier,
            tiedOlderId,
            tiedNewerId,
            Escalation(otherTicket.Id, TicketPriority.Low, TicketPriority.Medium,
                FixedClock.DefaultNow.AddDays(1), "Other ticket", null, null));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await new TicketEscalationRepository(_db)
            .GetByTicketIdAsync(firstTicket.Id, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal([tiedNewerId.Id, tiedOlderId.Id, earlier.Id], result.Select(item => item.Id));
        Assert.All(result, item => Assert.Equal(firstTicket.Id, item.TicketId));
        Assert.All(result, item => Assert.Equal(EntityState.Detached, _db.Entry(item).State));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Ticket> SeedTicketAsync(string reference)
    {
        var customer = new Customer(
            $"Customer {reference}", $"{reference}@example.com", null,
            CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category($"Category {reference}", false, false);
        _db.AddRange(customer, category);
        await _db.SaveChangesAsync();
        var ticket = Ticket.Raise(
            reference, "History test", "A sufficiently detailed description.",
            customer.Id, category.Id, TicketPriority.Low, FixedClock.DefaultNow);
        _db.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }

    private static TicketEscalation Escalation(
        int ticketId,
        TicketPriority from,
        TicketPriority to,
        DateTime atUtc,
        string reason,
        int? fromAgentId,
        int? toAgentId) =>
        new(ticketId, from, to, fromAgentId, toAgentId,
            FixedClock.DefaultNow.AddHours(72), atUtc.AddHours(24), reason, "shift-lead", atUtc);
}
