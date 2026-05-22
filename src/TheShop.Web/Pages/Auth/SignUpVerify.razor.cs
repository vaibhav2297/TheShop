using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Auth;

/// <summary>
/// Sign-up page (step 2 of 2). Reads profile data from <see cref="PendingSignUpState"/>,
/// dispatches <see cref="VerifySignUpOtpCommand"/>, and manages the resend cooldown.
/// If the pending state is absent (deep-link or refresh), redirects to <c>/sign-up</c>.
/// </summary>
[Route(Routes.Auth.SignUpVerify)]
public partial class SignUpVerify : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private AuthState AuthState { get; set; } = default!;
    [Inject] private PendingSignUpState PendingSignUp { get; set; } = default!;

    private MudForm _form = default!;
    private readonly string[] _digits = ["", "", "", "", "", ""];
    private bool _isFormValid;
    private int _resendCooldown;
    private System.Threading.Timer? _cooldownTimer;

    private string _pendingEmail => PendingSignUp.Email ?? string.Empty;
    private bool _isCodeComplete => _digits.All(d => d.Length == 1 && char.IsDigit(d[0]));
    private string _otpCode => string.Concat(_digits);

    protected override void OnInitialized()
    {
        if (!PendingSignUp.HasData)
        {
            Nav.NavigateTo(Routes.Auth.SignUp, replace: true);
            return;
        }

        StartCooldown(60);
    }

    private void OnDigitChanged(int index, string value)
    {
        if (value.Length > 1)
            value = value[^1..];

        _digits[index] = value;
        StateHasChanged();
    }

    private async Task OnVerifyAsync()
    {
        if (!_isCodeComplete || !PendingSignUp.HasData) return;

        await BusyState.RunAsync(BusyKeys.Auth.SignUpVerify, async () =>
        {
            var result = await Mediator.Send(new VerifySignUpOtpCommand(
                PendingSignUp.Email!,
                _otpCode,
                PendingSignUp.FirstName!,
                PendingSignUp.LastName!,
                PendingSignUp.DateOfBirth!.Value));

            if (result.IsSuccess)
            {
                var session = result.Value;
                PendingSignUp.Clear();
                AuthState.SetUser(session.UserId.ToString(), session.Email);
                Snackbar.Add(Strings.Auth_SignedIn, Severity.Success);
                Nav.NavigateTo(Routes.Home, forceLoad: false);
            }
            else
            {
                var key = result.Error ?? nameof(Strings.Auth_Unexpected);

                // Too many attempts: redirect back to sign-up start
                if (key == nameof(Strings.Auth_TooManyAttempts))
                {
                    Snackbar.Add(Localizer[key], Severity.Error);
                    PendingSignUp.Clear();
                    Nav.NavigateTo(Routes.Auth.SignUp, replace: true);
                    return;
                }

                Snackbar.Add(Localizer[key], Severity.Error);
                for (var i = 0; i < _digits.Length; i++) _digits[i] = "";
                await InvokeAsync(StateHasChanged);
            }
        });
    }

    private async Task OnResendAsync()
    {
        if (_resendCooldown > 0 || !PendingSignUp.HasData) return;

        await BusyState.RunAsync(BusyKeys.Auth.ResendOtp, async () =>
        {
            var result = await Mediator.Send(new ResendOtpCommand(
                PendingSignUp.Email!, OtpPurpose.SignUp));

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
        _cooldownTimer = new System.Threading.Timer(_ =>
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
