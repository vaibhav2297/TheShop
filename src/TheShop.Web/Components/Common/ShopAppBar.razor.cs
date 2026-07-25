using Microsoft.AspNetCore.Components;
using TheShop.Web.Components.Layout;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Top navigation bar rendered in <see cref="MainLayout"/>. Displays the brand logo,
/// primary nav links, and auth-aware action icons. The authenticated account dropdown and
/// sign-out flow are owned by <see cref="ProfileMenu"/>.
/// </summary>
public partial class ShopAppBar : ComponentBase
{
}
