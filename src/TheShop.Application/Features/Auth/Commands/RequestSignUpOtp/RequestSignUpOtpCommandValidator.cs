using FluentValidation;

namespace TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;

public sealed class RequestSignUpOtpCommandValidator
    : AbstractValidator<RequestSignUpOtpCommand>
{
    public RequestSignUpOtpCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(AuthErrorKeys.FirstNameRequired);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(AuthErrorKeys.LastNameRequired);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(AuthErrorKeys.EmailRequired)
            .EmailAddress().WithMessage(AuthErrorKeys.EmailInvalid);

        RuleFor(x => x.DateOfBirth)
            .Must(BeInThePast).WithMessage(AuthErrorKeys.DobInPast)
            .Must(BeAtLeast19YearsOld).WithMessage(AuthErrorKeys.Underage);
    }

    private static bool BeInThePast(DateOnly dob) =>
        dob < DateOnly.FromDateTime(DateTime.UtcNow.Date);

    private static bool BeAtLeast19YearsOld(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var age = today.Year - dob.Year;
        if (today < dob.AddYears(age)) age--;
        return age >= 19;
    }
}
