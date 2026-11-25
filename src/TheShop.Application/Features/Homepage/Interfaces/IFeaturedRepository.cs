using TheShop.Application.Common.Models;
using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Homepage.Interfaces;

/// <summary>Persists complete singleton configurations under the caller's permissions.</summary>
public interface IFeaturedRepository
{
    /// <summary>Returns the saved configuration, including Hidden, or null when absent.</summary>
    Task<FeaturedSection?> GetAsync(CancellationToken cancellationToken);
    /// <summary>Creates only an absent singleton; never overwrites an existing configuration.</summary>
    Task<Result> AddAsync(FeaturedSection section, CancellationToken cancellationToken);
    /// <summary>Atomically replaces an existing configuration; the last successful save wins.</summary>
    Task<Result> UpdateAsync(FeaturedSection section, CancellationToken cancellationToken);
}
