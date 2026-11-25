namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The outcome of a single or bulk activate/deactivate. <see cref="ChangedCount"/> counts only
/// products that actually changed state — one already in the target status is a no-op and is
/// excluded. <see cref="NotPublishable"/> lists the products activation skipped because they fail
/// <c>Product.EnsurePublishable()</c> (plan §5 Decision 3); always empty for a deactivation.
/// </summary>
public sealed record ProductStatusChangeDto(int ChangedCount, IReadOnlyList<BlockedProductDto> NotPublishable);
