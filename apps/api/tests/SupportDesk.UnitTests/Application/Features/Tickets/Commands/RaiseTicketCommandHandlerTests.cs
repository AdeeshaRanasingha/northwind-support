using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(8), created.DueAtUtc);
        Assert.Null(created.AssignedAgentId);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForPremiumCustomer_UsesShortenedSlaAndReturnsTriage()
    {
        var context = new TicketCommandTestContext
        {
            Customer = TicketCommandTestContext.WithId(
                new Customer(
                    "Contoso Ltd",
                    "support@contoso.example",
                    null,
                    CustomerTier.Premium,
                    FixedClock.DefaultNow),
                1)
        };

        var response = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket!.DueAtUtc);
        Assert.Equal(context.AddedTicket.DueAtUtc, response.DueAtUtc);
        Assert.Equal(SlaStatus.WithinSla, response.SlaStatus);
        Assert.NotNull(response.Triage);
        Assert.False(string.IsNullOrWhiteSpace(response.Triage.PriorityReason));
        Assert.False(string.IsNullOrWhiteSpace(response.Triage.SlaReason));
        Assert.False(string.IsNullOrWhiteSpace(response.Triage.AssignmentReason));
    }

    [Fact]
    public async Task HandleAsync_ForForcedCriticalCategory_PersistsCriticalPriorityAndDeadline()
    {
        var context = new TicketCommandTestContext
        {
            Category = TicketCommandTestContext.WithId(
                new Category("Routine request", false, forcesCriticalPriority: true),
                3)
        };

        var response = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket.DueAtUtc);
        Assert.Equal(context.AddedTicket.Priority, response.Priority);
        Assert.Contains("requires Critical", response.Triage!.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_SelectsEligibleAgentAndPersistsAssignment()
    {
        var context = new TicketCommandTestContext();
        var busier = Agent(1, "Alex Turner");
        var quieter = Agent(2, "Priya Nair");
        context.AssignmentCandidates =
        [
            new AgentAssignmentCandidate(
                busier,
                [new TicketBuilder().AssignedTo(busier.Id).WithStatus(TicketStatus.Open).Build()]),
            new AgentAssignmentCandidate(quieter, [])
        ];

        var response = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(quieter.Id, context.AddedTicket!.AssignedAgentId);
        Assert.Equal(quieter.Id, response.AssignedAgent!.Id);
        Assert.Contains(quieter.FullName, response.Triage!.AssignmentReason);
        context.Agents.Verify(
            repository => repository.GetAssignmentCandidatesAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ForSpecialistCategory_AssignsMatchingSpecialist()
    {
        var context = new TicketCommandTestContext
        {
            Category = TicketCommandTestContext.WithId(
                new Category("Technical", requiresSpecialist: true, forcesCriticalPriority: false),
                3)
        };
        var generalist = Agent(1, "Alex Turner");
        var specialist = Agent(2, "Priya Nair");
        specialist.AddSpecialization(context.Category.Id);
        context.AssignmentCandidates =
        [
            new AgentAssignmentCandidate(generalist, []),
            new AgentAssignmentCandidate(specialist, [])
        ];

        var response = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(specialist.Id, context.AddedTicket!.AssignedAgentId);
        Assert.Equal(specialist.Id, response.AssignedAgent!.Id);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsEligible_CreatesUnassignedTicketWithReason()
    {
        var context = new TicketCommandTestContext();
        var inactive = Agent(1, "Alex Turner");
        inactive.Deactivate();
        context.AssignmentCandidates = [new AgentAssignmentCandidate(inactive, [])];

        var response = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.Null(response.AssignedAgent);
        Assert.Contains("No active agent", response.Triage!.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    private static Agent Agent(int id, string name) =>
        TicketCommandTestContext.WithId(
            new Agent(name, $"agent{id}@example.com", maxOpenTickets: 5, FixedClock.DefaultNow),
            id);
}
