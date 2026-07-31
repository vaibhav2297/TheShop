using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Commands.SetBrandStatus;

/// <summary>
/// Handles <see cref="SetBrandStatusCommand"/>. Applies the target status to every selected brand
/// unconditionally, but counts only the brands that actually changed state — one already in the
/// target status is a no-op and is excluded (plan §6 Flow 3 step 3). A brand deleted concurrently
/// by someone else is silently skipped rather than failing the whole batch.
/// </summary>
public sealed class SetBrandStatusHandler(IBrandRepository brands)
    : IRequestHandler<SetBrandStatusCommand, Result<BrandStatusChangeDto>>
{
    /// <inheritdoc/>
    public async Task<Result<BrandStatusChangeDto>> Handle(
        SetBrandStatusCommand request, CancellationToken cancellationToken)
    {
        var changedCount = 0;

        try
        {
            foreach (var id in request.BrandIds)
            {
                var brand = await brands.GetByIdAsync(id, cancellationToken);
                if (brand is null)
                    continue;

                var wasActive = brand.IsActive;

                if (request.IsActive)
                    brand.Activate();
                else
                    brand.Deactivate();

                var updateResult = await brands.UpdateAsync(brand, cancellationToken);
                if (updateResult.IsFailure)
                    return Result.Fail<BrandStatusChangeDto>(updateResult.Error!);

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
            return Result.Fail<BrandStatusChangeDto>(BrandErrorKeys.StatusChangeFailed);
        }

        return Result.Ok(new BrandStatusChangeDto(changedCount));
    }
}
