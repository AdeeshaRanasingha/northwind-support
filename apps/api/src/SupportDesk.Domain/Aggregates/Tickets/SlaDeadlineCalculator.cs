using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Calculates an SLA deadline from configured windows and a supplied UTC start.</summary>
public sealed class SlaDeadlineCalculator
{
    private readonly SlaSettings _settings;

    public SlaDeadlineCalculator(SlaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsValid(out var error))
        {
            throw new ArgumentException(error, nameof(settings));
        }

        _settings = settings;
    }

    public DateTime Calculate(
        DateTime startingAtUtc,
        TicketPriority priority,
        CustomerTier customerTier)
    {
        if (startingAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("SLA starting time must be UTC.", nameof(startingAtUtc));
        }

        var baseWindowHours = _settings.BaseWindowHours[priority];
        var multiplier = customerTier switch
        {
            CustomerTier.Standard => 1,
            CustomerTier.Premium => _settings.PremiumCustomerMultiplier,
            _ => throw new ArgumentOutOfRangeException(nameof(customerTier), customerTier, null)
        };
        var effectiveWindowHours = Math.Max(
            baseWindowHours * multiplier,
            _settings.MinimumWindowHours);

        return startingAtUtc.AddHours(effectiveWindowHours);
    }
}
