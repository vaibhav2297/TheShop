namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One specification row in a create/update command's desired configuration: an existing row
/// (<see cref="Id"/> set) or a newly added one (<see cref="Id"/> <c>null</c>).
/// </summary>
public sealed record SpecificationInput(Guid? Id, string Name, string Value, int Position);
