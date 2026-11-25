using TheShop.Application.Features.Admin;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Counts the current total number of records in a governed admin module, bypassing storefront
/// RLS visibility (e.g. unpublished products, inactive brands) so the count reflects every row
/// regardless of status (spec FR-3 / RULE-3).
/// </summary>
public interface IAdminDashboardRepository
{
    /// <summary>
    /// Returns the current total record count for <paramref name="module"/>.
    /// </summary>
    Task<int> CountModuleAsync(AdminModule module, CancellationToken cancellationToken);
}
