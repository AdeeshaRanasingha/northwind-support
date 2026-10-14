namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>The supplied actor will be replaced by the authenticated user in Task 3.</summary>
public sealed record EscalateTicketCommand(int TicketId, string Reason, string EscalatedBy);
