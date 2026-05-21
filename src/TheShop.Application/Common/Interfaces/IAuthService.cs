using TheShop.Application.Common.Models;

namespace TheShop.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result> SendSignUpOtpAsync(string email, CancellationToken ct);

    Task<Result> SendSignInOtpAsync(string email, CancellationToken ct);

    Task<Result<AuthSession>> VerifyOtpAsync(string email, string code, CancellationToken ct);

    Task SignOutAsync(CancellationToken ct);

    /// <summary>
    /// The currently-known session (rehydrated from local storage on app start),
    /// or <c>null</c> if no user is signed in.
    /// </summary>
    AuthSession? CurrentSession { get; }

    /// <summary>
    /// Fires whenever the underlying auth state changes — sign-in, sign-out, token
    /// refresh, or expiry. The Web layer's <c>AuthenticationStateProvider</c>
    /// subscribes to this to re-publish the <c>ClaimsPrincipal</c>.
    /// </summary>
    event Action? AuthStateChanged;
}

public sealed record AuthSession(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
