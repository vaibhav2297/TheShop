namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>Immutable storage identity and a signed preview URL that expires after 900 seconds.</summary>
public sealed record HeroBannerImageDto(string ObjectKey, string PreviewUrl);

