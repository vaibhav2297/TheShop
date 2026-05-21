using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Auth;

/// <summary>
/// Sign-up OTP verification page. Verifies the code via <see cref="VerifySignUpOtpCommand"/>,
/// which creates the customer record. On success, updates <see cref="AuthState"/>,
/// clears <see cref="PendingSignUpState"/>, and navigates home.
/// Manages the 60-second resend countdown timer.
/// </summary>
[Route(Routes.Auth.SignUpVerify)]
public partial class SignUpVerify : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private PendingSignUpState Pending { get; set; } = default!;
    [Inject] private AuthState AuthState { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    private string _code = string.Empty;
    private int _resendSeconds = 60;
    private System.Threading.Timer? _timer;

    protected override void OnInitialized()
    {
        if (!Pending.HasData)
        {
            Nav.NavigateTo(Routes.Auth.SignUp, replace: true);
            return;
        }

        StartResendCountdown();
    }

    private void StartResendCountdown()
    {
        _resendSeconds = 60;
        _timer?.Dispose();
        _timer = new System.Threading.Timer(_ =>
        {
            if (_resendSeconds > 0)
            {
                _resendSeconds--;
                InvokeAsync(StateHasChanged);
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private async Task OnVerifyAsync()
    {
        if (!Pending.HasData) return;

        await BusyState.RunAsync(BusyKeys.Auth.SignUpVerify, async () =>
        {
            var command = new VerifySignUpOtpCommand(
                Pending.Email!,
                _code,
                Pending.FirstName!,
                Pending.LastName!,
                Pending.DateOfBirth!.Value);

            var result = await Mediator.Send(command);

            if (result.IsFailure)
            {
                Snackbar.Add(Localizer[result.Error!], Severity.Error);

                if (result.Error == AuthErrorKeys.TooManyAttempts)
                {
                    Pending.Clear();
                    Nav.NavigateTo(Routes.Auth.SignUp, replace: true);
                }
                return;
            }

            var session = result.Value;
            AuthState.SetUser(session.UserId.ToString(), session.Email);
            Pending.Clear();

            if (AuthStateProvider is SupabaseAuthStateProvider sup)
                sup.NotifyChanged();

            Snackbar.Add(Localizer[nameof(Strings.Auth_SignedIn)], Severity.Success);
            Nav.NavigateTo(Routes.Home);
        });
    }

    private async Task OnResendAsync()
    {
        if (!Pending.HasData) return;

        await BusyState.RunAsync(BusyKeys.Auth.ResendOtp, async () =>
        {
            var result = await Mediator.Send(
                new ResendOtpCommand(Pending.Email!, OtpPurpose.SignUp));

            if (result.IsFailure)
            {
                Snackbar.Add(Localizer[result.Error!], Severity.Error);
                return;
            }

            Snackbar.Add(Localizer[nameof(Strings.Auth_CodeSent)], Severity.Success);
            StartResendCountdown();
        });
    }

    public void Dispose() => _timer?.Dispose();
}
