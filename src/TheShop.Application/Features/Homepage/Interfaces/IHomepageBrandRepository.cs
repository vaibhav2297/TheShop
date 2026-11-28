using TheShop.Application.Common.Models;
using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Homepage.Interfaces;

/// <summary>Persistence contract for the singleton homepage brand section.</summary>
public interface IHomepageBrandRepository
{
    /// <summary>Reads the section, including a hidden one, for authorized staff.</summary>
    Task<HomepageBrandSection?> GetForAdminAsync(CancellationToken cancellationToken);

    /// <summary>Creates or updates the complete section atomically without upserting.</summary>
    Task<Result> SaveAsync(HomepageBrandSection section, bool create, CancellationToken cancellationToken);
}
