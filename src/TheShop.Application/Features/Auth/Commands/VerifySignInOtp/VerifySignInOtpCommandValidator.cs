using FluentValidation;

namespace TheShop.Application.Features.Auth.Commands.VerifySignInOtp;

public sealed class VerifySignInOtpCommandValidator
    : AbstractValidator<VerifySignInOtpCommand>
{
    public VerifySignInOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(AuthErrorKeys.EmailRequired)
            .EmailAddress().WithMessage(AuthErrorKeys.EmailInvalid);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(AuthErrorKeys.CodeInvalid)
            .Matches(@"^\d{6}$").WithMessage(AuthErrorKeys.CodeInvalid);
    }
}
