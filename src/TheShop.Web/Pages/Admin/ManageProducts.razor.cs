using Microsoft.AspNetCore.Components;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The Manage Products admin harness shell — the verification surface for RBAC's admin gating
/// (Figma node <c>2470:2200</c>). Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on <c>products.view</c> within the page
/// itself. Content shell only — full product management ships with the admin-catalogue
/// feature.
/// </summary>
[Route(Routes.Admin.ManageProducts)]
[AuthorizePermission("products.view")]
public partial class ManageProducts : ComponentBase
{
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Current(Strings.ManageProducts_Heading));
    }
}
