namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A product that <c>delete_products</c> could not delete because another record still
/// references it (FR-12, RULE-3). <see cref="ReferenceCount"/> feeds the row's in-use caption.
/// </summary>
public sealed record ReferencedProductDto(Guid Id, string Name, int ReferenceCount);
