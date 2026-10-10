using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Auth;

/// <summary>
/// Sign-up page (step 1 of 2). Collects the user's profile details and dispatches
/// <see cref="RequestSignUpOtpCommand"/>. On success stores data in <see cref="PendingSignUpState"/>
/// and navigates to the OTP verify page.
/// </summary>
[Route(Routes.Auth.SignUp)]
public partial class SignUp : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private PendingSignUpState PendingSignUp { get; set; } = default!;

    private MudForm _form = default!;
    private ShopDateField _dateField = default!;
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private DateOnly? _dateOfBirth;
    private bool _ageConfirmed;
    private bool _isFormValid;

    // Maximum selectable date: today minus 19 years — enforces age on picker level (UX hint only;
    // authoritative check is in the domain / Application layer).
    private readonly DateOnly _maxDate = DateOnly.FromDateTime(DateTime.Today).AddYears(-19);
    private bool CanSendCode => _isFormValid && _dobValidation(_dateOfBirth) is null;

    private readonly Func<string, string?> _emailValidation = email =>
        string.IsNullOrWhiteSpace(email)
            ? Strings.Email_Required
            : !email.Contains('@')
                ? Strings.Email_Invalid
                : null;

    private readonly Func<DateOnly?, string?> _dobValidation = dob =>
    {
        if (dob is null) return Strings.Auth_Dob_InPast;
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (dob.Value >= today) return Strings.Auth_Dob_InPast;

        var age = today.Year - dob.Value.Year;
        if (dob.Value > today.AddYears(-age)) age--;
        if (age < 19) return Strings.Auth_Underage;

        return null;
    };

    private async Task OnSendCodeAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Auth.SignUp)) return;
        var dateValid = _dateField.Validate();
        await _form.ValidateAsync();
        if (!dateValid || !_isFormValid) return;

        var dob = _dateOfBirth!.Value;

        await BusyState.RunAsync(BusyKeys.Auth.SignUp, async () =>
        {
            var result = await Mediator.Send(new RequestSignUpOtpCommand(
                _firstName.Trim(),
                _lastName.Trim(),
                _email.Trim(),
                dob));

            if (result.IsSuccess)
            {
                PendingSignUp.Set(_firstName.Trim(), _lastName.Trim(), _email.Trim(), dob);
                Notifications.Show(Strings.Auth_CodeSent, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Auth.SignUpVerify);
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Auth_Unexpected)], ShopNotificationKind.Error);
            }
        });
    }
}
