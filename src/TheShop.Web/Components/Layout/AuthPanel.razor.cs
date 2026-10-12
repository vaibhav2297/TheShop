using Microsoft.AspNetCore.Components;

namespace TheShop.Web.Components.Layout;

/// <summary>Shared sign-in panel with brand, account navigation, and responsive content placement.</summary>
public partial class AuthPanel : ComponentBase
{
    /// <summary>ID of the page heading that names the panel.</summary>
    [Parameter, EditorRequired] public string TitleId { get; set; } = string.Empty;

    /// <summary>Page heading and form, placed at opposite ends of the available panel space.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
