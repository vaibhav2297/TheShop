using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TheShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: Register Supabase client (SupabaseProductRepository, etc.)
        // TODO: Register StripePaymentService : IPaymentService
        // TODO: Register ResendEmailSender  : IEmailSender
        // TODO: Register SupabaseAuthService : IAuthService

        return services;
    }
}
