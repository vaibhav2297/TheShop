using Microsoft.AspNetCore.Components;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>Temporary Homepage administration entry point, restricted to Homepage View.</summary>
[Route(Routes.Admin.ManageHomepage)]
[AuthorizePermission(PermissionCatalogue.Homepage.ViewCode)]
public partial class ManageHomepage : ComponentBase
{
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnInitialized() =>
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Current(Strings.Homepage_Heading));
}

