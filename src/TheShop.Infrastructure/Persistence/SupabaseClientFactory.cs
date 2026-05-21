using Microsoft.Extensions.Configuration;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace TheShop.Infrastructure.Persistence;

/// <summary>
/// Constructs and configures the singleton <see cref="Supabase.Client"/> used across
/// the Infrastructure layer.
/// </summary>
public static class SupabaseClientFactory
{
    /// <summary>
    /// Builds a <see cref="Supabase.Client"/> from <c>Supabase:Url</c> and
    /// <c>Supabase:AnonKey</c> in configuration, wiring the provided
    /// <paramref name="sessionPersistence"/> as the session handler.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when either configuration key is missing or empty.
    /// </exception>
    public static Supabase.Client Build(
        IConfiguration configuration,
        IGotrueSessionPersistence<Session> sessionPersistence)
    {
        var url = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url is not configured.");
        var anonKey = configuration["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Supabase:AnonKey is not configured.");

        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false,
            SessionHandler = new SessionHandlerAdapter(sessionPersistence),
        };

        return new Supabase.Client(url, anonKey, options);
    }

    private sealed class SessionHandlerAdapter : IGotrueSessionPersistence<Session>
    {
        private readonly IGotrueSessionPersistence<Session> _inner;

        public SessionHandlerAdapter(IGotrueSessionPersistence<Session> inner)
        {
            _inner = inner;
        }

        public void SaveSession(Session session) => _inner.SaveSession(session);
        public void DestroySession() => _inner.DestroySession();
        public Session? LoadSession() => _inner.LoadSession();
    }
}
