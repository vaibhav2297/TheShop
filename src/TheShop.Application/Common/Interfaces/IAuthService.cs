using TheShop.Application.Common.Models;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Auth provider contract. Abstracts Supabase Gotrue so the Application layer stays
/// SDK-free. Implementations live in the Infrastructure layer.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Sends a sign-up OTP to <paramref name="email"/>, creating a Supabase auth user
    /// if one does not yet exist.
    /// </summary>
    /// <returns><see cref="Result.Ok"/> on success, or <see cref="Result.Fail"/> with an
    /// <see cref="AuthErrorKeys"/> key on failure.</returns>
    Task<Result> SendSignUpOtpAsync(string email, CancellationToken ct);

    /// <summary>
    /// Sends a sign-in OTP to <paramref name="email"/>. Does not create a new auth user.
    /// </summary>
    /// <returns><see cref="Result.Ok"/> on success, or <see cref="Result.Fail"/> with an
    /// <see cref="AuthErrorKeys"/> key on failure.</returns>
    Task<Result> SendSignInOtpAsync(string email, CancellationToken ct);

    /// <summary>
    /// Verifies a one-time passcode and, on success, establishes a session.
    /// </summary>
    /// <param name="code">The six-digit OTP entered by the user.</param>
    /// <returns>The new <see cref="AuthSession"/> on success, or a failure result with
    /// an <see cref="AuthErrorKeys"/> key.</returns>
    Task<Result<AuthSession>> VerifyOtpAsync(string email, string code, CancellationToken ct);

    /// <summary>
    /// Terminates the current session. Best-effort: a network failure is silently swallowed
    /// because the local session is always discarded regardless.
    /// </summary>
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

/// <summary>
/// SDK-agnostic representation of an active Supabase auth session.
/// </summary>
public sealed record AuthSession(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
