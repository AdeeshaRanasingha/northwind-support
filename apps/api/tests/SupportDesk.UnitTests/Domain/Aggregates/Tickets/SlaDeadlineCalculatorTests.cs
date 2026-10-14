using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class SlaDeadlineCalculatorTests
{
    private static readonly DateTime StartUtc = FixedClock.DefaultNow;

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void Calculate_ForStandardCustomer_UsesConfiguredBaseWindow(
        TicketPriority priority,
        double expectedHours)
    {
        var calculator = new SlaDeadlineCalculator(StandardSettings());

        var dueAtUtc = calculator.Calculate(StartUtc, priority, CustomerTier.Standard);

        Assert.Equal(StartUtc.AddHours(expectedHours), dueAtUtc);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void Calculate_ForPremiumCustomer_AppliesConfiguredMultiplier(
        TicketPriority priority,
        double expectedHours)
    {
        var calculator = new SlaDeadlineCalculator(StandardSettings());

        var dueAtUtc = calculator.Calculate(StartUtc, priority, CustomerTier.Premium);

        Assert.Equal(StartUtc.AddHours(expectedHours), dueAtUtc);
    }

    [Fact]
    public void Calculate_WhenPremiumWindowIsTooShort_UsesConfiguredMinimum()
    {
        var settings = StandardSettings();
        settings.BaseWindowHours[TicketPriority.Critical] = 0.5;
        var calculator = new SlaDeadlineCalculator(settings);

        var dueAtUtc = calculator.Calculate(StartUtc, TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(StartUtc.AddHours(1), dueAtUtc);
    }

    [Fact]
    public void Calculate_WhenConfigurationChanges_UsesNewValues()
    {
        var settings = StandardSettings();
        settings.BaseWindowHours[TicketPriority.High] = 10;
        settings.PremiumCustomerMultiplier = 0.25;
        var calculator = new SlaDeadlineCalculator(settings);

        var dueAtUtc = calculator.Calculate(StartUtc, TicketPriority.High, CustomerTier.Premium);

        Assert.Equal(StartUtc.AddHours(2.5), dueAtUtc);
    }

    [Fact]
    public void Calculate_UsesSuppliedUtcStartingTime()
    {
        var suppliedStartUtc = new DateTime(2030, 6, 15, 13, 45, 0, DateTimeKind.Utc);
        var calculator = new SlaDeadlineCalculator(StandardSettings());

        var dueAtUtc = calculator.Calculate(
            suppliedStartUtc,
            TicketPriority.Medium,
            CustomerTier.Standard);

        Assert.Equal(suppliedStartUtc.AddHours(24), dueAtUtc);
        Assert.Equal(DateTimeKind.Utc, dueAtUtc.Kind);
    }

    [Fact]
    public void Calculate_WithForcedCriticalPriority_UsesCriticalWindow()
    {
        var category = new Category(
            "General enquiry",
            requiresSpecialist: false,
            forcesCriticalPriority: true);
        var priority = TicketPriorityCalculator.Calculate(TicketPriority.Low, category);
        var calculator = new SlaDeadlineCalculator(StandardSettings());

        var dueAtUtc = calculator.Calculate(StartUtc, priority, CustomerTier.Standard);

        Assert.Equal(TicketPriority.Critical, priority);
        Assert.Equal(StartUtc.AddHours(4), dueAtUtc);
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Constructor_WithInvalidSettings_Throws(SlaSettings settings)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SlaDeadlineCalculator(settings));

        Assert.Contains("SLA", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<SlaSettings> InvalidSettings()
    {
        var missingPriority = StandardSettings();
        missingPriority.BaseWindowHours.Remove(TicketPriority.Low);

        var invalidBaseWindow = StandardSettings();
        invalidBaseWindow.BaseWindowHours[TicketPriority.High] = 0;

        var invalidMultiplier = StandardSettings();
        invalidMultiplier.PremiumCustomerMultiplier = 0;

        var invalidMinimum = StandardSettings();
        invalidMinimum.MinimumWindowHours = 0;

        var invalidThreshold = StandardSettings();
        invalidThreshold.AtRiskThresholdPercent = 101;

        return new TheoryData<SlaSettings>
        {
            missingPriority,
            invalidBaseWindow,
            invalidMultiplier,
            invalidMinimum,
            invalidThreshold
        };
    }

    private static SlaSettings StandardSettings() => new()
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
