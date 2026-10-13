using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class TicketPriorityCalculatorTests
{
    [Fact]
    public void Calculate_WithoutRequestedPriority_DefaultsToMedium()
    {
        var category = Category(forcesCriticalPriority: false);

        var result = TicketPriorityCalculator.Calculate(null, category);

        Assert.Equal(TicketPriority.Medium, result);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Calculate_WithRequestedPriority_PreservesIt(TicketPriority requestedPriority)
    {
        var category = Category(forcesCriticalPriority: false);

        var result = TicketPriorityCalculator.Calculate(requestedPriority, category);

        Assert.Equal(requestedPriority, result);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    public void Calculate_WhenCategoryForcesCritical_OverridesRequestedPriority(
        TicketPriority requestedPriority)
    {
        var category = Category(forcesCriticalPriority: true);

        var result = TicketPriorityCalculator.Calculate(requestedPriority, category);

        Assert.Equal(TicketPriority.Critical, result);
    }

    [Fact]
    public void Calculate_WhenCategoryDoesNotForceCritical_PreservesRequestedPriority()
    {
        var category = Category(forcesCriticalPriority: false);

        var result = TicketPriorityCalculator.Calculate(TicketPriority.Low, category);

        Assert.Equal(TicketPriority.Low, result);
    }

    [Fact]
    public void Calculate_UsesCategoryFlagRatherThanCategoryName()
    {
        var outageWithoutFlag = new Category(
            "Outage",
            requiresSpecialist: false,
            forcesCriticalPriority: false);
        var ordinaryNameWithFlag = new Category(
            "General enquiry",
            requiresSpecialist: false,
            forcesCriticalPriority: true);

        Assert.Equal(
            TicketPriority.Low,
            TicketPriorityCalculator.Calculate(TicketPriority.Low, outageWithoutFlag));
        Assert.Equal(
            TicketPriority.Critical,
            TicketPriorityCalculator.Calculate(TicketPriority.Low, ordinaryNameWithFlag));
    }

    private static Category Category(bool forcesCriticalPriority) =>
        new("Test category", requiresSpecialist: false, forcesCriticalPriority);
}
