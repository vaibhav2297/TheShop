using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth.Commands.RequestSignInOtp;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Pages.Auth;

/// <summary>
/// Sign-in page (step 1 of 2). Collects the user's email and dispatches
/// <see cref="RequestSignInOtpCommand"/>. On success navigates to the OTP verify page.
/// </summary>
[Route(Routes.Auth.SignIn)]
public partial class SignIn : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    /// <summary>
    /// Optional URL to redirect to after a successful sign-in. When absent, the user is
    /// sent to the home page.
    /// </summary>
    [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    private MudForm _form = default!;
    private string _email = string.Empty;
    private bool _isFormValid;

    private readonly Func<string, string?> _emailValidation = email =>
        string.IsNullOrWhiteSpace(email)
            ? Strings.Email_Required
            : !email.Contains('@')
                ? Strings.Email_Invalid
                : null;

    private async Task OnSendCodeAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid) return;

        await BusyState.RunAsync(BusyKeys.Auth.SignIn, async () =>
        {
            var result = await Mediator.Send(new RequestSignInOtpCommand(_email.Trim()));

            if (result.IsSuccess)
            {
                Snackbar.Add(Strings.Auth_CodeSent, Severity.Success);
                Nav.NavigateTo(Routes.Auth.SignInVerifyWith(_email.Trim(), ReturnUrl));
            }
            else
            {
                Snackbar.Add(Localizer[result.Error ?? nameof(Strings.Auth_Unexpected)], Severity.Error);
            }
        });
    }
}
