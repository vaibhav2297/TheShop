using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Auth;

/// <summary>
/// Component rendered by <c>AuthorizeRouteView</c> when the user is not authenticated.
/// Shows a session-expired snackbar and redirects to the sign-in page, preserving the
/// current URL as <c>returnUrl</c> so the user lands back after signing in.
/// </summary>
public partial class RedirectToSignIn : ComponentBase
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override void OnInitialized()
    {
        Snackbar.Add(Localizer[nameof(Strings.Auth_SessionExpired)], Severity.Info);

        var returnUrl = new Uri(Nav.Uri).PathAndQuery;

        Nav.NavigateTo(Routes.Auth.SignInWithReturn(returnUrl), forceLoad: false, replace: true);
    }
}
