namespace TheShop.Application.Common.Filtering;

/// <summary>
/// The min/max bounds of a range filter (e.g. the catalogue's price range, derived from
/// published products' effective prices).
/// </summary>
public sealed record RangeFilterDto(decimal Min, decimal Max);
