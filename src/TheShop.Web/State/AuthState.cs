namespace TheShop.Web.State;

/// <summary>
/// Client-side state store for the authenticated user session.
/// Populated after a successful Supabase auth flow.
/// </summary>
public class AuthState
{
    public bool IsAuthenticated { get; private set; }
    public string? UserId       { get; private set; }
    public string? Email        { get; private set; }

    public event Action? OnChange;

    public void SetUser(string userId, string email)
    {
        UserId          = userId;
        Email           = email;
        IsAuthenticated = true;
        NotifyStateChanged();
    }

    public void Clear()
    {
        UserId          = null;
        Email           = null;
        IsAuthenticated = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
