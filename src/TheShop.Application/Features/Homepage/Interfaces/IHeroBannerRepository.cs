using TheShop.Application.Common.Models;
using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Homepage.Interfaces;

/// <summary>Persists the singleton without upserting or removing it.</summary>
public interface IHeroBannerRepository
{
    /// <summary>Returns null only after an authorized read confirms absence.</summary>
    Task<HeroBanner?> GetAsync(CancellationToken cancellationToken);
    /// <summary>Loads save preparation using Create when true, or Edit when false, without requiring View.</summary>
    Task<HeroBanner?> GetForWriteAsync(bool create, CancellationToken cancellationToken);
    /// <summary>Creates the singleton; a concurrent creation returns a conflict.</summary>
    Task<Result> AddAsync(HeroBanner banner, CancellationToken cancellationToken);
    /// <summary>Atomically replaces the configuration of an existing singleton.</summary>
    Task<Result> UpdateAsync(HeroBanner banner, CancellationToken cancellationToken);
}
