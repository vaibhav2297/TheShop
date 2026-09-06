namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One value of an <see cref="OptionTypeInput"/>: an existing value (<see cref="Id"/> set) or a
/// newly added one (<see cref="Id"/> <c>null</c>).
/// </summary>
public sealed record OptionValueInput(Guid? Id, string Value);
