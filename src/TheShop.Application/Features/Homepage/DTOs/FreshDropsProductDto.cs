namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>A published product card available to the editor.</summary>
public sealed record FreshDropsProductDto(Guid Id, string Name, string BrandName,
    decimal DisplayPrice, string Currency, string? ImageUrl);
