namespace TheShop.Domain.ValueObjects;

/// <summary>
/// One specification row passed to <c>Product.SetSpecifications</c>: an existing row
/// (<paramref name="Id"/> set) or a newly added one (<paramref name="Id"/> <c>null</c>).
/// </summary>
public sealed record ProductSpecificationInput(Guid? Id, string Name, string Value);
