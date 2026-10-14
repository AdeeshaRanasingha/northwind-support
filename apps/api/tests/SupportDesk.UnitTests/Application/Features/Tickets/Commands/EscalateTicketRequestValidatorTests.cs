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
            new EscalateTicketRequest(reason!, "shift-lead"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Fact]
    public async Task Validate_WithReasonLongerThanPersistenceLimit_Fails()
    {
        var result = await _validator.ValidateAsync(new EscalateTicketRequest(
            new string('x', TicketEscalation.MaxReasonLength + 1),
            "shift-lead"));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithMissingActor_Fails(string? actor)
    {
        var result = await _validator.ValidateAsync(
            new EscalateTicketRequest("Customer impact increased", actor!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.EscalatedBy));
    }

    [Fact]
    public async Task Validate_WithActorLongerThanPersistenceLimit_Fails()
    {
        var result = await _validator.ValidateAsync(new EscalateTicketRequest(
            "Customer impact increased",
            new string('x', TicketEscalation.MaxEscalatedByLength + 1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_WithTrimmedBoundaryValues_Succeeds()
    {
        var result = await _validator.ValidateAsync(new EscalateTicketRequest(
            $"  {new string('x', TicketEscalationService.MinReasonLength)}  ",
            $"  {new string('x', TicketEscalation.MaxEscalatedByLength)}  "));

        Assert.True(result.IsValid);
    }
}
