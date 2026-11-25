using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TheShop.Application;
using TheShop.Infrastructure;
using TheShop.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// TEMP diagnostic — remove after env overlay is verified.
Console.WriteLine($"[ENV-CHECK] HostEnvironment.Environment = '{builder.HostEnvironment.Environment}'");
Console.WriteLine($"[ENV-CHECK] Supabase:Url = '{builder.Configuration["Supabase:Url"]}'");
Console.WriteLine($"[ENV-CHECK] Supabase:PublishableKey starts with = '{builder.Configuration["Supabase:PublishableKey"]?[..Math.Min(20, builder.Configuration["Supabase:PublishableKey"]?.Length ?? 0)]}'");

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

var host = builder.Build();

await host.Services.InitializeInfrastructureAsync();

await host.RunAsync();
