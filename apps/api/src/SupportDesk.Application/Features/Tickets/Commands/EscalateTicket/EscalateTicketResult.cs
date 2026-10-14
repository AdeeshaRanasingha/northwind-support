using SupportDesk.Application.Contracts.Tickets;
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
    TicketEscalationDto Escalation,
    string AssignmentReason);
