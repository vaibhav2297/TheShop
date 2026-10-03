using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using TheShop.Application.Features.Auth.Commands.RequestSignInOtp;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Pages.Auth;

/// <summary>Collects an email and requests a sign-in code, preserving the optional return URL.</summary>
[Route(Routes.Auth.SignIn)]
public partial class SignIn : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    /// <summary>Optional destination forwarded to the existing OTP verification route.</summary>
    [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    private readonly SignInFormModel _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool CanSubmit => !string.IsNullOrWhiteSpace(_model.Email) && _model.Email.Contains('@');

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += OnFieldChanged;
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        ValidateEmail();
    }

    private bool ValidateEmail()
    {
        var field = _editContext.Field(nameof(SignInFormModel.Email));
        _messages.Clear(field);
        var error = string.IsNullOrWhiteSpace(_model.Email)
            ? Strings.Email_Required
            : !_model.Email.Contains('@') ? Strings.Email_Invalid : null;
        if (error is not null) _messages.Add(field, error);
        _editContext.NotifyValidationStateChanged();
        return error is null;
    }

    private async Task OnSendCodeAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Auth.SignIn) || !ValidateEmail()) return;
        var email = _model.Email.Trim();

        await BusyState.RunAsync(BusyKeys.Auth.SignIn, async () =>
        {
            var result = await Mediator.Send(new RequestSignInOtpCommand(email));
            if (result.IsSuccess)
            {
                Notifications.Show(Strings.Auth_CodeSent, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Auth.SignInVerifyWith(email, ReturnUrl));
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Auth_Unexpected)], ShopNotificationKind.Error);
            }
        });
    }

    /// <summary>Releases the form's validation subscription.</summary>
    public void Dispose()
    {
        _editContext.OnFieldChanged -= OnFieldChanged;
    }
}
