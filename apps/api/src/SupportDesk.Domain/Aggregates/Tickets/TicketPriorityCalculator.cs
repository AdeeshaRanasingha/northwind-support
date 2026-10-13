using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Determines a new ticket's priority from the reporter's request and its category rules.
/// </summary>
public static class TicketPriorityCalculator
{
    public static TicketPriority Calculate(TicketPriority? requestedPriority, Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : requestedPriority ?? TicketPriority.Medium;
    }
}
