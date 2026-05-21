using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Auth;

/// <summary>
/// Sign-up page. Collects the customer's profile details and sends a sign-up OTP via
/// <see cref="RequestSignUpOtpCommand"/>. Stores the profile in <see cref="PendingSignUpState"/>
/// for use by the verification step, then navigates to OTP entry.
/// </summary>
[Route(Routes.Auth.SignUp)]
public partial class SignUp : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private PendingSignUpState Pending { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    private MudForm _form = default!;
    private bool _isFormValid;

    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private DateTime? _dateOfBirth;

    private string? ValidateEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Localizer[nameof(Strings.Email_Required)];

        var attr = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        return attr.IsValid(value) ? null : Localizer[nameof(Strings.Email_Invalid)].Value;
    }

    private string? ValidateDateOfBirth(DateTime? value)
    {
        if (!value.HasValue)
            return Localizer[nameof(Strings.Auth_Dob_InPast)].Value;

        var dob = DateOnly.FromDateTime(value.Value);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        if (dob >= today)
            return Localizer[nameof(Strings.Auth_Dob_InPast)].Value;

        var age = today.Year - dob.Year;
        if (today < dob.AddYears(age)) age--;

        return age >= 19 ? null : Localizer[nameof(Strings.Auth_Underage)].Value;
    }

    private async Task OnSubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid || !_dateOfBirth.HasValue) return;

        await BusyState.RunAsync(BusyKeys.Auth.SignUp, async () =>
        {
            var dob = DateOnly.FromDateTime(_dateOfBirth.Value);
            var result = await Mediator.Send(
                new RequestSignUpOtpCommand(_firstName, _lastName, _email, dob));

            if (result.IsFailure)
            {
                Snackbar.Add(Localizer[result.Error!], Severity.Error);
                return;
            }

            Pending.Set(_firstName, _lastName, _email, dob);
            Snackbar.Add(Localizer[nameof(Strings.Auth_CodeSent)], Severity.Success);
            Nav.NavigateTo(Routes.Auth.SignUpVerify);
        });
    }
}
