namespace TheShop.Application.Features.Auth.DTOs;

/// <summary>
/// Returned after successfully dispatching an OTP. Carries the target email and
/// how many seconds the UI should lock the "Resend" button before allowing another request.
/// </summary>
public sealed record OtpRequestedDto(string Email, int ResendCooldownSeconds);
