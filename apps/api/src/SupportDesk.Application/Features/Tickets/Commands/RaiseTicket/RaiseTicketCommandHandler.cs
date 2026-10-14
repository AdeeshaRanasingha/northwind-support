using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    SlaDeadlineCalculator slaDeadlineCalculator,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);
        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);
        var nowUtc = clock.UtcNow;
        var priority = TicketPriorityCalculator.Calculate(request.RequestedPriority, category);
        var dueAtUtc = slaDeadlineCalculator.Calculate(nowUtc, priority, customer.Tier);
        var assignment = AgentAssignmentSelector.Select(
            category,
            await agents.GetAssignmentCandidatesAsync(ct));

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            customer.Id,
            category.Id,
            priority,
            nowUtc,
            dueAtUtc,
            assignment.Agent?.Id);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        var created = await getTicket.HandleAsync(ticket.Id, ct);
        created.Triage = new TicketTriageDto(
            PriorityReason(request.RequestedPriority, category.ForcesCriticalPriority, priority),
            $"{customer.Tier} customer SLA is {(dueAtUtc - nowUtc).TotalHours:g} hours from creation.",
            assignment.Agent is null
                ? assignment.UnassignedReason!
                : $"Assigned to {assignment.Agent.FullName}, the eligible agent with the lowest open workload.");

        return created;
    }

    private static string PriorityReason(
        TicketPriority? requestedPriority,
        bool forcesCriticalPriority,
        TicketPriority calculatedPriority) =>
        forcesCriticalPriority
            ? "The selected category requires Critical priority."
            : requestedPriority is null
                ? "No priority was requested, so Medium priority was applied."
                : $"The requested {calculatedPriority} priority was applied.";
}
