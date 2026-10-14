using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("four")]
    [InlineData("  four  ")]
    public async Task Validate_WithMissingOrShortTrimmedReason_Fails(string? reason)
    {
        var result = await _validator.ValidateAsync(
            new EscalateTicketRequest(reason!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Fact]
    public async Task Validate_WithReasonLongerThanPersistenceLimit_Fails()
    {
        var result = await _validator.ValidateAsync(new EscalateTicketRequest(
            new string('x', TicketEscalation.MaxReasonLength + 1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_WithTrimmedBoundaryValues_Succeeds()
    {
        var result = await _validator.ValidateAsync(new EscalateTicketRequest(
            $"  {new string('x', TicketEscalationService.MinReasonLength)}  "));

        Assert.True(result.IsValid);
    }
}
