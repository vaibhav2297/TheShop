using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Products.Commands.SetProductStatus;

/// <summary>
/// Handles <see cref="SetProductStatusCommand"/>. Deactivation is unconditional. Activation loads
/// each candidate with its variants and calls <c>Product.EnsurePublishable()</c>; a product that
/// fails is skipped and reported in <see cref="ProductStatusChangeDto.NotPublishable"/> rather
/// than failing the whole batch, and left Inactive (plan §5 Decision 3).
/// </summary>
public sealed class SetProductStatusHandler(IProductRepository products)
    : IRequestHandler<SetProductStatusCommand, Result<ProductStatusChangeDto>>
{
    /// <inheritdoc/>
    public async Task<Result<ProductStatusChangeDto>> Handle(
        SetProductStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!request.IsActive)
            {
                var deactivatedCount = await products.SetPublishedAsync(request.ProductIds, false, cancellationToken);
                return Result.Ok(new ProductStatusChangeDto(deactivatedCount, []));
            }

            var candidates = await products.GetManyWithVariantsAsync(request.ProductIds, cancellationToken);
            var eligibleIds = new List<Guid>();
            var notPublishable = new List<BlockedProductDto>();

            foreach (var product in candidates)
            {
                try
                {
                    product.SetPublished(true);
                    product.EnsurePublishable();
                    eligibleIds.Add(product.Id);
                }
                catch (ProductNotPublishableException)
                {
                    notPublishable.Add(new BlockedProductDto(product.Id, product.Name));
                }
            }

            var changedCount = eligibleIds.Count > 0
                ? await products.SetPublishedAsync(eligibleIds, true, cancellationToken)
                : 0;

            return Result.Ok(new ProductStatusChangeDto(changedCount, notPublishable));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            return Result.Fail<ProductStatusChangeDto>(RbacErrorKeys.AccessDenied);
        }
        catch (Exception)
        {
            return Result.Fail<ProductStatusChangeDto>(ProductErrorKeys.StatusChangeFailed);
        }
    }

    // SetPublishedAsync re-checks its own authorization at the persistence boundary (defense in
    // depth behind the AuthorizationBehavior pipeline gate) and raises this exact message on failure.
    private static bool IsAccessDenied(Exception ex) =>
        ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase);
}
