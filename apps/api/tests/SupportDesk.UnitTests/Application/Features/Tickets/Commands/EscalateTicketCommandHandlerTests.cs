using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsUpdatedTicketAndCompleteHistoryInOneSave()
    {
        var context = ContextWithTicket(TicketPriority.Low, assignedAgentId: 2);
        var ticket = await ExistingTicket(context);
        var previousDueAtUtc = ticket.DueAtUtc;
        var agent = Agent(context, 2, maxOpenTickets: 5);
        context.AssignmentCandidates = [new AgentAssignmentCandidate(agent, [ticket])];
        context.Clock.Advance(TimeSpan.FromHours(6));

        var result = await context.EscalateTicket.HandleAsync(
            Command(ticket.Id), CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Equal(agent.Id, ticket.AssignedAgentId);
        Assert.Equal(context.Clock.UtcNow.AddHours(24), ticket.DueAtUtc);
        Assert.Equal(ticket.Priority, result.Priority);
        Assert.Equal(ticket.AssignedAgentId, result.AssignedAgentId);
        Assert.Equal(ticket.DueAtUtc, result.DueAtUtc);

        var history = Assert.IsType<TicketEscalation>(context.AddedEscalation);
        Assert.Equal(ticket.Id, history.TicketId);
        Assert.Equal(TicketPriority.Low, history.FromPriority);
        Assert.Equal(TicketPriority.Medium, history.ToPriority);
        Assert.Equal(2, history.FromAgentId);
        Assert.Equal(2, history.ToAgentId);
        Assert.Equal(previousDueAtUtc, history.FromDueAtUtc);
        Assert.Equal(context.Clock.UtcNow.AddHours(24), history.ToDueAtUtc);
        Assert.Equal("Customer impact increased", history.Reason);
        Assert.Equal("shift-lead", history.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, history.EscalatedAtUtc);
        Assert.Equal(history.ToPriority, result.Escalation.ToPriority);
        context.Escalations.Verify(repository => repository.AddAsync(
            It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()), Times.Once);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentIsIneligible_PersistsReplacement()
    {
        var context = ContextWithTicket(TicketPriority.Medium, assignedAgentId: 2);
        var ticket = await ExistingTicket(context);
        var inactive = Agent(context, 2, maxOpenTickets: 5);
        inactive.Deactivate();
        var replacement = Agent(context, 4, maxOpenTickets: 5);
        context.AssignmentCandidates =
        [
            new AgentAssignmentCandidate(inactive, [ticket]),
            new AgentAssignmentCandidate(replacement, [])
        ];

        var result = await context.EscalateTicket.HandleAsync(Command(ticket.Id), CancellationToken.None);

        Assert.Equal(replacement.Id, result.AssignedAgentId);
        Assert.Equal(2, result.Escalation.FromAgentId);
        Assert.Equal(replacement.Id, result.Escalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsEligible_PersistsNullAssignment()
    {
        var context = ContextWithTicket(TicketPriority.Medium, assignedAgentId: 2);
        var ticket = await ExistingTicket(context);
        var full = Agent(context, 2, maxOpenTickets: 1);
        context.AssignmentCandidates = [new AgentAssignmentCandidate(full, [ticket])];

        var result = await context.EscalateTicket.HandleAsync(Command(ticket.Id), CancellationToken.None);

        Assert.Null(result.AssignedAgentId);
        Assert.Null(result.Escalation.ToAgentId);
        Assert.False(string.IsNullOrWhiteSpace(result.AssignmentReason));
        context.VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_ForCompletedTicket_DoesNotStageOrSave(TicketStatus status)
    {
        var context = ContextWithTicket(TicketPriority.Low, status: status);
        var ticket = await ExistingTicket(context);
        var originalPriority = ticket.Priority;

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            context.EscalateTicket.HandleAsync(Command(ticket.Id), CancellationToken.None));

        Assert.Equal(originalPriority, ticket.Priority);
        VerifyNothingPersisted(context);
    }

    [Fact]
    public async Task HandleAsync_ForCriticalTicket_DoesNotStageOrSave()
    {
        var context = ContextWithTicket(TicketPriority.Critical);
        var ticket = await ExistingTicket(context);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            context.EscalateTicket.HandleAsync(Command(ticket.Id), CancellationToken.None));

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        VerifyNothingPersisted(context);
    }

    [Theory]
    [InlineData("no", "shift-lead")]
    [InlineData("Customer impact increased", "")]
    public async Task HandleAsync_WithInvalidInput_DoesNotStageOrSave(string reason, string actor)
    {
        var context = ContextWithTicket(TicketPriority.Low);
        var ticket = await ExistingTicket(context);

        await Assert.ThrowsAsync<ArgumentException>(() => context.EscalateTicket.HandleAsync(
            new EscalateTicketCommand(ticket.Id, reason, actor), CancellationToken.None));

        Assert.Equal(TicketPriority.Low, ticket.Priority);
        VerifyNothingPersisted(context);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsWithoutLoadingDependenciesOrSaving()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(repository => repository.GetByIdAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.EscalateTicket.HandleAsync(Command(404), CancellationToken.None));

        context.Customers.Verify(
            repository => repository.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Agents.Verify(
            repository => repository.GetAssignmentCandidatesAsync(It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingPersisted(context);
    }

    [Fact]
    public async Task HandleAsync_WhenAtomicCommitFails_PropagatesAndDoesNotAttemptAnotherSave()
    {
        var context = ContextWithTicket(TicketPriority.Low);
        var ticket = await ExistingTicket(context);
        context.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated database failure"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.EscalateTicket.HandleAsync(Command(ticket.Id), CancellationToken.None));

        Assert.Contains("database failure", exception.Message);
        Assert.NotNull(context.AddedEscalation);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenDatabaseSaveFails_CommitsNeitherTicketNorHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var failure = new FailingSaveInterceptor();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(failure)
            .Options;
        int ticketId;

        await using (var setup = new SupportDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var customer = new Customer(
                "Contoso", "support@contoso.example", null, CustomerTier.Standard, FixedClock.DefaultNow);
            var category = new Category("General", false, false);
            setup.AddRange(customer, category);
            await setup.SaveChangesAsync();
            var ticket = Ticket.Raise(
                "TCK-0042", "Service unavailable", "Unavailable for several users.",
                customer.Id, category.Id, TicketPriority.Low, FixedClock.DefaultNow,
                FixedClock.DefaultNow.AddHours(72));
            setup.Add(ticket);
            await setup.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        failure.Enabled = true;
        await using (var db = new SupportDbContext(options))
        {
            var handler = new EscalateTicketCommandHandler(
                new TicketRepository(db),
                new CustomerRepository(db),
                new CategoryRepository(db),
                new AgentRepository(db),
                new TicketEscalationRepository(db),
                Service(),
                db,
                new FixedClock());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(Command(ticketId), CancellationToken.None));
        }

        await using (var verification = new SupportDbContext(options))
        {
            var persistedTicket = await verification.Set<Ticket>().AsNoTracking().SingleAsync();
            Assert.Equal(TicketPriority.Low, persistedTicket.Priority);
            Assert.Equal(FixedClock.DefaultNow.AddHours(72), persistedTicket.DueAtUtc);
            Assert.Empty(await verification.Set<TicketEscalation>().AsNoTracking().ToListAsync());
        }
    }

    private static TicketCommandTestContext ContextWithTicket(
        TicketPriority priority,
        int? assignedAgentId = null,
        TicketStatus status = TicketStatus.New)
    {
        var context = new TicketCommandTestContext();
        var ticket = Ticket.Raise(
            "TCK-0042",
            "Service unavailable",
            "The service is unavailable for several users.",
            context.Customer.Id,
            context.Category.Id,
            priority,
            context.Clock.UtcNow,
            context.Clock.UtcNow.AddHours(72),
            assignedAgentId);
        TicketCommandTestContext.WithId(ticket, 42);
        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, context.Clock.UtcNow);
        }

        context.ExistingTicket(ticket);
        return context;
    }

    private static async Task<Ticket> ExistingTicket(TicketCommandTestContext context) =>
        Assert.IsType<Ticket>(await context.Tickets.Object.GetByIdAsync(42, CancellationToken.None));

    private static Agent Agent(TicketCommandTestContext context, int id, int maxOpenTickets) =>
        TicketCommandTestContext.WithId(
            new Agent($"Agent {id}", $"agent{id}@example.com", maxOpenTickets, context.Clock.UtcNow),
            id);

    private static EscalateTicketCommand Command(int ticketId) =>
        new(ticketId, "Customer impact increased", "shift-lead");

    private static TicketEscalationService Service() => new(new SlaDeadlineCalculator(new SlaSettings
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
    }));

    private static void VerifyNothingPersisted(TicketCommandTestContext context)
    {
        context.Escalations.Verify(repository => repository.AddAsync(
            It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()), Times.Never);
        context.VerifySaved(Times.Never());
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            Enabled
                ? ValueTask.FromException<InterceptionResult<int>>(
                    new InvalidOperationException("Simulated database failure"))
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
