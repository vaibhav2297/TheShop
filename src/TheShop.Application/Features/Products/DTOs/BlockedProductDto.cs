namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A product that activation skipped because it failed <c>Product.EnsurePublishable()</c> — it is
/// left Inactive (plan §5 Decision 3).
/// </summary>
public sealed record BlockedProductDto(Guid Id, string Name);
