using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using MudExtensions.Services;
using TheShop.Application.Common.Interfaces;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.State;
using TheShop.Web.Theme;

namespace TheShop.Web;

/// <summary>
/// Registers all Web-layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds MudBlazor, localization, theming, all scoped UI state stores, Blazored
    /// LocalStorage, the Supabase-backed authentication services, and the claims-based RBAC
    /// authorization (a static <c>AdminArea</c> policy plus on-demand <c>perm:{code}</c>
    /// policies via <see cref="ShopAuthorizationPolicyProvider"/>, both reading the permission
    /// claims minted into the access token) needed by the presentation layer.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddMudExtensions();
        services.AddLocalization();

        services.AddSingleton<ShopTheme>();
        services.AddScoped<BusyState>();
        services.AddScoped<CartState>();
        services.AddScoped<AuthState>();
        services.AddScoped<AnnouncementState>();
        services.AddScoped<PendingSignUpState>();
        services.AddScoped<BreadcrumbState>();
        services.AddScoped<FooterState>();

        services.AddBlazoredLocalStorage();

        services.AddAuthorizationCore(options =>
        {
            // Every catalogue permission is admin-area this release, so "holds at least one
            // permission claim" doubles as "can reach the admin surface".
            options.AddPolicy(PolicyNames.AdminArea, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == ShopClaimTypes.Permission)));
        });

        // perm:{code} policies are synthesized on demand from the permission claims in the
        // access token — no per-catalogue-entry registration, no authorization handlers.
        services.AddSingleton<IAuthorizationPolicyProvider, ShopAuthorizationPolicyProvider>();

        services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
        services.AddScoped<ICurrentUserService, BlazorCurrentUserService>();

        return services;
    }
}
