using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Commands.SetCategoryStatus;

/// <summary>
/// Handles <see cref="SetCategoryStatusCommand"/>. Applies the target status to every selected
/// category unconditionally, but counts only the categories that actually changed state — one
/// already in the target status is a no-op and is excluded (plan §6 Flow 4 step 3). A category
/// deleted concurrently by someone else is silently skipped rather than failing the whole batch.
/// </summary>
public sealed class SetCategoryStatusHandler(ICategoryRepository categories)
    : IRequestHandler<SetCategoryStatusCommand, Result<CategoryStatusChangeDto>>
{
    /// <inheritdoc/>
    public async Task<Result<CategoryStatusChangeDto>> Handle(
        SetCategoryStatusCommand request, CancellationToken cancellationToken)
    {
        var changedCount = 0;

        try
        {
            foreach (var id in request.CategoryIds)
            {
                var category = await categories.GetByIdAsync(id, cancellationToken);
                if (category is null)
                    continue;

                var wasActive = category.IsActive;

                if (request.IsActive)
                    category.Activate();
                else
                    category.Deactivate();

                var updateResult = await categories.UpdateAsync(category, cancellationToken);
                if (updateResult.IsFailure)
                    return Result.Fail<CategoryStatusChangeDto>(updateResult.Error!);

                if (wasActive != request.IsActive)
                    changedCount++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Result.Fail<CategoryStatusChangeDto>(CategoryErrorKeys.StatusChangeFailed);
        }

        return Result.Ok(new CategoryStatusChangeDto(changedCount));
    }
}
