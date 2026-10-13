using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>Selects the least-loaded eligible agent for a category.</summary>
public static class AgentAssignmentSelector
{
    public static AgentAssignmentDecision Select(
        Category category,
        IEnumerable<AgentAssignmentCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(candidates);

        var selected = candidates
            .Where(candidate => candidate.Agent.IsActive)
            .Where(candidate =>
                !category.RequiresSpecialist
                || candidate.Agent.Specializations.Any(
                    specialization => specialization.CategoryId == category.Id))
            .Where(candidate => candidate.OpenTicketCount < candidate.Agent.MaxOpenTickets)
            .OrderBy(candidate => candidate.OpenTicketCount)
            .ThenBy(candidate => candidate.Agent.Id)
            .ThenBy(candidate => candidate.Agent.FullName, StringComparer.Ordinal)
            .FirstOrDefault();

        return selected is null
            ? AgentAssignmentDecision.Unassigned(
                "No active agent with the required specialization and available capacity was found.")
            : AgentAssignmentDecision.AssignedTo(selected.Agent);
    }
}
