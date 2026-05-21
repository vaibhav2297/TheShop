namespace TheShop.Application.Features.Auth.DTOs;

public sealed record OtpRequestedDto(string Email, int ResendCooldownSeconds);
