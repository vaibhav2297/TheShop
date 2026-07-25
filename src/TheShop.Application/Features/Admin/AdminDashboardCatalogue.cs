using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Admin;

/// <summary>
/// The ordered set of dashboard modules and the permission that gates each one's card. A module
/// is included in the rendered dashboard only when the current user holds its
/// <see cref="ModuleEntry.ViewPermissionCode"/> (spec FR-5 / RULE-2).
/// </summary>
public static class AdminDashboardCatalogue
{
    /// <summary>
    /// One catalogue entry: a dashboard module paired with the permission code that gates its
    /// visibility.
    /// </summary>
    public sealed record ModuleEntry(AdminModule Module, string ViewPermissionCode);

    /// <summary>
    /// The five governed modules, in display order.
    /// </summary>
    public static readonly IReadOnlyList<ModuleEntry> Modules =
    [
        new(AdminModule.Products, PermissionCatalogue.Products.View.Code),
        new(AdminModule.Categories, PermissionCatalogue.Categories.View.Code),
        new(AdminModule.Brands, PermissionCatalogue.Brands.View.Code),
        new(AdminModule.Users, PermissionCatalogue.AdminUsers.View.Code),
        new(AdminModule.Roles, PermissionCatalogue.Roles.View.Code),
    ];
}
