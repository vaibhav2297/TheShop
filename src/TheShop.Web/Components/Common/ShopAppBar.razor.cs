using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Auth.Commands.SignOut;
using TheShop.Web.Common;
using TheShop.Web.Components.Layout;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Top navigation bar rendered in <see cref="MainLayout"/>. Displays the brand logo,
/// primary nav links, and auth-aware action icons. Owns the sign-out flow.
/// </summary>
public partial class ShopAppBar : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private AuthState AuthState { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    private async Task OnSignOutAsync()
    {
        await BusyState.RunAsync(BusyKeys.Global, async () =>
        {
            await Mediator.Send(new SignOutCommand());
            AuthState.Clear();
            Snackbar.Add(Localizer[nameof(Strings.Auth_SignedOut)], Severity.Success);
            Nav.NavigateTo(Routes.Home);
        });
    }
}
