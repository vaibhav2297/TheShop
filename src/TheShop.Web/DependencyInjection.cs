using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using TheShop.Application.Common.Interfaces;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Dialogs;
using TheShop.Web.Common.Notifications;
using TheShop.Web.State;

namespace TheShop.Web;

/// <summary>
/// Registers all Web-layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds localization, native UI services, all scoped UI state stores, Blazored
    /// LocalStorage, the Supabase-backed authentication services, and the claims-based RBAC
    /// authorization (on-demand <c>perm:{code}</c> policies via
    /// <see cref="ShopAuthorizationPolicyProvider"/>, reading the permission claims minted into
    /// the access token) needed by the presentation layer.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddLocalization();

        services.AddScoped<BusyState>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ShopNotificationService>();
        services.AddScoped<IShopNotificationService>(provider => provider.GetRequiredService<ShopNotificationService>());
        services.AddScoped<ShopDialogService>();
        services.AddScoped<IShopDialogService>(provider => provider.GetRequiredService<ShopDialogService>());
        services.AddScoped<CartState>();
        services.AddScoped<AuthState>();
        services.AddScoped<AnnouncementState>();
        services.AddScoped<PendingSignUpState>();
        services.AddScoped<BreadcrumbState>();
        services.AddScoped<FooterState>();

        services.AddBlazoredLocalStorage();

        services.AddAuthorizationCore();

        // perm:{code} policies are synthesized on demand from the permission claims in the
        // access token — no per-catalogue-entry registration, no authorization handlers. The
        // admin shell gate (PolicyNames.AdminDashboard) is one of these ordinary permission
        // policies, on dashboard.view — no statically registered policy exists.
        services.AddSingleton<IAuthorizationPolicyProvider, ShopAuthorizationPolicyProvider>();

        services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
        services.AddScoped<ICurrentUserService, BlazorCurrentUserService>();

        return services;
    }
}
