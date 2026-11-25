namespace TheShop.Application.Features.Auth.DTOs;

/// <summary>
/// Full auth session returned after a successful OTP verification. Combines the Supabase
/// token pair with the linked customer profile so the Web layer can initialise both
/// <c>AuthState</c> and <c>SupabaseAuthStateProvider</c> in a single step.
/// </summary>
public sealed record SessionDto(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    CustomerProfileDto Customer);
