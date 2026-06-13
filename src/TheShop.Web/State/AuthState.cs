using TheShop.Application.Common.Interfaces;

namespace TheShop.Web.State;

/// <summary>
/// Client-side mirror of the active auth session. Reads live from
/// <see cref="IAuthService.CurrentSession"/> so it is always accurate —
/// including after a page reload when the session is rehydrated from local storage
/// before the first render.
/// </summary>
public sealed class AuthState : IDisposable
{
    private readonly IAuthService _auth;

    public AuthState(IAuthService auth)
    {
        _auth = auth;
        _auth.AuthStateChanged += OnAuthChanged;
    }

    public bool IsAuthenticated => _auth.CurrentSession is not null;

    public string? UserId => _auth.CurrentSession?.UserId.ToString();

    public string? Email => _auth.CurrentSession?.Email;

    /// <summary>
    /// Fires on every auth state transition: sign-in, sign-out, token refresh, or
    /// session rehydration. Subscribe to re-render components that read this store directly.
    /// </summary>
    public event Action? OnChange;

    private void OnAuthChanged() => OnChange?.Invoke();

    public void Dispose() => _auth.AuthStateChanged -= OnAuthChanged;
}
