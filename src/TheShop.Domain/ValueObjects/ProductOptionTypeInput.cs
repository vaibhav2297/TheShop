namespace TheShop.Domain.ValueObjects;

/// <summary>
/// One option type passed to <c>Product.SetOptionTypes</c>: an existing type
/// (<paramref name="Id"/> set) or a newly added one (<paramref name="Id"/> <c>null</c>).
/// </summary>
public sealed record ProductOptionTypeInput(Guid? Id, string Name, IReadOnlyList<ProductOptionValueInput> Values);
