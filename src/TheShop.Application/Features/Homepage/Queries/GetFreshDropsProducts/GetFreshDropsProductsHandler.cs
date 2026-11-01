using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;

/// <summary>Reads bounded published cards and restores selected order.</summary>
public sealed class GetFreshDropsProductsHandler(IFreshDropsProductReader reader)
    : IRequestHandler<GetFreshDropsProductsQuery, Result<IReadOnlyList<FreshDropsProductDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<FreshDropsProductDto>>> Handle(GetFreshDropsProductsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cards = await reader.GetAsync(request.Ids, request.Search, request.Limit, request.Offset, cancellationToken);
            if (request.Ids is { } ids)
            {
                var byId = cards.ToDictionary(x => x.Id);
                cards = [.. ids.Where(byId.ContainsKey).Select(id => byId[id])];
            }
            return Result.Ok(cards);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail<IReadOnlyList<FreshDropsProductDto>>(ex.MessageKey); }
        catch { return Result.Fail<IReadOnlyList<FreshDropsProductDto>>(FreshDropsErrorKeys.SearchFailed); }
    }
}
