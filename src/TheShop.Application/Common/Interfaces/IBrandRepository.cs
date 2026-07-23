using TheShop.Application.Common.Models;
using TheShop.Domain.Entities;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Brand"/>. Implementations live in the
/// Infrastructure layer.
/// </summary>
public interface IBrandRepository
{
    /// <summary>
    /// Returns <c>true</c> when a brand already exists whose name matches
    /// <paramref name="name"/> case- and whitespace-insensitively (RULE-2).
    /// </summary>
    Task<bool> ExistsByNormalizedNameAsync(string name, CancellationToken ct);

    /// <summary>
    /// Persists a newly created brand. Fails with a resource-key <see cref="Result"/> when a
    /// concurrent insert has since taken the name; throws for unexpected technical failures.
    /// </summary>
    Task<Result> AddAsync(Brand brand, CancellationToken ct);
}
