using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class TicketEscalationServiceTests
{
    private static readonly DateTime EscalatedAtUtc = FixedClock.DefaultNow.AddDays(2);

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_IncreasesPriorityExactlyOneLevel(
        TicketPriority fromPriority,
        TicketPriority expectedPriority)
    {
        var ticket = CreateTicket(fromPriority);

        var decision = Service().Escalate(
            ticket, Category(), CustomerTier.Standard, [], "Customer impact increased", "operator", EscalatedAtUtc);

        Assert.Equal(fromPriority, decision.FromPriority);
        Assert.Equal(expectedPriority, decision.ToPriority);
        Assert.Equal(expectedPriority, ticket.Priority);
    }

    [Fact]
    public void Escalate_CriticalTicket_RejectsWithoutMutation()
    {
        var ticket = CreateTicket(TicketPriority.Critical, assignedAgentId: 7);
        var originalDueAtUtc = ticket.DueAtUtc;

        Assert.Throws<BusinessRuleViolationException>(() => Service().Escalate(
            ticket, Category(), CustomerTier.Standard, [], "Customer impact increased", "operator", EscalatedAtUtc));

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(7, ticket.AssignedAgentId);
        Assert.Equal(originalDueAtUtc, ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_CompletedTicket_RejectsWithoutMutation(TicketStatus status)
    {
        var ticket = CreateTicket(TicketPriority.Low);
        ticket.ChangeStatus(status, FixedClock.DefaultNow.AddHours(1));
        var originalUpdatedAtUtc = ticket.UpdatedAtUtc;

        Assert.Throws<BusinessRuleViolationException>(() => Service().Escalate(
            ticket, Category(), CustomerTier.Standard, [], "Customer impact increased", "operator", EscalatedAtUtc));

        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Equal(status, ticket.Status);
        Assert.Equal(originalUpdatedAtUtc, ticket.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("four")]
    public void Escalate_WithMissingOrTooShortTrimmedReason_Rejects(string reason)
    {
        Assert.Throws<ArgumentException>(() => Service().Escalate(
            CreateTicket(TicketPriority.Low), Category(), CustomerTier.Standard, [], reason, "operator", EscalatedAtUtc));
    }

    [Fact]
    public void Escalate_WithTooLongReason_Rejects()
    {
        Assert.Throws<ArgumentException>(() => Service().Escalate(
            CreateTicket(TicketPriority.Low), Category(), CustomerTier.Standard, [],
            new string('x', TicketEscalation.MaxReasonLength + 1), "operator", EscalatedAtUtc));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Escalate_WithMissingActor_Rejects(string actor)
    {
        Assert.Throws<ArgumentException>(() => Service().Escalate(
            CreateTicket(TicketPriority.Low), Category(), CustomerTier.Standard, [], "Valid reason", actor, EscalatedAtUtc));
    }

    [Fact]
    public void Escalate_WithTooLongActor_Rejects()
    {
        Assert.Throws<ArgumentException>(() => Service().Escalate(
            CreateTicket(TicketPriority.Low), Category(), CustomerTier.Standard, [], "Valid reason",
            new string('x', TicketEscalation.MaxEscalatedByLength + 1), EscalatedAtUtc));
    }

    [Fact]
    public void Escalate_CalculatesDeadlineFromEscalationTimeAndRecordsBeforeAfterValues()
    {
        var ticket = CreateTicket(TicketPriority.Low, assignedAgentId: 4);
        var previousDueAtUtc = ticket.DueAtUtc;

        var decision = Service().Escalate(
            ticket, Category(), CustomerTier.Standard, [], "  Impact has increased  ", "  supervisor  ", EscalatedAtUtc);

        Assert.Equal(EscalatedAtUtc.AddHours(24), decision.ToDueAtUtc);
        Assert.Equal(previousDueAtUtc, decision.FromDueAtUtc);
        Assert.Equal(4, decision.FromAgentId);
        Assert.Null(decision.ToAgentId);
        Assert.Equal("Impact has increased", decision.Reason);
        Assert.Equal("supervisor", decision.EscalatedBy);
        Assert.Equal(EscalatedAtUtc, decision.EscalatedAtUtc);
        Assert.Equal(decision.ToDueAtUtc, ticket.DueAtUtc);
        Assert.Equal(EscalatedAtUtc, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_ForPremiumCustomer_AppliesPremiumSlaWindow()
    {
        var decision = Service().Escalate(
            CreateTicket(TicketPriority.High), Category(), CustomerTier.Premium, [],
            "Customer impact increased", "operator", EscalatedAtUtc);

        Assert.Equal(TicketPriority.Critical, decision.ToPriority);
        Assert.Equal(EscalatedAtUtc.AddHours(2), decision.ToDueAtUtc);
    }

    [Fact]
    public void Escalate_WhenCurrentAgentRemainsEligible_RetainsAgent()
    {
        var agent = Agent(3, maxOpenTickets: 3);
        var ticket = CreateTicket(TicketPriority.Low, assignedAgentId: agent.Id);

        var decision = Service().Escalate(
            ticket, Category(), CustomerTier.Standard, [Candidate(agent, ticket)],
            "Customer impact increased", "operator", EscalatedAtUtc);

        Assert.Equal(agent.Id, decision.ToAgentId);
        Assert.Equal(agent.Id, ticket.AssignedAgentId);
        Assert.Contains("retained", decision.AssignmentReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Escalate_WhenCurrentAgentIsIneligible_SelectsEligibleReplacement()
    {
        var current = Agent(3, maxOpenTickets: 2);
        current.Deactivate();
        var replacement = Agent(5, maxOpenTickets: 2);
        var ticket = CreateTicket(TicketPriority.Low, assignedAgentId: current.Id);

        var decision = Service().Escalate(
            ticket, Category(), CustomerTier.Standard,
            [Candidate(current, ticket), Candidate(replacement)],
            "Customer impact increased", "operator", EscalatedAtUtc);

        Assert.Equal(current.Id, decision.FromAgentId);
        Assert.Equal(replacement.Id, decision.ToAgentId);
        Assert.Equal(replacement.Id, ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_ForSpecialistCategory_ReplacesWithMatchingSpecialist()
    {
        var category = Category(requiresSpecialist: true);
        var currentGeneralist = Agent(3, maxOpenTickets: 2);
        var specialist = Agent(5, maxOpenTickets: 2);
        specialist.AddSpecialization(category.Id);
        var ticket = CreateTicket(TicketPriority.Low, assignedAgentId: currentGeneralist.Id);

        var decision = Service().Escalate(
            ticket, category, CustomerTier.Standard,
            [Candidate(currentGeneralist, ticket), Candidate(specialist)],
            "Customer impact increased", "operator", EscalatedAtUtc);

        Assert.Equal(specialist.Id, decision.ToAgentId);
    }

    [Fact]
    public void Escalate_WhenNoAgentIsEligible_LeavesTicketUnassignedWithReason()
    {
        var fullAgent = Agent(3, maxOpenTickets: 1);
        var ticket = CreateTicket(TicketPriority.Low, assignedAgentId: fullAgent.Id);

        var decision = Service().Escalate(
            ticket, Category(), CustomerTier.Standard, [Candidate(fullAgent, ticket)],
            "Customer impact increased", "operator", EscalatedAtUtc);

        Assert.Null(decision.ToAgentId);
        Assert.Null(ticket.AssignedAgentId);
        Assert.False(string.IsNullOrWhiteSpace(decision.AssignmentReason));
    }

    private static TicketEscalationService Service() => new(new SlaDeadlineCalculator(new SlaSettings
    {
        BaseWindowHours = new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    }));

    private static Ticket CreateTicket(TicketPriority priority, int? assignedAgentId = null) => Ticket.Raise(
        "TCK-0099",
        "Service unavailable",
        "The service is unavailable for several users.",
        customerId: 1,
        categoryId: 10,
        priority,
        FixedClock.DefaultNow,
        FixedClock.DefaultNow.AddHours(72),
        assignedAgentId);

    private static Agent Agent(int id, int maxOpenTickets)
    {
        var agent = new Agent($"Agent {id}", $"agent{id}@example.com", maxOpenTickets, FixedClock.DefaultNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, id);
        return agent;
    }

    private static AgentAssignmentCandidate Candidate(Agent agent, params Ticket[] tickets) =>
        new(agent, tickets);

    private static Category Category(bool requiresSpecialist = false)
    {
        var category = new Category("Technical", requiresSpecialist, forcesCriticalPriority: false);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, 10);
        return category;
    }
}
