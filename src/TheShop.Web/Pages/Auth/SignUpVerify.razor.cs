using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;
using TheShop.Domain.Enums;
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
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private PendingSignUpState PendingSignUp { get; set; } = default!;

    private const int OtpLength = 6;

    private MudForm _form = default!;
    private string _otp = string.Empty;
    private bool _isFormValid;
    private int _resendCooldown;
    private System.Threading.Timer? _cooldownTimer;

    private string _pendingEmail => PendingSignUp.Email ?? string.Empty;
    private bool _isCodeComplete => _otp.Length == OtpLength;
    private string _otpCode => _otp;

    protected override void OnInitialized()
    {
        if (!PendingSignUp.HasData)
        {
            Nav.NavigateTo(Routes.Auth.SignUp, replace: true);
            return;
        }

        StartCooldown(60);
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
                PendingSignUp.Clear();
                Notifications.Show(Strings.Auth_SignedIn, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Home, forceLoad: false);
            }
            else
            {
                var key = result.Error ?? nameof(Strings.Auth_Unexpected);

                // Too many attempts: redirect back to sign-up start
                if (key == nameof(Strings.Auth_TooManyAttempts))
                {
                    Notifications.Show(Localizer[key], ShopNotificationKind.Error);
                    PendingSignUp.Clear();
                    Nav.NavigateTo(Routes.Auth.SignUp, replace: true);
                    return;
                }

                Notifications.Show(Localizer[key], ShopNotificationKind.Error);
                _otp = string.Empty;
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
                Notifications.Show(Strings.Auth_CodeSent, ShopNotificationKind.Success);
                StartCooldown(result.Value.ResendCooldownSeconds);
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Auth_Unexpected)], ShopNotificationKind.Error);
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
