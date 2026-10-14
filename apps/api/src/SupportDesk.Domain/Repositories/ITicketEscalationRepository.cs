using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

/// <summary>Stages immutable ticket escalation audit records for the unit of work.</summary>
public interface ITicketEscalationRepository
{
    Task AddAsync(TicketEscalation escalation, CancellationToken ct);
}
