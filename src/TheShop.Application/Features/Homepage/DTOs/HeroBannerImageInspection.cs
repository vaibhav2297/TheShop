namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>Actual format and decoded pixel geometry, independent of declared upload metadata.</summary>
public sealed record HeroBannerImageInspection(string? ContentType, int Width, int Height, bool IsDecodable);

