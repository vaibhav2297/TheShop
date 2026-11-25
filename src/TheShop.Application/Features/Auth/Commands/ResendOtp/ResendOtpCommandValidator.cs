using FluentValidation;

namespace TheShop.Application.Features.Auth.Commands.ResendOtp;

public sealed class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(AuthErrorKeys.EmailRequired)
            .EmailAddress().WithMessage(AuthErrorKeys.EmailInvalid);

        RuleFor(x => x.Purpose)
            .IsInEnum();
    }
}
