using FluentValidation;

namespace TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;

public sealed class VerifySignUpOtpCommandValidator
    : AbstractValidator<VerifySignUpOtpCommand>
{
    public VerifySignUpOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(AuthErrorKeys.EmailRequired)
            .EmailAddress().WithMessage(AuthErrorKeys.EmailInvalid);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(AuthErrorKeys.CodeInvalid)
            .Matches(@"^\d{6}$").WithMessage(AuthErrorKeys.CodeInvalid);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(AuthErrorKeys.FirstNameRequired);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(AuthErrorKeys.LastNameRequired);
    }
}
