using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Validates and applies the business rules for one ticket escalation.</summary>
public sealed class TicketEscalationService
{
    public const int MinReasonLength = 5;

    private readonly SlaDeadlineCalculator _slaDeadlineCalculator;

    public TicketEscalationService(SlaDeadlineCalculator slaDeadlineCalculator)
    {
        ArgumentNullException.ThrowIfNull(slaDeadlineCalculator);
        _slaDeadlineCalculator = slaDeadlineCalculator;
    }

    public TicketEscalationDecision Escalate(
        Ticket ticket,
        Category category,
        CustomerTier customerTier,
        IEnumerable<AgentAssignmentCandidate> candidates,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(candidates);

        ValidateTicket(ticket);
        var trimmedReason = ValidateReason(reason);
        var trimmedActor = ValidateActor(escalatedBy);

        if (escalatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Escalation time must be UTC.", nameof(escalatedAtUtc));
        }

        var nextPriority = (TicketPriority)((int)ticket.Priority + 1);
        var nextDueAtUtc = _slaDeadlineCalculator.Calculate(
            escalatedAtUtc,
            nextPriority,
            customerTier);
        var candidateList = candidates.ToArray();
        var assignment = SelectAssignment(ticket, category, candidateList);

        var decision = new TicketEscalationDecision(
            ticket.Priority,
            nextPriority,
            ticket.AssignedAgentId,
            assignment.Agent?.Id,
            ticket.DueAtUtc,
            nextDueAtUtc,
            trimmedReason,
            trimmedActor,
            escalatedAtUtc,
            assignment.Reason);

        ticket.ApplyEscalation(decision);
        return decision;
    }

    private static void ValidateTicket(Ticket ticket)
    {
        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException("Critical tickets cannot be escalated further.");
        }

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Tickets with status {ticket.Status} cannot be escalated.");
        }
    }

    private static string ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Escalation reason is required.", nameof(reason));
        }

        var trimmed = reason.Trim();
        if (trimmed.Length < MinReasonLength || trimmed.Length > TicketEscalation.MaxReasonLength)
        {
            throw new ArgumentException(
                $"Escalation reason must contain {MinReasonLength} to {TicketEscalation.MaxReasonLength} characters.",
                nameof(reason));
        }

        return trimmed;
    }

    private static string ValidateActor(string escalatedBy)
    {
        if (string.IsNullOrWhiteSpace(escalatedBy))
        {
            throw new ArgumentException("Escalation actor is required.", nameof(escalatedBy));
        }

        var trimmed = escalatedBy.Trim();
        if (trimmed.Length > TicketEscalation.MaxEscalatedByLength)
        {
            throw new ArgumentException(
                $"Escalation actor cannot exceed {TicketEscalation.MaxEscalatedByLength} characters.",
                nameof(escalatedBy));
        }

        return trimmed;
    }

    private static AssignmentResult SelectAssignment(
        Ticket ticket,
        Category category,
        IReadOnlyCollection<AgentAssignmentCandidate> candidates)
    {
        var current = ticket.AssignedAgentId is int currentAgentId
            ? candidates.FirstOrDefault(candidate => candidate.Agent.Id == currentAgentId)
            : null;

        if (current is not null && AgentAssignmentSelector.IsEligible(category, current))
        {
            return new AssignmentResult(
                current.Agent,
                $"Current agent {current.Agent.FullName} remains eligible and was retained.");
        }

        var selection = AgentAssignmentSelector.Select(category, candidates);
        return selection.Agent is null
            ? new AssignmentResult(null, selection.UnassignedReason!)
            : new AssignmentResult(
                selection.Agent,
                $"Assigned to eligible agent {selection.Agent.FullName} with the lowest open workload.");
    }

    private sealed record AssignmentResult(Agent? Agent, string Reason);
}
