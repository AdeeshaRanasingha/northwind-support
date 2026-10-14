using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>Escalates a ticket and stages its audit history in one unit of work.</summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    ITicketEscalationRepository escalations,
    TicketEscalationService escalationService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<EscalateTicketResult> HandleAsync(
        EscalateTicketCommand command,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(command.TicketId, ct)
            ?? throw new NotFoundException("Ticket", command.TicketId);
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);
        var candidates = await agents.GetAssignmentCandidatesAsync(ct);

        var decision = escalationService.Escalate(
            ticket,
            category,
            customer.Tier,
            candidates,
            command.Reason,
            command.EscalatedBy,
            clock.UtcNow);
        var escalation = new TicketEscalation(
            ticket.Id,
            decision.FromPriority,
            decision.ToPriority,
            decision.FromAgentId,
            decision.ToAgentId,
            decision.FromDueAtUtc,
            decision.ToDueAtUtc,
            decision.Reason,
            decision.EscalatedBy,
            decision.EscalatedAtUtc);

        await escalations.AddAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new EscalateTicketResult(
            ticket.Id,
            ticket.Reference,
            ticket.Status,
            ticket.Priority,
            ticket.AssignedAgentId,
            ticket.DueAtUtc!.Value,
            ticket.UpdatedAtUtc,
            new TicketEscalationDto(
                escalation.Id,
                escalation.TicketId,
                escalation.FromPriority,
                escalation.ToPriority,
                escalation.FromAgentId,
                escalation.ToAgentId,
                escalation.FromDueAtUtc,
                escalation.ToDueAtUtc,
                escalation.Reason,
                escalation.EscalatedBy,
                escalation.EscalatedAtUtc),
            decision.AssignmentReason);
    }
}
