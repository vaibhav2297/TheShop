using Microsoft.Extensions.DependencyInjection;
using TheShop.Web.State;
using TheShop.Web.Theme;

namespace TheShop.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.AddSingleton<ShopTheme>();
        services.AddScoped<CartState>();
        services.AddScoped<AuthState>();

        return services;
    }
}
