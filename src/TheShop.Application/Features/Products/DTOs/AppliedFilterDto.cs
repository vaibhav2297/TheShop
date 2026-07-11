namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A filter group's selected values, sent back on the page query. <see cref="Key"/> must be
/// one of <see cref="ProductFilterKeys.SelectableKeys"/>.
/// </summary>
public sealed record AppliedFilterDto(string Key, IReadOnlyList<string> Values);
