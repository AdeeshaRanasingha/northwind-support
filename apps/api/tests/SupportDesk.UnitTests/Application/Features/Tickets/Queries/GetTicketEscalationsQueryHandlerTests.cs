using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Queries;

public sealed class GetTicketEscalationsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForExistingTicketWithoutHistory_ReturnsEmptyArray()
    {
        var tickets = new Mock<ITicketRepository>();
        var escalations = new Mock<ITicketEscalationRepository>();
        tickets.Setup(repository => repository.ExistsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        escalations.Setup(repository => repository.GetByTicketIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await new GetTicketEscalationsQueryHandler(tickets.Object, escalations.Object)
            .HandleAsync(42, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_MapsCompleteAuditValuesIncludingNulls()
    {
        var tickets = new Mock<ITicketRepository>();
        var escalations = new Mock<ITicketEscalationRepository>();
        tickets.Setup(repository => repository.ExistsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        escalations.Setup(repository => repository.GetByTicketIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketEscalation(
                42, TicketPriority.Low, TicketPriority.Medium, null, null, null, null,
                "Customer impact increased", "shift-lead", FixedClock.DefaultNow)]);

        var item = Assert.Single(await new GetTicketEscalationsQueryHandler(
            tickets.Object, escalations.Object).HandleAsync(42, CancellationToken.None));

        Assert.Equal(TicketPriority.Low, item.FromPriority);
        Assert.Equal(TicketPriority.Medium, item.ToPriority);
        Assert.Null(item.FromAgentId);
        Assert.Null(item.ToAgentId);
        Assert.Null(item.FromDueAtUtc);
        Assert.Null(item.ToDueAtUtc);
        Assert.Equal("Customer impact increased", item.Reason);
        Assert.Equal("shift-lead", item.EscalatedBy);
        Assert.Equal(FixedClock.DefaultNow, item.EscalatedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ForUnknownTicket_ThrowsWithoutQueryingHistory()
    {
        var tickets = new Mock<ITicketRepository>();
        var escalations = new Mock<ITicketEscalationRepository>();
        tickets.Setup(repository => repository.ExistsAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() => new GetTicketEscalationsQueryHandler(
            tickets.Object, escalations.Object).HandleAsync(404, CancellationToken.None));

        escalations.Verify(repository => repository.GetByTicketIdAsync(
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
