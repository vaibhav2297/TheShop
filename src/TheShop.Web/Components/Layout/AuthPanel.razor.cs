using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Layout;

/// <summary>Shared authentication panel with brand, account navigation, and responsive content placement.</summary>
public partial class AuthPanel : ComponentBase
{
    /// <summary>ID of the page heading that names the panel.</summary>
    [Parameter, EditorRequired] public string TitleId { get; set; } = string.Empty;

    /// <summary>Destination for switching between login and account creation.</summary>
    [Parameter] public string NavigationHref { get; set; } = Routes.Auth.SignUp;

    /// <summary>Resource-backed label for the account navigation link.</summary>
    [Parameter] public string NavigationLabel { get; set; } = Strings.SignUp;

    /// <summary>Page heading and form, placed at opposite ends of the available panel space.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
