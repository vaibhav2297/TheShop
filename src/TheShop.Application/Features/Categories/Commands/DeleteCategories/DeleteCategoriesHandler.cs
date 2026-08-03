using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Roles;

namespace TheShop.Application.Features.Categories.Commands.DeleteCategories;

/// <summary>
/// Handles <see cref="DeleteCategoriesCommand"/>. Delegates the atomic partial-success delete to
/// <see cref="ICategoryRepository.DeleteManyAsync"/>, then best-effort disposes the image objects
/// of the categories that were actually deleted (RULE-11) — the row is already gone, so a storage
/// failure here never fails the request.
/// </summary>
public sealed class DeleteCategoriesHandler(ICategoryRepository categories, IFileStorage fileStorage)
    : IRequestHandler<DeleteCategoriesCommand, Result<CategoryDeletionOutcomeDto>>
{
    /// <inheritdoc/>
    public async Task<Result<CategoryDeletionOutcomeDto>> Handle(
        DeleteCategoriesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var (outcome, deletedImagePaths) = await categories.DeleteManyAsync(request.CategoryIds, cancellationToken);

            foreach (var imagePath in deletedImagePaths)
            {
                try
                {
                    await fileStorage.DeleteAsync(StorageArea.CategoryImages, imagePath, cancellationToken);
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
            return Result.Fail<CategoryDeletionOutcomeDto>(RbacErrorKeys.AccessDenied);
        }
        catch (Exception)
        {
            return Result.Fail<CategoryDeletionOutcomeDto>(CategoryErrorKeys.DeleteFailed);
        }
    }

    // The delete_categories RPC re-checks authorize('categories.delete') itself (defense in depth
    // behind the AuthorizationBehavior pipeline gate) and raises this exact message on failure.
    private static bool IsAccessDenied(Exception ex) =>
        ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase);
}
