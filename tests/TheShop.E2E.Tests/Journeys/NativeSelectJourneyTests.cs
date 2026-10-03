using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common.Sorting;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeSelectJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390)]
    [InlineData(1440)]
    public async Task Select_FigmaAndBrowserMechanics_WorkWithoutVendorCss(int width)
    {
        var backendRequests = 0;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            Interlocked.Increment(ref backendRequests);
            await route.AbortAsync();
        });
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        var html = await RenderSpecimensAsync();
        await Page.EvaluateAsync("""
            async html => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'select-probe';
                host.className = 'shop-native';
                host.style.cssText = 'display:grid;gap:32px;padding:32px;max-width:364px;background:white';
                host.innerHTML = html + '<button id="select-after" type="button">After select</button>';
                document.body.appendChild(host);
                const module = await import('/js/shopSelect.js');
                window.selectProbe = { module, calls: [] };
                for (const root of host.querySelectorAll('.shop-select')) {
                    module.initialize(root, { invokeMethodAsync: async (method, id) => window.selectProbe.calls.push({method, id}) });
                }
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.load('500 16px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, html);
        var fields = Page.Locator("#select-probe .shop-select");
        var empty = fields.Nth(0).GetByRole(AriaRole.Combobox);
        var selected = fields.Nth(1).GetByRole(AriaRole.Combobox);
        var disabled = fields.Nth(2).GetByRole(AriaRole.Combobox);
        await Assertions.Expect(empty).ToHaveAccessibleNameAsync("Placeholder");
        (await empty.BoundingBoxAsync())!.Height.Should().BeApproximately(61, 0.1f);
        (await empty.BoundingBoxAsync())!.Width.Should().BeApproximately(Math.Min(300, width - 64), 0.1f);
        await Assertions.Expect(fields.Nth(0).Locator("label")).ToHaveCSSAsync("font-size", "16px");
        await Assertions.Expect(fields.Nth(1).Locator("label")).ToHaveCSSAsync("font-size", "12px");
        await Assertions.Expect(fields.Nth(1).Locator(".shop-select-value")).ToHaveTextAsync("Selection 1");
        await Assertions.Expect(disabled).ToBeDisabledAsync();
        await Assertions.Expect(Page.Locator("#select-probe input, #select-probe textarea, #select-probe [contenteditable]")).ToHaveCountAsync(0);

        await selected.ClickAsync();
        var list = fields.Nth(1).Locator("[role='listbox']");
        await Assertions.Expect(list).ToBeVisibleAsync();
        await Assertions.Expect(list).ToHaveCSSAsync("border-top-width", "0px");
        await Assertions.Expect(list).ToHaveCSSAsync("padding-left", "1px");
        await Assertions.Expect(selected).ToHaveAttributeAsync("aria-expanded", "true");
        var options = list.Locator("[role='option']");
        (await options.Nth(0).BoundingBoxAsync())!.Height.Should().BeApproximately(44, 0.1f);
        await Assertions.Expect(options.Nth(0)).ToHaveCSSAsync("background-color", "rgb(245, 245, 245)");
        await Assertions.Expect(options.Nth(0)).ToHaveCSSAsync("font-weight", "500");
        (await options.Nth(0).Locator("svg").BoundingBoxAsync())!.Width.Should().BeApproximately(18, 0.1f);
        await Assertions.Expect(options.Nth(1).Locator("svg")).ToHaveCSSAsync("visibility", "hidden");
        var triggerBox = (await selected.BoundingBoxAsync())!;
        var listBox = (await list.BoundingBoxAsync())!;
        listBox.Width.Should().BeApproximately(triggerBox.Width, 0.1f);
        listBox.Y.Should().BeApproximately(triggerBox.Y + triggerBox.Height, 0.1f);
        await SaveAsync($"select-{width}-figma");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(selected).ToHaveAttributeAsync("aria-activedescendant", (await options.Nth(1).GetAttributeAsync("id"))!);
        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(list).ToBeHiddenAsync();
        await Assertions.Expect(selected).ToBeFocusedAsync();
        (await Page.EvaluateAsync<int>("() => window.selectProbe.calls.length")).Should().Be(0);
        await Page.Keyboard.PressAsync("Space");
        await Page.Keyboard.PressAsync("End");
        await Assertions.Expect(selected).ToHaveAttributeAsync("aria-activedescendant", (await options.Nth(3).GetAttributeAsync("id"))!);
        await Page.Keyboard.PressAsync("Home");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown"); // Skip disabled third option.
        await Assertions.Expect(selected).ToHaveAttributeAsync("aria-activedescendant", (await options.Nth(3).GetAttributeAsync("id"))!);
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(list).ToBeHiddenAsync();
        (await Page.EvaluateAsync<string>("() => window.selectProbe.calls.at(-1).id")).Should().Be(await options.Nth(3).GetAttributeAsync("id"));
        await selected.ClickAsync();
        await Page.Keyboard.PressAsync("s");
        await Assertions.Expect(selected).ToHaveAttributeAsync("aria-activedescendant", (await options.Nth(1).GetAttributeAsync("id"))!);
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Page.Locator("#select-after")).ToBeFocusedAsync();
        await Assertions.Expect(list).ToBeHiddenAsync();
        await selected.ClickAsync();
        await Page.Mouse.ClickAsync(4, 4);
        await Assertions.Expect(list).ToBeHiddenAsync();
        await selected.ClickAsync();
        await options.Nth(1).ClickAsync();
        await Assertions.Expect(selected).ToBeFocusedAsync();
        await Assertions.Expect(list).ToBeHiddenAsync();

        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce, ForcedColors = ForcedColors.Active });
        await selected.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(selected).ToHaveCSSAsync("outline-style", "solid");
        await SaveAsync($"select-{width}-forced-colors");
        await Page.Keyboard.PressAsync("Escape");
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce, ForcedColors = ForcedColors.None });
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '200%'");
        await Assertions.Expect(fields.Nth(1).Locator("label")).ToHaveCSSAsync("transition-duration", "0s");
        await selected.ClickAsync();
        (await list.BoundingBoxAsync())!.X.Should().BeGreaterThanOrEqualTo(0);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        await SaveAsync($"select-{width}-large-text");
        await Page.Keyboard.PressAsync("Escape");
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = ''");
        await Page.SetViewportSizeAsync(width, 300);
        await selected.ClickAsync();
        await Assertions.Expect(list).ToHaveAttributeAsync("data-placement", "above");
        var flipped = (await list.BoundingBoxAsync())!;
        (flipped.Y + flipped.Height).Should().BeApproximately((await selected.BoundingBoxAsync())!.Y, 0.1f);
        await Page.Keyboard.PressAsync("End");
        await Assertions.Expect(list).ToBeVisibleAsync();
        (await list.EvaluateAsync<bool>("el => el.scrollTop > 0")).Should().BeTrue();
        await Page.EvaluateAsync("""
            () => {
                const root = document.querySelectorAll('#select-probe .shop-select')[1];
                window.selectProbe.module.dispose(root);
            }
            """);
        await Assertions.Expect(list).ToBeHiddenAsync();
        await selected.ClickAsync();
        await Assertions.Expect(list).ToBeHiddenAsync();
        backendRequests.Should().Be(0);
    }

    [Fact]
    public async Task Sort_LiveBlazorSelection_UpdatesTypedValueQueryAndBrowserHistory()
    {
        List<string> requests = [];
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path.EndsWith("/rest/v1/products", StringComparison.Ordinal) && route.Request.Method == "GET")
            {
                requests.Add(route.Request.Url);
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Headers = new Dictionary<string, string>
                    {
                        ["Content-Range"] = "0-0/1",
                        ["Access-Control-Expose-Headers"] = "Content-Range"
                    },
                    Body = """
                        [{"id":"c596b652-a878-43de-bfe7-7b1f876978e7","name":"Select test product","description":"","sku":"SELECT-TEST","original_price":20,"currency":"CAD","is_published":true,"created_at":"2026-01-01T00:00:00Z","category_id":"aeb9e3d7-df19-49aa-9c24-519563ecdf8b","brand_id":"0d69781e-7314-4207-84e9-a64027b3f73d","categories":{"id":"aeb9e3d7-df19-49aa-9c24-519563ecdf8b","name":"Test category"},"brands":{"id":"0d69781e-7314-4207-84e9-a64027b3f73d","name":"Test brand"}}]
                        """
                });
            }
            else if (path.EndsWith("/rpc/get_catalogue_filters", StringComparison.Ordinal))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "{\"categories\":[],\"brands\":[],\"option_types\":[],\"price_min\":0,\"price_max\":100}" });
            else
                await route.AbortAsync();
        });
        await Page.GotoAsync(WebRoutes.Products);
        var select = Page.GetByRole(AriaRole.Combobox, new() { Name = Strings.Sort_Label });
        await select.WaitForAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(select).ToContainTextAsync(Strings.Sort_Newest);
        await select.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = Strings.Sort_PriceLowHigh, Exact = true }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"sort={SortSlugs.PriceAsc}"));
        await Assertions.Expect(select).ToContainTextAsync(Strings.Sort_PriceLowHigh);
        await select.FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"sort={SortSlugs.NameDesc}"));
        await Assertions.Expect(select).ToContainTextAsync(Strings.Sort_NameZA);
        await Page.GoBackAsync();
        await Assertions.Expect(select).ToContainTextAsync(Strings.Sort_PriceLowHigh);
        requests.Should().Contain(url => Uri.UnescapeDataString(url).Contains("display_price.asc", StringComparison.Ordinal));
        requests.Should().Contain(url => Uri.UnescapeDataString(url).Contains("name.desc", StringComparison.Ordinal));
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    private static async Task<string> RenderSpecimensAsync()
    {
        await using var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, StaticJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var html = new System.Text.StringBuilder();
            string? value = null;
            (string? Value, bool Disabled)[] states = [(null, false), ("one", false), ("one", true)];
            foreach (var state in states)
            {
                var component = await renderer.RenderComponentAsync<ShopSelect<string?>>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ShopSelect<string?>.Label)] = "Placeholder",
                    [nameof(ShopSelect<string?>.Value)] = state.Value,
                    [nameof(ShopSelect<string?>.Disabled)] = state.Disabled,
                    [nameof(ShopSelect<string?>.ValueExpression)] = (Expression<Func<string?>>)(() => value),
                    [nameof(ShopSelect<string?>.Options)] = (ShopSelectOption<string?>[])
                    [
                        new("one", "Selection 1"), new("two", "Selection 2"), new("three", "Selection 3", true), new("four", "Selection 4")
                    ]
                }));
                html.Append(component.ToHtmlString());
            }
            return html.ToString();
        });
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }

    private sealed class StaticJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static rendering must not execute browser interop.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new InvalidOperationException("Static rendering must not execute browser interop.");
    }
}
