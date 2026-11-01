using Supabase.Postgrest.Exceptions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Exceptions;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>Reads bounded published cards for the Fresh Drops editor.</summary>
public sealed class SupabaseFreshDropsProductReader(Supabase.Client client, IFileStorage storage)
    : IFreshDropsProductReader
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<FreshDropsProductDto>> GetAsync(IReadOnlyList<Guid>? ids, string? search,
        int limit, int offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var rows = await client.Rpc<List<FreshDropsProductRecord>>("fresh_drops_product_cards", new
            {
                p_ids = ids,
                p_search = search,
                p_limit = limit,
                p_offset = offset
            });
            return rows?.Select(x => new FreshDropsProductDto(x.Id, x.Name, x.BrandName, x.DisplayPrice,
                x.Currency, x.ImageKey is { } key ? storage.GetPublicUrl(StorageArea.ProductImages, key) : null))
                .ToList() ?? [];
        }
        catch (PostgrestException ex) when (ex.Content?.Contains("42501", StringComparison.Ordinal) == true)
        {
            throw new DomainException(RbacErrorKeys.AccessDenied);
        }
    }
}
