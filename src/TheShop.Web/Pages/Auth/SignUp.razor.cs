using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components.Forms;
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

    private readonly SignUpFormModel _model = new();
    private EditContext _editContext = default!;
    private ShopCommandValidation _validation = default!;
    private ShopDateField _dateField = default!;

    /// <inheritdoc />
    protected override void OnInitialized() => _editContext = new EditContext(_model);

    private async Task OnSendCodeAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Auth.SignUp)) return;
        _validation.Clear();
        if (!_dateField.Validate()) return;
        // Consent is a Web-only confirmation, not an input on the existing command.
        if (!_model.AgeConfirmed)
        {
            _validation.ShowErrors([new(nameof(_model.AgeConfirmed), nameof(Strings.Auth_Underage))]);
            return;
        }

        var dob = _model.DateOfBirth ?? default;
        var firstName = _model.FirstName.Trim();
        var lastName = _model.LastName.Trim();
        var email = _model.Email.Trim();

        await BusyState.RunAsync(BusyKeys.Auth.SignUp, async () =>
        {
            var result = await Mediator.Send(new RequestSignUpOtpCommand(
                firstName,
                lastName,
                email,
                dob));

            if (result.IsSuccess)
            {
                PendingSignUp.Set(firstName, lastName, email, dob);
                Notifications.Show(Strings.Auth_CodeSent, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Auth.SignUpVerify);
            }
            else if (result.ValidationErrors.Count > 0)
            {
                _validation.ShowErrors(result.ValidationErrors);
            }
            else
            {
                Notifications.Show(Localizer[result.Error ?? nameof(Strings.Auth_Unexpected)], ShopNotificationKind.Error);
            }
        });
    }
}
