using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>Saved Fresh Drops settings shown to staff.</summary>
public sealed record FreshDropsDto(string Title, string? ButtonLabel, string? ButtonLink,
    FreshDropsSelectionMode SelectionMode, IReadOnlyList<Guid> ManualProductIds, bool IsVisible);
