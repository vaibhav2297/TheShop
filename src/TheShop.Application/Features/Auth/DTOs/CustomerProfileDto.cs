namespace TheShop.Application.Features.Auth.DTOs;

public sealed record CustomerProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth);
