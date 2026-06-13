using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TheShop.Application.Common.Interfaces;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.State;
using TheShop.Web.Theme;

namespace TheShop.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddLocalization();

        services.AddSingleton<ShopTheme>();
        services.AddScoped<BusyState>();
        services.AddScoped<CartState>();
        services.AddScoped<AuthState>();
        services.AddScoped<PendingSignUpState>();

        services.AddBlazoredLocalStorage();

        services.AddAuthorizationCore();
        services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
        services.AddScoped<ICurrentUserService, BlazorCurrentUserService>();

        return services;
    }
}
