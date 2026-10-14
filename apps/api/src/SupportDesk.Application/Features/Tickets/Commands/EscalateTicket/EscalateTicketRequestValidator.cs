using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .Must(reason => reason is not null
                && reason.Trim().Length is >= TicketEscalationService.MinReasonLength
                    and <= TicketEscalation.MaxReasonLength)
            .WithMessage(
                $"Reason must contain {TicketEscalationService.MinReasonLength} to "
                + $"{TicketEscalation.MaxReasonLength} characters.");

        RuleFor(request => request.EscalatedBy)
            .NotEmpty()
            .Must(actor => actor is not null
                && actor.Trim().Length <= TicketEscalation.MaxEscalatedByLength)
            .WithMessage(
                $"Escalated by must not exceed {TicketEscalation.MaxEscalatedByLength} characters.");
    }
}
