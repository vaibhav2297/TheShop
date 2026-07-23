using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The Manage Brands admin harness shell — AddBrand's entry point and post-save return target
/// (Decision 6). Gated at the route level on <c>PolicyNames.AdminArea</c> via
/// <c>Pages/Admin/_Imports.razor</c>, and additionally on <c>brands.view</c> within the page
/// itself, rendering the shared <see cref="Components.Common.AccessDeniedView"/> in place when
/// that finer-grained permission is missing. Content shell only — full brand management is a
/// separate future feature.
/// </summary>
[Route(Routes.Admin.ManageBrands)]
public partial class ManageBrands : ComponentBase
{
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Current(Strings.ManageBrands_Heading));
    }
}
