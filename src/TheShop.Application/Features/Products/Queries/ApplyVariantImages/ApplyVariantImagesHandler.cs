using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Queries.ApplyVariantImages;

/// <summary>Returns staged variants with only the requested target assignments changed.</summary>
public sealed class ApplyVariantImagesHandler
    : IRequestHandler<ApplyVariantImagesQuery, Result<IReadOnlyList<VariantInput>>>
{
    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<VariantInput>>> Handle(
        ApplyVariantImagesQuery request, CancellationToken cancellationToken)
    {
        var targets = request.TargetVariantIds.ToHashSet();
        IReadOnlyList<VariantInput> updated = request.Variants.Select(variant =>
        {
            if (variant.Id is not Guid id || !targets.Contains(id))
                return variant;
            var assignment = request.Append
                ? new VariantImageAssignment(variant.ImageIds).Remove(request.RemovedImageIds).AppendMissing(request.ImageIds)
                : new VariantImageAssignment(request.ImageIds);
            return variant with { ImageIds = assignment.ImageIds };
        }).ToArray();
        return Task.FromResult(Result.Ok(updated));
    }
}
