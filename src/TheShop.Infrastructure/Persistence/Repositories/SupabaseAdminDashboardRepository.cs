using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Admin;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>
/// Supabase-backed implementation of <see cref="IAdminDashboardRepository"/>. Calls the
/// <c>admin_module_count</c> <c>SECURITY DEFINER</c> RPC (migration 0013), which re-checks the
/// module's <c>authorize()</c> gate itself and counts every row regardless of storefront RLS
/// visibility or status.
/// </summary>
public sealed class SupabaseAdminDashboardRepository(Supabase.Client client) : IAdminDashboardRepository
{
    /// <inheritdoc/>
    public async Task<int> CountModuleAsync(AdminModule module, CancellationToken cancellationToken)
    {
        // The Postgrest SDK's Rpc overloads take no CancellationToken; honor an already-cancelled
        // token before making the call rather than silently ignoring it.
        cancellationToken.ThrowIfCancellationRequested();

        var parameters = new Dictionary<string, object> { ["p_module"] = ToRpcModule(module) };
        var response = await client.Rpc("admin_module_count", parameters);

        return int.Parse(response.Content!);
    }

    private static string ToRpcModule(AdminModule module) => module switch
    {
        AdminModule.Products => "products",
        AdminModule.Categories => "categories",
        AdminModule.Brands => "brands",
        AdminModule.Users => "users",
        AdminModule.Roles => "roles",
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
    };
}
