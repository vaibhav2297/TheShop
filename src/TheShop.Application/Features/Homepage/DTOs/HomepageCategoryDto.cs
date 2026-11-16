using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>Saved admin configuration, with existing selected category details.</summary>
public sealed record HomepageCategoryDto(string Title, string ButtonLabel, string ButtonLink,
    IReadOnlyList<Guid> CategoryIds, IReadOnlyList<CategoryDto> Categories, bool IsVisible);
