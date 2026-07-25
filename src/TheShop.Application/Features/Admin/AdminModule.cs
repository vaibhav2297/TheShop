namespace TheShop.Application.Features.Admin;

/// <summary>
/// A governed module shown on the admin console dashboard. The Infrastructure layer maps each
/// value to the <c>admin_module_count</c> RPC's <c>p_module</c> string; the Web layer maps each
/// value to its route, label, and icon.
/// </summary>
public enum AdminModule
{
    Products,
    Categories,
    Brands,
    Users,
    Roles,
}
