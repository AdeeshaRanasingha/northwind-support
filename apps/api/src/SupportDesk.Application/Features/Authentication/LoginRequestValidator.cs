using FluentValidation;
using SupportDesk.Application.Contracts.Authentication;

namespace SupportDesk.Application.Features.Authentication;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(256);
    }
}
