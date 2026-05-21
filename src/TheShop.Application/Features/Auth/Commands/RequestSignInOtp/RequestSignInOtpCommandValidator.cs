using FluentValidation;

namespace TheShop.Application.Features.Auth.Commands.RequestSignInOtp;

public sealed class RequestSignInOtpCommandValidator
    : AbstractValidator<RequestSignInOtpCommand>
{
    public RequestSignInOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(AuthErrorKeys.EmailRequired)
            .EmailAddress().WithMessage(AuthErrorKeys.EmailInvalid);
    }
}
