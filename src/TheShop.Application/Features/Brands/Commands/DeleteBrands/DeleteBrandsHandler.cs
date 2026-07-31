using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Roles;

namespace TheShop.Application.Features.Brands.Commands.DeleteBrands;

/// <summary>
/// Handles <see cref="DeleteBrandsCommand"/>. Delegates the atomic partial-success delete to
/// <see cref="IBrandRepository.DeleteManyAsync"/>, then best-effort disposes the logo objects of
/// the brands that were actually deleted (RULE-11) — the row is already gone, so a storage
/// failure here never fails the request.
/// </summary>
public sealed class DeleteBrandsHandler(IBrandRepository brands, IFileStorage fileStorage)
    : IRequestHandler<DeleteBrandsCommand, Result<BrandDeletionOutcomeDto>>
{
    /// <inheritdoc/>
    public async Task<Result<BrandDeletionOutcomeDto>> Handle(
        DeleteBrandsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var (outcome, deletedLogoPaths) = await brands.DeleteManyAsync(request.BrandIds, cancellationToken);

            foreach (var logoPath in deletedLogoPaths)
            {
                try
                {
                    await fileStorage.DeleteAsync(StorageArea.BrandLogos, logoPath, cancellationToken);
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
            return Result.Fail<BrandDeletionOutcomeDto>(RbacErrorKeys.AccessDenied);
        }
        catch (Exception)
        {
            return Result.Fail<BrandDeletionOutcomeDto>(BrandErrorKeys.DeleteFailed);
        }
    }

    // The delete_brands RPC re-checks authorize('brands.delete') itself (defense in depth behind
    // the AuthorizationBehavior pipeline gate) and raises this exact message on failure.
    private static bool IsAccessDenied(Exception ex) =>
        ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase);
}
