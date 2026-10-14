using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>The updated ticket state and its newly-created immutable audit record.</summary>
public sealed record EscalateTicketResult(
    int TicketId,
    string Reference,
    TicketStatus Status,
    TicketPriority Priority,
    int? AssignedAgentId,
    DateTime DueAtUtc,
    DateTime UpdatedAtUtc,
    EscalationRecordResult Escalation,
    string AssignmentReason);

public sealed record EscalationRecordResult(
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    int? FromAgentId,
    int? ToAgentId,
    DateTime? FromDueAtUtc,
    DateTime? ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);
