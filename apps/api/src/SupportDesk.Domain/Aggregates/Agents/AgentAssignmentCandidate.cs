using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// An agent together with the tickets currently assigned to them. Open workload is derived from
/// ticket status so assignment never depends on a cached count.
/// </summary>
public sealed record AgentAssignmentCandidate(
    Agent Agent,
    IReadOnlyCollection<Ticket> AssignedTickets)
{
    public int OpenTicketCount => AssignedTickets.Count(ticket => ticket.IsOpen);
}
