using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.Web.Components.Common;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages;

/// <summary>
/// Test-only composition host for <c>ShopImage</c> presets that have no production placement yet
/// (.specs/shop-image/plan.md TASK-005: "exercise all ten presets in test-only composition; no
/// production demo route"). The real component is rendered to static markup with Blazor's
/// <see cref="HtmlRenderer"/> — no app service is resolved or called — and mounted into a running
/// app page, so the geometry measured is the app's own compiled MudBlazor and shop-image CSS applied
/// to the component's own markup. The JS failure bridge is not attached to mounted frames; failure
/// behaviour is proven on production placements instead.
/// </summary>
public sealed class ShopImageCompositionPage(IPage page) : ShopPage(page)
{
    /// <summary>Host style giving the frame the full viewport width minus a 16px gutter.</summary>
    public const string FullWidthHost =
        "position:absolute;left:0;top:0;width:100%;padding:16px;box-sizing:border-box;z-index:10000;background:#fff";

    // A light page that carries the full app layout and stylesheet but no data grid.
    protected override string Route => WebRoutes.NotFound;

    /// <summary>
    /// Renders one ShopImage with the given parameters and mounts it into a host element with
    /// <paramref name="hostStyle"/>, replacing any earlier host with the same <paramref name="hostId"/>.
    /// </summary>
    public async Task<ShopImageFrame> MountAsync(string hostId, string hostStyle, ShopImagePreset preset, string? src,
        string? mobileSrc = null, string alt = "", string placeholderLabel = "")
    {
        var html = await RenderAsync(new()
        {
            [nameof(ShopImage.Src)] = src,
            [nameof(ShopImage.MobileSrc)] = mobileSrc,
            [nameof(ShopImage.Preset)] = preset,
            [nameof(ShopImage.Alt)] = alt,
            [nameof(ShopImage.PlaceholderLabel)] = placeholderLabel,
        });

        await Page.EvaluateAsync("""
            ([id, style, html]) => {
                document.querySelector(`[data-testid="${id}"]`)?.remove();
                const host = document.createElement('div');
                host.dataset.testid = id;
                host.setAttribute('style', style);
                host.innerHTML = html;
                document.body.appendChild(host);
            }
            """, new object[] { hostId, hostStyle, html });

        return new ShopImageFrame(Host(hostId).Locator(ShopImageFrame.FrameSelector));
    }

    /// <summary>The host element mounted under <paramref name="hostId"/>.</summary>
    public ILocator Host(string hostId) => Page.GetByTestId(hostId);

    /// <summary>Changes the caller allocation of a mounted host, e.g. to simulate a narrower row.</summary>
    public Task ResizeHostAsync(string hostId, string hostStyle) =>
        Host(hostId).EvaluateAsync("(host, style) => host.setAttribute('style', style)", hostStyle);

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IJSRuntime, StaticRenderJSRuntime>()
            .BuildServiceProvider();
        await using var _ = services;
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ShopImage>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    // Static rendering never runs OnAfterRenderAsync, so ShopImage never reaches its JS module.
    private sealed class StaticRenderJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("Static composition renders markup only.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("Static composition renders markup only.");
    }
}
