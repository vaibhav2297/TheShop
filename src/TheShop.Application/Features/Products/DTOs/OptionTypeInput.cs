namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One option type in a create/update command's desired configuration: an existing type
/// (<see cref="Id"/> set) or a newly added one (<see cref="Id"/> <c>null</c>).
/// </summary>
public sealed record OptionTypeInput(Guid? Id, string Name, IReadOnlyList<OptionValueInput> Values);
