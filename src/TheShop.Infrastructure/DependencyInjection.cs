using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using TheShop.Application.Common.Interfaces;
using TheShop.Infrastructure.Auth;
using TheShop.Infrastructure.Persistence;
using TheShop.Infrastructure.Persistence.Repositories;

namespace TheShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IGotrueSessionPersistence<Session>, LocalStorageSessionPersistence>();

        services.AddScoped(sp =>
        {
            var persistence = sp.GetRequiredService<IGotrueSessionPersistence<Session>>();
            return SupabaseClientFactory.Build(configuration, persistence);
        });

        services.AddScoped<IAuthService, SupabaseAuthService>();
        services.AddScoped<ICustomerRepository, SupabaseCustomerRepository>();

        return services;
    }

    /// <summary>
    /// Restores the persisted auth session before the first render.
    /// Per gotrue-csharp docs, session restoration requires two explicit steps:
    /// LoadSession (sync, reads from localStorage) then RetrieveSessionAsync
    /// (async, validates the token and refreshes it if it has expired).
    /// Call this once at app start from <c>Program.cs</c> after <c>host.Build()</c>.
    /// </summary>
    public static async Task InitializeInfrastructureAsync(this IServiceProvider services)
    {
        var supabase = services.GetRequiredService<Supabase.Client>();
        supabase.Auth.LoadSession();
        await supabase.Auth.RetrieveSessionAsync();
    }
}
