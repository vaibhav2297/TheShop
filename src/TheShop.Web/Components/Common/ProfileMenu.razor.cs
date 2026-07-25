using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth.Commands.SignOut;
using TheShop.Application.Features.Customers.Queries.GetCurrentCustomerProfile;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>
/// The authenticated user's account dropdown in <see cref="ShopAppBar"/>: shows the signed-in
/// customer's name and email, quick links to their account surfaces, a permission-gated admin-area
/// entry, and the sign-out action. Renders only inside an <c>Authorized</c> context; the customer
/// name and email are loaded on init via <see cref="GetCurrentCustomerProfileQuery"/>.
/// </summary>
public partial class ProfileMenu : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    private string? _displayName;
    private string? _email;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        var result = await Mediator.Send(new GetCurrentCustomerProfileQuery());
        if (result.IsSuccess)
        {
            var profile = result.Value;
            _displayName = $"{profile.FirstName} {profile.LastName}".Trim();
            _email = profile.Email;
        }
    }

    private async Task OnSignOutAsync()
    {
        await BusyState.RunAsync(BusyKeys.Global, async () =>
        {
            await Mediator.Send(new SignOutCommand());
            Snackbar.Add(Localizer[nameof(Strings.Auth_SignedOut)], Severity.Success);
            Nav.NavigateTo(Routes.Home);
        });
    }
}
