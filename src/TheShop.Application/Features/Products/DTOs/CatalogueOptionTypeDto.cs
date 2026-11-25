namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One option type published by <c>get_catalogue_filters()</c>, aggregated across the option
/// types of every published product. Replaces the retired dedicated Flavour/Nicotine facets
/// (Decision 3) — the product-catalogue feature builds its dynamic filter controls from this.
/// </summary>
public sealed record CatalogueOptionTypeDto(string Name, IReadOnlyList<string> Values);
