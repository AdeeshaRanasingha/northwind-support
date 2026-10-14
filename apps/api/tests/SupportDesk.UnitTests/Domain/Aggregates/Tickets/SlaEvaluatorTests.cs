using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class SlaEvaluatorTests
{
    private static readonly DateTime StartedAtUtc = FixedClock.DefaultNow;
    private static readonly DateTime DueAtUtc = StartedAtUtc.AddHours(8);

    [Fact]
    public void Evaluate_WithoutDueDate_IsNotApplicable() =>
        Assert.Equal(
            SlaStatus.NotApplicable,
            new SlaEvaluator(Settings()).Evaluate(StartedAtUtc, null, null, StartedAtUtc));

    [Fact]
    public void Evaluate_ResolvedBeforeDeadline_IsMet() =>
        Assert.Equal(SlaStatus.Met, Evaluate(resolvedAtUtc: DueAtUtc.AddTicks(-1)));

    [Fact]
    public void Evaluate_ResolvedExactlyAtDeadline_IsMet() =>
        Assert.Equal(SlaStatus.Met, Evaluate(resolvedAtUtc: DueAtUtc));

    [Fact]
    public void Evaluate_ResolvedAfterDeadline_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Evaluate(resolvedAtUtc: DueAtUtc.AddTicks(1)));

    [Fact]
    public void Evaluate_UnresolvedAfterDeadline_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Evaluate(nowUtc: DueAtUtc.AddTicks(1)));

    [Fact]
    public void Evaluate_ExactlyAtRiskThreshold_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Evaluate(nowUtc: StartedAtUtc.AddHours(6)));

    [Fact]
    public void Evaluate_JustOutsideAtRiskThreshold_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, Evaluate(nowUtc: StartedAtUtc.AddHours(6).AddTicks(-1)));

    [Fact]
    public void Evaluate_UnresolvedExactlyAtDeadline_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Evaluate(nowUtc: DueAtUtc));

    [Fact]
    public void Evaluate_WithDifferentConfiguredThreshold_ChangesResult()
    {
        var nowUtc = StartedAtUtc.AddHours(4.8);

        Assert.Equal(SlaStatus.WithinSla, Evaluate(nowUtc: nowUtc, thresholdPercent: 25));
        Assert.Equal(SlaStatus.AtRisk, Evaluate(nowUtc: nowUtc, thresholdPercent: 50));
    }

    [Fact]
    public void Evaluate_AfterEscalation_UsesEscalationTimeAsWindowStart()
    {
        var ticket = Ticket.Raise(
            "TCK-0042", "Service unavailable", "Unavailable for several users.",
            1, 1, TicketPriority.Medium, StartedAtUtc, StartedAtUtc.AddHours(24));
        var escalationTimeUtc = StartedAtUtc.AddHours(20);
        var service = new TicketEscalationService(new SlaDeadlineCalculator(Settings()));
        var decision = service.Escalate(
            ticket,
            new Category("General", false, false),
            CustomerTier.Standard,
            [],
            "Customer impact increased",
            "shift-lead",
            escalationTimeUtc);

        var status = new SlaEvaluator(Settings()).Evaluate(
            decision.EscalatedAtUtc,
            ticket.DueAtUtc,
            ticket.ResolvedAtUtc,
            escalationTimeUtc.AddHours(6));

        Assert.Equal(escalationTimeUtc.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Evaluate_WhenInjectedClockAdvances_ChangesStatusWithoutPersistedState()
    {
        var clock = new FixedClock(StartedAtUtc.AddHours(5));
        var evaluator = new SlaEvaluator(Settings());

        Assert.Equal(
            SlaStatus.WithinSla,
            evaluator.Evaluate(StartedAtUtc, DueAtUtc, null, clock.UtcNow));

        clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(
            SlaStatus.AtRisk,
            evaluator.Evaluate(StartedAtUtc, DueAtUtc, null, clock.UtcNow));
    }

    [Theory]
    [InlineData(-1, SlaStatus.Met)]
    [InlineData(1, SlaStatus.Breached)]
    public void Evaluate_ClosedTicket_UsesItsRecordedResolutionTime(
        int minutesFromDeadline,
        SlaStatus expected)
    {
        var ticket = Ticket.Raise(
            "TCK-0043", "Service unavailable", "Unavailable for several users.",
            1, 1, TicketPriority.High, StartedAtUtc, DueAtUtc);
        ticket.ChangeStatus(TicketStatus.Closed, DueAtUtc.AddMinutes(minutesFromDeadline));

        var status = new SlaEvaluator(Settings()).Evaluate(
            ticket.CreatedAtUtc,
            ticket.DueAtUtc,
            ticket.ResolvedAtUtc,
            DueAtUtc.AddDays(1));

        Assert.Equal(expected, status);
    }

    private static SlaStatus Evaluate(
        DateTime? resolvedAtUtc = null,
        DateTime? nowUtc = null,
        double thresholdPercent = 25) =>
        new SlaEvaluator(Settings(thresholdPercent)).Evaluate(
            StartedAtUtc,
            DueAtUtc,
            resolvedAtUtc,
            nowUtc ?? StartedAtUtc);

    private static SlaSettings Settings(double thresholdPercent = 25) => new()
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
        AtRiskThresholdPercent = thresholdPercent
    };
}
