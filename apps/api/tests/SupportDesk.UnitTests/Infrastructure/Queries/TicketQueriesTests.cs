using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
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
    private int _technicalCategoryId;
    private int _billingCategoryId;
    private int _northwindCustomerId;
    private int _contosoCustomerId;
    private int _alexAgentId;
    private int _saraAgentId;

    public TicketQueriesTests()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();
        Seed();

        _queries = new TicketQueries(_db, new FixedClock(), new SlaEvaluator(SlaSettings()));
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

    [Fact]
    public async Task GetPagedAsync_WithCategory_ReturnsOnlyExactMatches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { CategoryId = _technicalCategoryId }, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal(_technicalCategoryId, ticket.Category.Id));
    }

    [Fact]
    public async Task GetPagedAsync_WithCustomer_ReturnsOnlyExactMatches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { CustomerId = _contosoCustomerId }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal(_contosoCustomerId, ticket.Customer.Id));
    }

    [Fact]
    public async Task GetPagedAsync_WithAssignedAgent_ExcludesUnassignedTickets()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { AssignedAgentId = _alexAgentId }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal(_alexAgentId, ticket.AssignedAgent!.Id));
    }

    [Fact]
    public async Task GetPagedAsync_WithStatusAndCategory_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Status = TicketStatus.InProgress, CategoryId = _technicalCategoryId },
            CancellationToken.None);

        Assert.Equal("TCK-0003", Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task GetPagedAsync_WithPriorityAndCustomer_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High, CustomerId = _northwindCustomerId },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket =>
        {
            Assert.Equal(TicketPriority.High, ticket.Priority);
            Assert.Equal(_northwindCustomerId, ticket.Customer.Id);
        });
    }

    [Fact]
    public async Task GetPagedAsync_WithCategoryCustomerAndAgent_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                CategoryId = _billingCategoryId,
                CustomerId = _contosoCustomerId,
                AssignedAgentId = _saraAgentId
            },
            CancellationToken.None);

        Assert.Equal("TCK-0004", Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task GetPagedAsync_WithAllFiveExactFilters_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                CategoryId = _technicalCategoryId,
                CustomerId = _contosoCustomerId,
                AssignedAgentId = _alexAgentId
            },
            CancellationToken.None);

        Assert.Equal("TCK-0003", Assert.Single(result.Items).Reference);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("customer")]
    [InlineData("agent")]
    public async Task GetPagedAsync_WithNonexistentForeignKey_ReturnsEmptyPagination(string filter)
    {
        var query = filter switch
        {
            "category" => new TicketQuery { CategoryId = int.MaxValue },
            "customer" => new TicketQuery { CustomerId = int.MaxValue },
            "agent" => new TicketQuery { AssignedAgentId = int.MaxValue },
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

        var result = await _queries.GetPagedAsync(query, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_CountsCategoryFilteredRowsBeforePaging()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { CategoryId = _billingCategoryId, Page = 2, PageSize = 2 },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.All(result.Items, ticket => Assert.Equal(_billingCategoryId, ticket.Category.Id));
    }

    [Fact]
    public async Task GetPagedAsync_WithUnassignedOnlyTrue_ReturnsOnlyUnassignedTickets()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Null(ticket.AssignedAgent));
    }

    [Fact]
    public async Task GetPagedAsync_WithUnassignedOnlyFalse_PreservesAssignedAndUnassignedTickets()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = false }, CancellationToken.None);

        Assert.Equal(6, result.TotalCount);
        Assert.Contains(result.Items, ticket => ticket.AssignedAgent is null);
        Assert.Contains(result.Items, ticket => ticket.AssignedAgent is not null);
    }

    [Fact]
    public async Task GetPagedAsync_WithoutUnassignedOnly_PreservesAssignedAndUnassignedTickets()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery(), CancellationToken.None);

        Assert.Equal(6, result.TotalCount);
        Assert.Contains(result.Items, ticket => ticket.AssignedAgent is null);
        Assert.Contains(result.Items, ticket => ticket.AssignedAgent is not null);
    }

    [Fact]
    public async Task GetPagedAsync_WithUnassignedOnlyAndPriority_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true, Priority = TicketPriority.Critical },
            CancellationToken.None);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-0006", ticket.Reference);
        Assert.Null(ticket.AssignedAgent);
    }

    [Fact]
    public async Task GetPagedAsync_WithAssignedAgentAndUnassignedOnly_ReturnsEmptyPagination()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { AssignedAgentId = _alexAgentId, UnassignedOnly = true },
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_CountsUnassignedRowsBeforePaging()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true, Page = 2, PageSize = 1 },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Null(result.Items[0].AssignedAgent);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesTicketTitle()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = "Printer failure" }, CancellationToken.None);

        Assert.Equal("TCK-0001", Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesTicketReference()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = "TCK-0003" }, CancellationToken.None);

        Assert.Equal("TCK-0003", Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesCustomerName()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = "Contoso" }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal("Contoso Ltd", ticket.Customer.Name));
    }

    [Fact]
    public async Task GetPagedAsync_TrimsSearchWhitespace()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = "  Printer failure  " }, CancellationToken.None);

        Assert.Equal("TCK-0001", Assert.Single(result.Items).Reference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPagedAsync_WithBlankSearch_DoesNotRestrictResults(string search)
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = search }, CancellationToken.None);

        Assert.Equal(6, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_WithSearchAndPriority_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = "Billing portal", Priority = TicketPriority.High },
            CancellationToken.None);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-0002", ticket.Reference);
        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Fact]
    public async Task GetPagedAsync_WithSearchAndAllCompatibleExactFilters_CombinesThemWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Search = "Email delivery",
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                CategoryId = _technicalCategoryId,
                CustomerId = _contosoCustomerId,
                AssignedAgentId = _alexAgentId
            },
            CancellationToken.None);

        Assert.Equal("TCK-0003", Assert.Single(result.Items).Reference);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task GetPagedAsync_WithSqlWildcardCharacters_TreatsThemAsLiterals(string search)
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = search }, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_WhenSearchHasNoMatches_ReturnsEmptyPagination()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = "does-not-exist" }, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_CountsSearchResultsBeforePaging()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Search = "Northwind", Page = 2, PageSize = 2 },
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.All(result.Items, ticket => Assert.Equal("Northwind Traders", ticket.Customer.Name));
        Assert.Equal(["TCK-0002", "TCK-0001"], result.Items.Select(ticket => ticket.Reference));
    }

    [Fact]
    public async Task GetDetailAsync_ForEscalatedTicket_UsesLatestEscalationAsSlaWindowStart()
    {
        var createdAtUtc = FixedClock.DefaultNow.AddDays(-1);
        var ticket = Ticket.Raise(
            "TCK-0099",
            "Escalated SLA query",
            "A description long enough for a support ticket.",
            _northwindCustomerId,
            _technicalCategoryId,
            TicketPriority.Medium,
            createdAtUtc,
            createdAtUtc.AddHours(24));
        _db.Add(ticket);
        await _db.SaveChangesAsync();
        var escalationTimeUtc = FixedClock.DefaultNow.AddHours(-6);
        var decision = new TicketEscalationService(new SlaDeadlineCalculator(SlaSettings())).Escalate(
            ticket,
            await _db.Set<Category>().SingleAsync(category => category.Id == _technicalCategoryId),
            CustomerTier.Standard,
            [],
            "Customer impact increased",
            "shift-lead",
            escalationTimeUtc);
        _db.Add(new TicketEscalation(
            ticket.Id,
            decision.FromPriority,
            decision.ToPriority,
            decision.FromAgentId,
            decision.ToAgentId,
            decision.FromDueAtUtc,
            decision.ToDueAtUtc,
            decision.Reason,
            decision.EscalatedBy,
            decision.EscalatedAtUtc));
        await _db.SaveChangesAsync();

        var result = await _queries.GetDetailAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(FixedClock.DefaultNow.AddHours(2), result.DueAtUtc);
        Assert.Equal(SlaStatus.AtRisk, result.SlaStatus);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static SlaSettings SlaSettings() => new()
    {
        BaseWindowHours = new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    private void Seed()
    {
        var northwind = new Customer(
            "Northwind Traders",
            "support@northwind.example",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow.AddDays(-30));
        var contoso = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            null,
            CustomerTier.Premium,
            FixedClock.DefaultNow.AddDays(-20));
        var technical = new Category(
            "Technical",
            requiresSpecialist: false,
            forcesCriticalPriority: false);
        var billing = new Category(
            "Billing",
            requiresSpecialist: false,
            forcesCriticalPriority: false);
        var alex = new Agent("Alex Turner", "alex@example.com", 10, FixedClock.DefaultNow.AddDays(-10));
        var sara = new Agent("Sara Lindqvist", "sara@example.com", 10, FixedClock.DefaultNow.AddDays(-10));

        _db.AddRange(northwind, contoso, technical, billing, alex, sara);
        _db.SaveChanges();

        _technicalCategoryId = technical.Id;
        _billingCategoryId = billing.Id;
        _northwindCustomerId = northwind.Id;
        _contosoCustomerId = contoso.Id;
        _alexAgentId = alex.Id;
        _saraAgentId = sara.Id;

        _db.AddRange(
            BuildTicket("TCK-0001", "Printer failure in warehouse", TicketPriority.Low, TicketStatus.New, northwind.Id, technical.Id, null),
            BuildTicket("TCK-0002", "Billing portal unavailable", TicketPriority.High, TicketStatus.New, northwind.Id, billing.Id, alex.Id),
            BuildTicket("TCK-0003", "Email delivery delayed", TicketPriority.High, TicketStatus.InProgress, contoso.Id, technical.Id, alex.Id),
            BuildTicket("TCK-0004", "Laptop setup request", TicketPriority.Medium, TicketStatus.InProgress, contoso.Id, billing.Id, sara.Id),
            BuildTicket("TCK-0005", "Refund request pending", TicketPriority.High, TicketStatus.Resolved, northwind.Id, billing.Id, sara.Id),
            BuildTicket("TCK-0006", "Security alert investigation", TicketPriority.Critical, TicketStatus.Open, northwind.Id, technical.Id, null));
        _db.SaveChanges();

        Ticket BuildTicket(
            string reference,
            string title,
            TicketPriority priority,
            TicketStatus status,
            int customerId,
            int categoryId,
            int? assignedAgentId)
        {
            var ticket = Ticket.Raise(
                reference,
                title,
                "A description long enough for a support ticket.",
                customerId,
                categoryId,
                priority,
                FixedClock.DefaultNow);

            if (status != TicketStatus.New)
            {
                ticket.ChangeStatus(status, FixedClock.DefaultNow.AddMinutes(5));
            }

            if (assignedAgentId is { } agentId)
            {
                ticket.AssignTo(agentId, FixedClock.DefaultNow.AddMinutes(10));
            }

            return ticket;
        }
    }
}
