using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The Manage Products admin harness shell — the verification surface for RBAC's admin gating
/// (Figma node <c>2470:2200</c>). Gated at the route level on <c>PolicyNames.AdminArea</c> via
/// <c>Pages/Admin/_Imports.razor</c>, and additionally on <c>products.view</c> within the page
/// itself, rendering the shared <see cref="Components.Common.AccessDeniedView"/> in place when
/// that finer-grained permission is missing. Content shell only — full product management ships
/// with the admin-catalogue feature.
/// </summary>
[Route(Routes.Admin.ManageProducts)]
public partial class ManageProducts : ComponentBase
{
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Current(Strings.ManageProducts_Heading));
    }
}
