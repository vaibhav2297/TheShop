namespace TheShop.Application.Features.Brands.DTOs;

/// <summary>
/// The outcome of a single or bulk activate/deactivate. <see cref="ChangedCount"/> counts only
/// brands that actually changed state — a brand already in the target status is a no-op and is
/// excluded (plan §6 Flow 3 step 3).
/// </summary>
public sealed record BrandStatusChangeDto(int ChangedCount);
