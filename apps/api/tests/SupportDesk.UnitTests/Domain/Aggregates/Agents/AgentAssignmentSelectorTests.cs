using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Common;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public sealed class AgentAssignmentSelectorTests
{
    [Fact]
    public void Select_WithActiveAgentBelowCapacity_AssignsAgent()
    {
        var agent = Agent(1, "Alex Turner", maxOpenTickets: 2);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [Candidate(agent, TicketStatus.Open)]);

        Assert.Same(agent, decision.Agent);
        Assert.Null(decision.UnassignedReason);
    }

    [Fact]
    public void Select_ExcludesInactiveAgent()
    {
        var inactive = Agent(1, "Alex Turner", maxOpenTickets: 5);
        inactive.Deactivate();
        var active = Agent(2, "Priya Nair", maxOpenTickets: 5);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [Candidate(inactive), Candidate(active)]);

        Assert.Same(active, decision.Agent);
    }

    [Fact]
    public void Select_ExcludesAgentAtCapacity()
    {
        var full = Agent(1, "Alex Turner", maxOpenTickets: 1);
        var available = Agent(2, "Priya Nair", maxOpenTickets: 2);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [Candidate(full, TicketStatus.InProgress), Candidate(available, TicketStatus.Open)]);

        Assert.Same(available, decision.Agent);
    }

    [Fact]
    public void Select_DoesNotCountResolvedOrClosedTicketsAsOpen()
    {
        var agent = Agent(1, "Alex Turner", maxOpenTickets: 1);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [Candidate(agent, TicketStatus.Resolved, TicketStatus.Closed)]);

        Assert.Same(agent, decision.Agent);
    }

    [Fact]
    public void Select_ForSpecialistCategory_ChoosesOnlyMatchingSpecialist()
    {
        var category = Category(requiresSpecialist: true);
        var generalist = Agent(1, "Alex Turner", maxOpenTickets: 5);
        var specialist = Agent(2, "Priya Nair", maxOpenTickets: 5);
        specialist.AddSpecialization(category.Id);

        var decision = AgentAssignmentSelector.Select(
            category,
            [Candidate(generalist), Candidate(specialist, TicketStatus.Open)]);

        Assert.Same(specialist, decision.Agent);
    }

    [Fact]
    public void Select_ForNonSpecialistCategory_CanChooseAgentWithoutSpecialization()
    {
        var category = Category(requiresSpecialist: false);
        var generalist = Agent(1, "Alex Turner", maxOpenTickets: 5);

        var decision = AgentAssignmentSelector.Select(category, [Candidate(generalist)]);

        Assert.Same(generalist, decision.Agent);
    }

    [Fact]
    public void Select_ChoosesAgentWithFewestOpenTickets()
    {
        var busier = Agent(1, "Alex Turner", maxOpenTickets: 5);
        var quieter = Agent(2, "Priya Nair", maxOpenTickets: 5);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [
                Candidate(busier, TicketStatus.New, TicketStatus.Open),
                Candidate(quieter, TicketStatus.InProgress)
            ]);

        Assert.Same(quieter, decision.Agent);
    }

    [Fact]
    public void Select_WhenWorkloadsTie_ChoosesLowestAgentId()
    {
        var higherId = Agent(8, "Alex Turner", maxOpenTickets: 5);
        var lowerId = Agent(3, "Priya Nair", maxOpenTickets: 5);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [Candidate(higherId), Candidate(lowerId)]);

        Assert.Same(lowerId, decision.Agent);
    }

    [Fact]
    public void Select_WithNoEligibleAgent_ReturnsUnassignedReason()
    {
        var inactive = Agent(1, "Alex Turner", maxOpenTickets: 5);
        inactive.Deactivate();
        var full = Agent(2, "Priya Nair", maxOpenTickets: 1);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [Candidate(inactive), Candidate(full, TicketStatus.Open)]);

        Assert.Null(decision.Agent);
        Assert.False(string.IsNullOrWhiteSpace(decision.UnassignedReason));
    }

    [Fact]
    public void Select_WithDifferentCapacitiesAndWorkloads_UsesLowestEligibleWorkload()
    {
        var fullLowCapacity = Agent(1, "Alex Turner", maxOpenTickets: 1);
        var availableHigherWorkload = Agent(2, "Priya Nair", maxOpenTickets: 5);
        var availableLowerWorkload = Agent(3, "Sara Lindqvist", maxOpenTickets: 2);

        var decision = AgentAssignmentSelector.Select(
            Category(requiresSpecialist: false),
            [
                Candidate(fullLowCapacity, TicketStatus.Open),
                Candidate(availableHigherWorkload, TicketStatus.New, TicketStatus.InProgress),
                Candidate(availableLowerWorkload, TicketStatus.Open)
            ]);

        Assert.Same(availableLowerWorkload, decision.Agent);
    }

    private static AgentAssignmentCandidate Candidate(Agent agent, params TicketStatus[] statuses) =>
        new(agent, statuses.Select((status, index) =>
            new TicketBuilder()
                .WithReference($"TCK-{agent.Id}{index:000}")
                .WithStatus(status)
                .AssignedTo(agent.Id)
                .Build()).ToArray());

    private static Agent Agent(int id, string name, int maxOpenTickets)
    {
        var agent = new Agent(name, $"agent{id}@example.com", maxOpenTickets, FixedClock.DefaultNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, id);
        return agent;
    }

    private static Category Category(bool requiresSpecialist)
    {
        var category = new Category(
            "Technical",
            requiresSpecialist,
            forcesCriticalPriority: false);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, 10);
        return category;
    }
}
