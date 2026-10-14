using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Presentation.Controllers;
using SupportDesk.UnitTests.Application.Features.Tickets.Commands;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation.Controllers;

public sealed class TicketsControllerTests
{
    [Fact]
    public async Task Create_WhenNoAgentIsEligible_Returns201WithUnassignedTriage()
    {
        var context = new TicketCommandTestContext();
        var inactive = TicketCommandTestContext.WithId(
            new Agent(
                "Alex Turner",
                "alex@example.com",
                maxOpenTickets: 5,
                FixedClock.DefaultNow),
            1);
        inactive.Deactivate();
        context.AssignmentCandidates = [new AgentAssignmentCandidate(inactive, [])];
        var controller = new TicketsController();
        var request = new CreateTicketRequest(
            "Cannot log in",
            "Happens for every user since this morning.",
            CustomerId: 1,
            CategoryId: 3,
            RequestedPriority: TicketPriority.High);

        var result = await controller.Create(request, context.RaiseTicket, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<TicketDetailDto>(created.Value);
        Assert.Null(response.AssignedAgent);
        Assert.Contains("No active agent", response.Triage!.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Escalate_OnSuccess_Returns200WithUpdatedTicketAndAuditRecord()
    {
        var context = new TicketCommandTestContext();
        var ticket = ExistingTicket(context, TicketPriority.Low);
        var controller = new TicketsController();

        var result = await controller.Escalate(
            ticket.Id,
            new EscalateTicketRequest("Customer impact increased", "shift-lead"),
            context.EscalateTicket,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        var response = Assert.IsType<EscalateTicketResult>(ok.Value);
        Assert.Equal(ticket.Id, response.TicketId);
        Assert.Equal(TicketPriority.Medium, response.Priority);
        Assert.Equal(TicketPriority.Low, response.Escalation.FromPriority);
        Assert.Equal(TicketPriority.Medium, response.Escalation.ToPriority);
        Assert.Equal("Customer impact increased", response.Escalation.Reason);
        Assert.Equal("shift-lead", response.Escalation.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, response.Escalation.EscalatedAtUtc);
        Assert.Equal(response.DueAtUtc, response.Escalation.ToDueAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Escalate_WhenTicketIsUnknown_PropagatesNotFoundWithoutPersistence()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(repository => repository.GetByIdAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => new TicketsController().Escalate(
            404,
            new EscalateTicketRequest("Customer impact increased", "shift-lead"),
            context.EscalateTicket,
            CancellationToken.None));

        Assert.Contains("Ticket", exception.Message);
        VerifyNoEscalationPersisted(context);
    }

    [Fact]
    public async Task Escalate_WhenTicketIsCritical_PropagatesConflictReasonWithoutPersistence()
    {
        var context = new TicketCommandTestContext();
        var ticket = ExistingTicket(context, TicketPriority.Critical);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            new TicketsController().Escalate(
                ticket.Id,
                new EscalateTicketRequest("Customer impact increased", "shift-lead"),
                context.EscalateTicket,
                CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        VerifyNoEscalationPersisted(context);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task Escalate_WhenTicketIsCompleted_PropagatesRelevantConflictWithoutPersistence(
        TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = ExistingTicket(context, TicketPriority.Low);
        ticket.ChangeStatus(status, context.Clock.UtcNow);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            new TicketsController().Escalate(
                ticket.Id,
                new EscalateTicketRequest("Customer impact increased", "shift-lead"),
                context.EscalateTicket,
                CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        VerifyNoEscalationPersisted(context);
    }

    [Fact]
    public void Escalate_DeclaresExpectedPostRoute()
    {
        var action = typeof(TicketsController).GetMethod(nameof(TicketsController.Escalate));
        var route = Assert.Single(action!.GetCustomAttributes(typeof(HttpPostAttribute), inherit: false));

        Assert.Equal("{id:int}/escalate", Assert.IsType<HttpPostAttribute>(route).Template);
    }

    [Fact]
    public async Task GetEscalations_ForExistingTicket_Returns200WithHistory()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(repository => repository.ExistsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        context.Escalations.Setup(repository => repository.GetByTicketIdAsync(
                42, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketEscalation(
                42, TicketPriority.Low, TicketPriority.Medium, null, null, null,
                FixedClock.DefaultNow.AddHours(24), "Customer impact increased", "shift-lead",
                FixedClock.DefaultNow)]);
        var handler = new GetTicketEscalationsQueryHandler(
            context.Tickets.Object, context.Escalations.Object);

        var result = await new TicketsController().GetEscalations(
            42, handler, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TicketEscalationDto>>(ok.Value));
    }

    private static Ticket ExistingTicket(TicketCommandTestContext context, TicketPriority priority)
    {
        var ticket = Ticket.Raise(
            "TCK-0042",
            "Service unavailable",
            "The service is unavailable for several users.",
            context.Customer.Id,
            context.Category.Id,
            priority,
            context.Clock.UtcNow,
            context.Clock.UtcNow.AddHours(72));
        TicketCommandTestContext.WithId(ticket, 42);
        return context.ExistingTicket(ticket);
    }

    private static void VerifyNoEscalationPersisted(TicketCommandTestContext context)
    {
        context.Escalations.Verify(repository => repository.AddAsync(
            It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()), Times.Never);
        context.VerifySaved(Times.Never());
    }
}
