using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.ApplyVariantImages;

/// <summary>Previews an ordered image replacement or bulk add/remove for explicitly selected variants.</summary>
public sealed record ApplyVariantImagesQuery(
    IReadOnlyList<VariantInput> Variants,
    IReadOnlyList<Guid> TargetVariantIds,
    IReadOnlyList<Guid> ImageIds,
    bool Append,
    IReadOnlyList<Guid> RemovedImageIds) : IRequest<Result<IReadOnlyList<VariantInput>>>;
