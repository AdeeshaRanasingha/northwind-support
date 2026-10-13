using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    public TicketQueriesTests()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();
        Seed();

        _queries = new TicketQueries(_db, new FixedClock());
    }

    [Fact]
    public async Task GetPagedAsync_WithStatus_ReturnsOnlyExactMatches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Status = TicketStatus.InProgress }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal(TicketStatus.InProgress, ticket.Status));
    }

    [Fact]
    public async Task GetPagedAsync_WithPriority_ReturnsOnlyExactMatches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High }, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal(TicketPriority.High, ticket.Priority));
        Assert.DoesNotContain(result.Items, ticket => ticket.Priority == TicketPriority.Critical);
    }

    [Fact]
    public async Task GetPagedAsync_WithStatusAndPriority_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High
            },
            CancellationToken.None);

        var ticket = Assert.Single(result.Items);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_WithoutFilters_PreservesAllResultsAndPagination()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Page = 1, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_WhenFilterHasNoMatches_ReturnsEmptyPagination()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Status = TicketStatus.Closed }, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_CountsFilteredRowsBeforePaging()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High, Page = 1, PageSize = 2 },
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void Seed()
    {
        var customer = new Customer(
            "Northwind Traders",
            "support@northwind.example",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow.AddDays(-30));
        var category = new Category(
            "Technical",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        _db.AddRange(customer, category);
        _db.SaveChanges();

        _db.AddRange(
            BuildTicket("TCK-0001", TicketPriority.Low, TicketStatus.New),
            BuildTicket("TCK-0002", TicketPriority.High, TicketStatus.New),
            BuildTicket("TCK-0003", TicketPriority.High, TicketStatus.InProgress),
            BuildTicket("TCK-0004", TicketPriority.Medium, TicketStatus.InProgress),
            BuildTicket("TCK-0005", TicketPriority.High, TicketStatus.Resolved),
            BuildTicket("TCK-0006", TicketPriority.Critical, TicketStatus.Open));
        _db.SaveChanges();

        Ticket BuildTicket(string reference, TicketPriority priority, TicketStatus status)
        {
            var ticket = Ticket.Raise(
                reference,
                $"Ticket {reference}",
                "A description long enough for a support ticket.",
                customer.Id,
                category.Id,
                priority,
                FixedClock.DefaultNow);

            if (status != TicketStatus.New)
            {
                ticket.ChangeStatus(status, FixedClock.DefaultNow.AddMinutes(5));
            }

            return ticket;
        }
    }
}
