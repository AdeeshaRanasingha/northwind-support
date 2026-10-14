using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Loads every agent and their actual assigned-ticket workload for triage.</summary>
    Task<IReadOnlyList<AgentAssignmentCandidate>> GetAssignmentCandidatesAsync(CancellationToken ct);
}
