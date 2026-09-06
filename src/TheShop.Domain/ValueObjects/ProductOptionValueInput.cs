namespace TheShop.Domain.ValueObjects;

/// <summary>
/// One value of an option type passed to <c>Product.SetOptionTypes</c>: an existing value
/// (<paramref name="Id"/> set) or a newly added one (<paramref name="Id"/> <c>null</c>).
/// </summary>
public sealed record ProductOptionValueInput(Guid? Id, string Value);
