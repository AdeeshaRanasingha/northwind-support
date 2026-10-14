namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public sealed class SlaEvaluator
{
    private readonly double _atRiskThreshold;

    public double AtRiskThresholdPercent { get; }

    public SlaEvaluator(SlaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsValid(out var error))
        {
            throw new ArgumentException(error, nameof(settings));
        }

        AtRiskThresholdPercent = settings.AtRiskThresholdPercent;
        _atRiskThreshold = AtRiskThresholdPercent / 100;
    }

    /// <summary>
    /// Evaluates the current SLA window. For an escalated ticket,
    /// <paramref name="slaStartedAtUtc"/> is the latest escalation time; otherwise it is the
    /// creation time. Closed tickets use their resolution timestamp, which the ticket records
    /// when closing if it was not already resolved.
    /// </summary>
    public SlaStatus Evaluate(
        DateTime slaStartedAtUtc,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        var totalWindow = dueAtUtc.Value - slaStartedAtUtc;
        var remaining = dueAtUtc.Value - nowUtc;

        return remaining <= TimeSpan.FromTicks((long)(totalWindow.Ticks * _atRiskThreshold))
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
