using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<AgentAssignmentCandidate>> GetAssignmentCandidatesAsync(
        CancellationToken ct)
    {
        var agents = await db.Set<Agent>()
            .AsNoTracking()
            .Include(agent => agent.Specializations)
            .ToListAsync(ct);
        var agentIds = agents.Select(agent => agent.Id).ToArray();
        var assignedTickets = await db.Set<Ticket>()
            .AsNoTracking()
            .Where(ticket =>
                ticket.AssignedAgentId != null
                && agentIds.Contains(ticket.AssignedAgentId.Value))
            .ToListAsync(ct);
        var ticketsByAgent = assignedTickets.ToLookup(ticket => ticket.AssignedAgentId!.Value);

        return agents
            .Select(agent => new AgentAssignmentCandidate(agent, ticketsByAgent[agent.Id].ToArray()))
            .ToArray();
    }
}
