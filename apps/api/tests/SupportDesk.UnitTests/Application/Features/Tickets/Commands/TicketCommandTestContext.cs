using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Shared arrangement for the ticket command tests: mocked repositories and queries, a fixed
/// clock, and command handlers wired to all of them.
/// </summary>
internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        Customer = WithId(
            new Customer(
                "Contoso Ltd",
                "support@contoso.example",
                null,
                CustomerTier.Standard,
                Clock.UtcNow),
            1);
        Category = WithId(
            new Category("General", requiresSpecialist: false, forcesCriticalPriority: false),
            3);

        Customers.Setup(c => c.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Customer);
        Categories.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Category);
        Agents.Setup(a => a.GetAssignmentCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => AssignmentCandidates);

        Tickets.Setup(t => t.NextReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        // Every command returns the ticket as re-read once its change has been saved.
        TicketQueries.Setup(q => q.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreatedDetail());

        var getTicket = new GetTicketQueryHandler(TicketQueries.Object);

        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, Agents.Object,
            new SlaDeadlineCalculator(SlaSettings()), UnitOfWork.Object, Clock, getTicket,
            NullLogger<RaiseTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(Tickets.Object, UnitOfWork.Object, Clock, getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object, Agents.Object, UnitOfWork.Object, Clock, getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public FixedClock Clock { get; } = new();

    public Customer Customer { get; set; }

    public Category Category { get; set; }

    public IReadOnlyList<AgentAssignmentCandidate> AssignmentCandidates { get; set; } = [];

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    /// <summary>The ticket handed to the repository by the last create call.</summary>
    public Ticket? AddedTicket { get; private set; }

    /// <summary>Makes <see cref="ITicketRepository.GetByIdAsync"/> return this ticket.</summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void ExistingAgent(int id, string fullName = "Alex Turner", bool isActive = true)
    {
        var agent = new Agent(fullName, $"agent{id}@example.com", maxOpenTickets: 10, Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents.Setup(a => a.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);

    public static T WithId<T>(T entity, int id) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
        return entity;
    }

    private TicketDetailDto CreatedDetail()
    {
        if (AddedTicket is null)
        {
            return new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(1, "Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard),
                Category = new CategoryDto(3, "General", false)
            };
        }

        var assignedAgent = AssignmentCandidates
            .Select(candidate => candidate.Agent)
            .FirstOrDefault(agent => agent.Id == AddedTicket.AssignedAgentId);

        return new TicketDetailDto
        {
            Id = 41,
            Reference = AddedTicket.Reference,
            Title = AddedTicket.Title,
            Description = AddedTicket.Description,
            Status = AddedTicket.Status,
            Priority = AddedTicket.Priority,
            Customer = new CustomerContactDto(
                Customer.Id, Customer.Name, Customer.Email, Customer.Phone, Customer.Tier),
            Category = new CategoryDto(
                Category.Id, Category.Name, Category.RequiresSpecialist),
            AssignedAgent = assignedAgent is null
                ? null
                : new AgentSummaryDto(assignedAgent.Id, assignedAgent.FullName),
            CreatedAtUtc = AddedTicket.CreatedAtUtc,
            UpdatedAtUtc = AddedTicket.UpdatedAtUtc,
            DueAtUtc = AddedTicket.DueAtUtc,
            SlaStatus = SlaEvaluator.Evaluate(AddedTicket.DueAtUtc, null, Clock.UtcNow)
        };
    }

    private static SlaSettings SlaSettings() => new()
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
    };
}
