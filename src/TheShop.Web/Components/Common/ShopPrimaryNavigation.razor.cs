using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Shared primary links for the app bar and navigation drawer.</summary>
public partial class ShopPrimaryNavigation : ShopComponentBase
{
    /// <summary>Requests drawer closure when a link is activated; native navigation remains enabled.</summary>
    [Parameter] public EventCallback OnNavigate { get; set; }

    private string ClassName => ShopCssClass.Join("shop-primary-navigation", Class);
}
