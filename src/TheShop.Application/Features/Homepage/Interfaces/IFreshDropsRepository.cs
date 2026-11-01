using TheShop.Application.Common.Models;
using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Homepage.Interfaces;

/// <summary>
/// Loads and writes the singleton Fresh Drops settings.
/// </summary>
public interface IFreshDropsRepository
{
    /// <summary>
    /// Returns null only if the authorized singleton is absent.
    /// </summary>
    Task<FreshDrops?> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates without upserting.
    /// </summary>
    Task<Result> SaveAsync(FreshDrops drops, bool create, CancellationToken cancellationToken);
}
