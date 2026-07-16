using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Full-page loading indicator rendered by <c>App.razor</c>'s <c>AuthorizeRouteView.Authorizing</c>
/// template while the authentication state is being resolved. The principal (including permission
/// claims) now comes synchronously from the access token, so this renders at most for a frame —
/// kept as a guard against a flash of <see cref="AccessDeniedView"/> on cold loads.
/// </summary>
public partial class AuthorizingView : MudComponentBase
{
    protected string Classname =>
        new CssBuilder("py-20")
            .AddClass(Class)
            .Build();
}
