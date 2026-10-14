using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(
        int ticketId,
        CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(ticketId, ct))
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        var history = await escalations.GetByTicketIdAsync(ticketId, ct);
        return history.Select(escalation => new TicketEscalationDto(
            escalation.Id,
            escalation.TicketId,
            escalation.FromPriority,
            escalation.ToPriority,
            escalation.FromAgentId,
            escalation.ToAgentId,
            escalation.FromDueAtUtc,
            escalation.ToDueAtUtc,
            escalation.Reason,
            escalation.EscalatedBy,
            escalation.EscalatedAtUtc)).ToArray();
    }
}
