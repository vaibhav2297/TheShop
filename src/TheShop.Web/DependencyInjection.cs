using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TheShop.Application.Common.Interfaces;
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
    /// LocalStorage, and the Supabase-backed authentication services needed by the
    /// presentation layer.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddMudServices();
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

        services.AddAuthorizationCore();
        services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
        services.AddScoped<ICurrentUserService, BlazorCurrentUserService>();

        return services;
    }
}
