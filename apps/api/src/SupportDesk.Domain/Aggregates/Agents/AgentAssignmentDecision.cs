namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>The selected agent, or the normal unassigned outcome and its explanation.</summary>
public sealed record AgentAssignmentDecision(Agent? Agent, string? UnassignedReason)
{
    public static AgentAssignmentDecision AssignedTo(Agent agent) => new(agent, null);

    public static AgentAssignmentDecision Unassigned(string reason) => new(null, reason);
}
