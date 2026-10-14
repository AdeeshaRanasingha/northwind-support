namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>The complete before/after result of a validated ticket escalation.</summary>
public sealed record TicketEscalationDecision(
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    int? FromAgentId,
    int? ToAgentId,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc,
    string AssignmentReason);
