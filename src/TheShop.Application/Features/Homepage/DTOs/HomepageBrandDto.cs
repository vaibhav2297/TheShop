using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>Saved admin configuration with selected brand details, including inactive brands.</summary>
public sealed record HomepageBrandDto(string Title, string ButtonLabel, string ButtonLink,
    IReadOnlyList<Guid> BrandIds, IReadOnlyList<BrandDto> Brands, bool IsVisible);
