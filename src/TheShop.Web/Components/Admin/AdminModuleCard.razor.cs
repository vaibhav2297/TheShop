using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using TheShop.Application.Features.Admin;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Admin;

/// <summary>
/// One overview card on the admin console dashboard: a governed module's name, current record
/// count (or a placeholder when unavailable), and a keyboard-focusable link to that module's
/// management page (spec FR-3, FR-4, FR-7, AC-5).
/// </summary>
public partial class AdminModuleCard : MudComponentBase
{
    /// <summary>The module this card represents.</summary>
    [Parameter, EditorRequired]
    public AdminModule Module { get; set; }

    /// <summary>The module's current record count, or <c>null</c> to render a placeholder.</summary>
    [Parameter]
    public int? Count { get; set; }

    private string Href => Module switch
    {
        AdminModule.Products => Routes.Admin.ManageProducts,
        AdminModule.Categories => Routes.Admin.ManageCategories,
        AdminModule.Brands => Routes.Admin.ManageBrands,
        AdminModule.Users => Routes.Admin.ManageUsers,
        AdminModule.Roles => Routes.Admin.ManageRoles,
        _ => throw new ArgumentOutOfRangeException(nameof(Module), Module, null),
    };

    private string Label => Module switch
    {
        AdminModule.Products => Strings.AdminConsole_Module_Products,
        AdminModule.Categories => Strings.AdminConsole_Module_Categories,
        AdminModule.Brands => Strings.AdminConsole_Module_Brands,
        AdminModule.Users => Strings.AdminConsole_Module_Users,
        AdminModule.Roles => Strings.AdminConsole_Module_Roles,
        _ => throw new ArgumentOutOfRangeException(nameof(Module), Module, null),
    };

    private string CountText => Count?.ToString() ?? Strings.AdminConsole_CountUnavailable;

    private string AriaLabel => string.Format(Strings.AdminConsole_CountAria, Label, CountText);

    private string ManageLabel => string.Format(Strings.AdminConsole_ManageAction, Label);

    private string ClassName =>
        new CssBuilder("mud-transparent")
        .AddClass(Class)
        .Build();
}
