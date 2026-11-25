using Blazored.LocalStorage;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace TheShop.Infrastructure.Auth;

/// <summary>
/// Persists the Supabase Gotrue session to browser local storage so that it survives
/// page refreshes. Injected into <see cref="SupabaseClientFactory"/> as the session handler.
/// </summary>
public sealed class LocalStorageSessionPersistence(ISyncLocalStorageService storage)
    : IGotrueSessionPersistence<Session>
{
    private const string SessionKey = "shop.auth.session";

    public void SaveSession(Session session) =>
        storage.SetItem(SessionKey, session);

    public void DestroySession() =>
        storage.RemoveItem(SessionKey);

    public Session? LoadSession() =>
        storage.ContainKey(SessionKey)
            ? storage.GetItem<Session>(SessionKey)
            : null;
}
