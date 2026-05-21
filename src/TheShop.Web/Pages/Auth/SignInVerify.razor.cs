using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using TheShop.Application.Features.Auth.Commands.VerifySignInOtp;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Auth;

[Route(Routes.Auth.SignInVerify)]
public partial class SignInVerify : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private AuthState AuthState { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery] public string? Email { get; set; }
    [Parameter, SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    private string _code = string.Empty;
    private int _resendSeconds = 60;
    private System.Threading.Timer? _timer;

    protected override void OnInitialized()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Nav.NavigateTo(Routes.Auth.SignIn, replace: true);
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
        if (string.IsNullOrWhiteSpace(Email)) return;

        await BusyState.RunAsync(BusyKeys.Auth.SignInVerify, async () =>
        {
            var result = await Mediator.Send(new VerifySignInOtpCommand(Email, _code));

            if (result.IsFailure)
            {
                Snackbar.Add(Localizer[result.Error!], Severity.Error);

                if (result.Error == AuthErrorKeys.TooManyAttempts)
                    Nav.NavigateTo(Routes.Auth.SignIn, replace: true);
                return;
            }

            var session = result.Value;
            AuthState.SetUser(session.UserId.ToString(), session.Email);

            if (AuthStateProvider is SupabaseAuthStateProvider sup)
                sup.NotifyChanged();

            Snackbar.Add(Localizer[nameof(Strings.Auth_SignedIn)], Severity.Success);

            Nav.NavigateTo(string.IsNullOrWhiteSpace(ReturnUrl) ? Routes.Home : ReturnUrl);
        });
    }

    private async Task OnResendAsync()
    {
        if (string.IsNullOrWhiteSpace(Email)) return;

        await BusyState.RunAsync(BusyKeys.Auth.ResendOtp, async () =>
        {
            var result = await Mediator.Send(new ResendOtpCommand(Email, OtpPurpose.SignIn));

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
