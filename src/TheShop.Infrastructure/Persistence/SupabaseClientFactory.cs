using Microsoft.Extensions.Configuration;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace TheShop.Infrastructure.Persistence;

public static class SupabaseClientFactory
{
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
