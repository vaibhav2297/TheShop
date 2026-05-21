using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth.Commands.RequestSignInOtp;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Pages.Auth;

[Route(Routes.Auth.SignIn)]
public partial class SignIn : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    private MudForm _form = default!;
    private bool _isFormValid;
    private string _email = string.Empty;

    private string? ValidateEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Localizer[nameof(Strings.Email_Required)];

        var attr = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        return attr.IsValid(value) ? null : Localizer[nameof(Strings.Email_Invalid)].Value;
    }

    private async Task OnSubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid) return;

        await BusyState.RunAsync(BusyKeys.Auth.SignIn, async () =>
        {
            var result = await Mediator.Send(new RequestSignInOtpCommand(_email));

            if (result.IsFailure)
            {
                Snackbar.Add(Localizer[result.Error!], Severity.Error);
                return;
            }

            Snackbar.Add(Localizer[nameof(Strings.Auth_CodeSent)], Severity.Success);
            Nav.NavigateTo(Routes.Auth.SignInVerifyWith(_email, ReturnUrl));
        });
    }
}
