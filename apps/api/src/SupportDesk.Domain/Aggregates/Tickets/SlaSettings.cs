namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Configurable response windows and thresholds for ticket SLAs.</summary>
public sealed class SlaSettings
{
    public const string SectionName = "Sla";

    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];

    public double PremiumCustomerMultiplier { get; set; }

    public double MinimumWindowHours { get; set; }

    /// <summary>Percentage of the effective SLA window at or below which a ticket is at risk.</summary>
    public double AtRiskThresholdPercent { get; set; }

    public bool IsValid(out string? error)
    {
        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!BaseWindowHours.TryGetValue(priority, out var hours)
                || !IsPositiveFinite(hours))
            {
                error = $"SLA base window for {priority} must be a positive finite number.";
                return false;
            }
        }

        if (!IsPositiveFinite(PremiumCustomerMultiplier) || PremiumCustomerMultiplier > 1)
        {
            error = "SLA premium customer multiplier must be greater than zero and at most one.";
            return false;
        }

        if (!IsPositiveFinite(MinimumWindowHours))
        {
            error = "SLA minimum window must be a positive finite number.";
            return false;
        }

        if (!IsPositiveFinite(AtRiskThresholdPercent) || AtRiskThresholdPercent > 100)
        {
            error = "SLA at-risk threshold percent must be greater than zero and at most 100.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsPositiveFinite(double value) => value > 0 && double.IsFinite(value);
}
