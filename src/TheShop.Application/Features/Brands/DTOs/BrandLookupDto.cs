namespace TheShop.Application.Features.Brands.DTOs;

/// <summary>
/// A minimal <c>{ id, name }</c> projection of an Active brand, for picker controls (e.g. the
/// create/edit product form's brand select — AC-23).
/// </summary>
public sealed record BrandLookupDto(Guid Id, string Name);
