using TheShop.Application.Features.Homepage.DTOs;

namespace TheShop.Application.Features.Homepage.Interfaces;

/// <summary>Reads published product cards for automatic and manual selection.</summary>
public interface IFreshDropsProductReader
{
    /// <summary>Reads one bounded, newest-first page, optionally filtered by IDs or name.</summary>
    Task<IReadOnlyList<FreshDropsProductDto>> GetAsync(IReadOnlyList<Guid>? ids, string? search,
        int limit, int offset, CancellationToken cancellationToken);
}
