namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>Admin-only banner projection with temporary private image URLs.</summary>
public sealed record HeroBannerDto(string Heading, string? Description, string? ButtonLabel,
    string? ButtonLink, bool IsVisible, HeroBannerImageDto DesktopImage, HeroBannerImageDto? MobileImage);

