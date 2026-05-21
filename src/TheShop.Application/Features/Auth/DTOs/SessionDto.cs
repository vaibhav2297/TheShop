namespace TheShop.Application.Features.Auth.DTOs;

public sealed record SessionDto(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    CustomerProfileDto Customer);
