using TheShop.Application.Common.Models;
using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Homepage.Interfaces;

/// <summary>Persistence contract for the singleton homepage category section.</summary>
public interface IHomepageCategoryRepository
{
    /// <summary>Reads the section, including a hidden one, for an authorized administrator.</summary>
    Task<HomepageCategorySection?> GetForAdminAsync(CancellationToken cancellationToken);

    /// <summary>Creates or updates atomically without upserting.</summary>
    Task<Result> SaveAsync(HomepageCategorySection section, bool create, CancellationToken cancellationToken);
}
