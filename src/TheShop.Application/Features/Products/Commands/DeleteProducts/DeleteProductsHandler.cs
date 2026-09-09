using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Roles;

namespace TheShop.Application.Features.Products.Commands.DeleteProducts;

/// <summary>
/// Handles <see cref="DeleteProductsCommand"/>. Delegates the atomic partial-success delete to
/// <see cref="IProductRepository.DeleteManyAsync"/>, then best-effort disposes the exclusive image
/// objects of the products that were actually deleted (RULE-8) — the rows are already gone, so a
/// storage failure here never fails the request.
/// </summary>
public sealed class DeleteProductsHandler(IProductRepository products, IFileStorage fileStorage)
    : IRequestHandler<DeleteProductsCommand, Result<ProductDeletionOutcomeDto>>
{
    /// <inheritdoc/>
    public async Task<Result<ProductDeletionOutcomeDto>> Handle(
        DeleteProductsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var (outcome, deletedImageKeys) = await products.DeleteManyAsync(request.ProductIds, cancellationToken);

            foreach (var imageKey in deletedImageKeys)
            {
                try
                {
                    await fileStorage.DeleteAsync(StorageArea.ProductImages, imageKey, cancellationToken);
                }
                catch
                {
                    // Best-effort compensation: an orphaned storage object is acceptable; failing
                    // this request a second time over cleanup is not.
                }
            }

            return Result.Ok(outcome);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            return Result.Fail<ProductDeletionOutcomeDto>(RbacErrorKeys.AccessDenied);
        }
        catch (Exception)
        {
            return Result.Fail<ProductDeletionOutcomeDto>(ProductErrorKeys.DeleteFailed);
        }
    }

    // The delete_products RPC re-checks authorize('products.delete') itself (defense in depth
    // behind the AuthorizationBehavior pipeline gate) and raises this exact message on failure.
    private static bool IsAccessDenied(Exception ex) =>
        ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase);
}
