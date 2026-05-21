using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth;
using Constants = Supabase.Gotrue.Constants;

namespace TheShop.Infrastructure.Auth;

/// <summary>
/// Thin adapter over Supabase's Gotrue client. The Application layer never sees
/// the Supabase SDK — all SDK types stay inside this file.
/// </summary>
public sealed class SupabaseAuthService : IAuthService
{
    private readonly Supabase.Client _client;

    public SupabaseAuthService(Supabase.Client client)
    {
        _client = client;
        _client.Auth.AddStateChangedListener(OnAuthStateChanged);
    }

    public event Action? AuthStateChanged;

    public AuthSession? CurrentSession => ToAuthSession(_client.Auth.CurrentSession);

    public async Task<Result> SendSignUpOtpAsync(string email, CancellationToken ct)
    {
        try
        {
            await _client.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email)
            {
                ShouldCreateUser = true,
            });
            return Result.Ok();
        }
        catch (GotrueException ex)
        {
            return Result.Fail(MapGotrueError(ex));
        }
    }

    public async Task<Result> SendSignInOtpAsync(string email, CancellationToken ct)
    {
        try
        {
            await _client.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email)
            {
                ShouldCreateUser = false,
            });
            return Result.Ok();
        }
        catch (GotrueException ex)
        {
            return Result.Fail(MapGotrueError(ex));
        }
    }

    public async Task<Result<AuthSession>> VerifyOtpAsync(string email, string code, CancellationToken ct)
    {
        try
        {
            var session = await _client.Auth.VerifyOTP(email, code, Constants.EmailOtpType.Email);
            var mapped = ToAuthSession(session);
            return mapped is null
                ? Result.Fail<AuthSession>(AuthErrorKeys.CodeIncorrect)
                : Result.Ok(mapped);
        }
        catch (GotrueException ex)
        {
            return Result.Fail<AuthSession>(MapGotrueError(ex));
        }
    }

    public async Task SignOutAsync(CancellationToken ct)
    {
        try
        {
            await _client.Auth.SignOut();
        }
        catch (GotrueException)
        {
            // Best-effort sign-out; the local session has already been discarded by the caller.
        }
    }

    private void OnAuthStateChanged(IGotrueClient<User, Session> sender, Constants.AuthState state)
    {
        AuthStateChanged?.Invoke();
    }

    private static AuthSession? ToAuthSession(Session? session)
    {
        if (session?.AccessToken is null || session.User is null)
            return null;

        if (!Guid.TryParse(session.User.Id, out var userId))
            return null;

        return new AuthSession(
            userId,
            session.User.Email ?? string.Empty,
            session.AccessToken,
            session.RefreshToken ?? string.Empty,
            session.ExpiresAt());
    }

    private static string MapGotrueError(GotrueException ex)
    {
        return ex.Reason switch
        {
            FailureHint.Reason.UserAlreadyRegistered => AuthErrorKeys.AccountAlreadyExists,
            FailureHint.Reason.UserTooManyRequests => AuthErrorKeys.ResendTooSoon,
            FailureHint.Reason.UserBadEmailAddress => AuthErrorKeys.EmailInvalid,
            FailureHint.Reason.UserBadLogin => AuthErrorKeys.CodeIncorrect,
            FailureHint.Reason.UserMissingInformation => AuthErrorKeys.CodeIncorrect,
            FailureHint.Reason.ExpiredRefreshToken => AuthErrorKeys.CodeExpired,
            FailureHint.Reason.InvalidRefreshToken => AuthErrorKeys.CodeExpired,
            _ when LooksLikeExpiredOtp(ex) => AuthErrorKeys.CodeExpired,
            _ when LooksLikeRateLimited(ex) => AuthErrorKeys.TooManyAttempts,
            _ => AuthErrorKeys.CodeIncorrect,
        };
    }

    private static bool LooksLikeExpiredOtp(GotrueException ex) =>
        ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
        (ex.Content?.Contains("expired", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool LooksLikeRateLimited(GotrueException ex) =>
        (int?)ex.StatusCode == 429 ||
        ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
}
