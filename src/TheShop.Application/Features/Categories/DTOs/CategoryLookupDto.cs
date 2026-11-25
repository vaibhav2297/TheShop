namespace TheShop.Application.Features.Categories.DTOs;

/// <summary>
/// A minimal <c>{ id, name }</c> projection of an Active category, for picker controls (e.g. the
/// create/edit product form's category select — AC-23).
/// </summary>
public sealed record CategoryLookupDto(Guid Id, string Name);
