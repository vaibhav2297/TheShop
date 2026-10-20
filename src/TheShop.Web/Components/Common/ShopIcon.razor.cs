using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Decorative SVG from the trusted ShopIcons registry. The containing control owns its accessible name.</summary>
public partial class ShopIcon : ShopComponentBase
{
    /// <summary>Trusted application SVG fragment only; never pass user or remote markup.</summary>
    [Parameter, EditorRequired] public string Icon { get; set; } = string.Empty;

    private string ClassName => ShopCssClass.Join("shop-icon", Class);
}
