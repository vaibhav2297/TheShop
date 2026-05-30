using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using TheShop.Application.Features.Auth.Commands.VerifySignInOtp;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Auth;

/// <summary>
/// Sign-in page (step 2 of 2). Collects the 6-digit OTP, dispatches
/// <see cref="VerifySignInOtpCommand"/>, and manages the 60-second resend cooldown.
/// </summary>
[Route(Routes.Auth.SignInVerify)]
public partial class SignInVerify : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    /// <summary>
    /// The email address the OTP was sent to. Supplied by <see cref="SignIn"/> via query string.
    /// If absent, <see cref="OnInitialized"/> redirects back to the sign-in page.
    /// </summary>
    [SupplyParameterFromQuery] public string? Email { get; set; }

    /// <summary>
    /// Optional URL to redirect to after a successful sign-in. When absent, the user is
    /// sent to the home page.
    /// </summary>
    [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    private const int OtpLength = 6;

    private MudForm _form = default!;
    private string _otp = string.Empty;
    private bool _isFormValid;
    private int _resendCooldown;
    private Timer? _cooldownTimer;

    private string _email => Email ?? string.Empty;
    private bool _isCodeComplete => _otp.Length == OtpLength;
    private string _otpCode => _otp;

    protected override void OnInitialized()
    {
        if (string.IsNullOrWhiteSpace(_email))
        {
            Nav.NavigateTo(Routes.Auth.SignIn, replace: true);
            return;
        }

        StartCooldown(60);
    }

    private async Task OnVerifyAsync()
    {
        if (!_isCodeComplete) return;

        await BusyState.RunAsync(BusyKeys.Auth.SignInVerify, async () =>
        {
            var result = await Mediator.Send(new VerifySignInOtpCommand(_email, _otpCode));

            if (result.IsSuccess)
            {
                Snackbar.Add(Strings.Auth_SignedIn, Severity.Success);

                var destination = !string.IsNullOrWhiteSpace(ReturnUrl)
                    ? ReturnUrl
                    : Routes.Home;
                Nav.NavigateTo(destination, forceLoad: false);
            }
            else
            {
                var key = result.Error ?? nameof(Strings.Auth_Unexpected);

                // Too many attempts: redirect back to email entry
                if (key == nameof(Strings.Auth_TooManyAttempts))
                {
                    Snackbar.Add(Localizer[key], Severity.Error);
                    Nav.NavigateTo(Routes.Auth.SignIn, replace: true);
                    return;
                }

                Snackbar.Add(Localizer[key], Severity.Error);

                // Clear the digits on failure so the user types fresh
                _otp = string.Empty;
                await InvokeAsync(StateHasChanged);
            }
        });
    }

    private async Task OnResendAsync()
    {
        if (_resendCooldown > 0) return;

        await BusyState.RunAsync(BusyKeys.Auth.ResendOtp, async () =>
        {
            var result = await Mediator.Send(new ResendOtpCommand(_email, OtpPurpose.SignIn));

            if (result.IsSuccess)
            {
                Snackbar.Add(Strings.Auth_CodeSent, Severity.Success);
                StartCooldown(result.Value.ResendCooldownSeconds);
            }
            else
            {
                Snackbar.Add(Localizer[result.Error ?? nameof(Strings.Auth_Unexpected)], Severity.Error);
            }
        });
    }

    private void StartCooldown(int seconds)
    {
        _resendCooldown = seconds;
        _cooldownTimer?.Dispose();
        _cooldownTimer = new Timer(_ =>
        {
            if (_resendCooldown > 0)
            {
                _resendCooldown--;
                InvokeAsync(StateHasChanged);
            }
        }, null, 1000, 1000);
    }

    public void Dispose() => _cooldownTimer?.Dispose();
}
