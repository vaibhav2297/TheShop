namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The min/max bounds of the price filter, derived from published products' effective prices.
/// </summary>
public sealed record PriceRangeDto(decimal Min, decimal Max);
