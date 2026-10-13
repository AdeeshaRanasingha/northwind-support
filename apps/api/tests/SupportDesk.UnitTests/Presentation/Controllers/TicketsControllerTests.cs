using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
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
}
