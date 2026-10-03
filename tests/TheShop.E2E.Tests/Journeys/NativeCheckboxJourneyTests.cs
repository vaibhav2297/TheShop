using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeCheckboxJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private int _backendRequests;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            Interlocked.Increment(ref _backendRequests);
            await route.AbortAsync();
        });
    }

    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    public async Task Checkbox_FigmaSizesAndNativeStates_WorkWithAndWithoutVendorStyles(int width, bool withoutVendorCss)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await Page.EvaluateAsync("""
            async () => {
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.load('500 16px "Space Grotesk"');
                await document.fonts.ready;
            }
            """);
        var html = await RenderSpecimensAsync();
        await Page.EvaluateAsync("""
            args => {
                if (args.withoutVendorCss) {
                    for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                    }
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'checkbox-probe';
                host.style.cssText = 'display:grid;gap:16px;padding:24px;background:white';
                host.innerHTML = args.html;
                document.body.appendChild(host);
            }
            """, new { html, withoutVendorCss });

        var fields = Page.Locator("#checkbox-probe .shop-checkbox");
        var ids = await fields.Locator("input").EvaluateAllAsync<string[]>("nodes => nodes.map(n => n.id)");
        ids.Should().OnlyHaveUniqueItems();
        int[] iconSizes = [20, 24, 36];
        for (var size = 0; size < 3; size++)
        {
            for (var state = 0; state < 2; state++)
            {
                var field = fields.Nth(size * 2 + state);
                var input = field.Locator("input");
                var label = field.Locator(".shop-checkbox-text");
                var icon = field.Locator("svg:visible");
                await Assertions.Expect(input).ToHaveAccessibleNameAsync("Mops");
                await Assertions.Expect(input).ToBeCheckedAsync(new() { Checked = state == 0 });
                (await field.Locator(".shop-checkbox-control").BoundingBoxAsync())!.Width.Should().BeApproximately(iconSizes[size] + 24, 0.1f);
                (await field.Locator(".shop-checkbox-control").BoundingBoxAsync())!.Height.Should().BeApproximately(iconSizes[size] + 24, 0.1f);
                (await icon.BoundingBoxAsync())!.Width.Should().BeApproximately(iconSizes[size], 0.1f);
                await Assertions.Expect(icon).ToHaveCSSAsync("color", state == 0 ? "rgb(23, 23, 23)" : "rgb(122, 122, 122)");
                await Assertions.Expect(icon.Locator("path")).ToHaveCSSAsync("vector-effect", "non-scaling-stroke");
                await Assertions.Expect(label).ToHaveCSSAsync("font-size", "16px");
                await Assertions.Expect(label).ToHaveCSSAsync("font-weight", state == 0 ? "500" : "400");
                await Assertions.Expect(label).ToHaveCSSAsync("color", "rgb(23, 23, 23)");
                await Assertions.Expect(label).ToHaveCSSAsync("letter-spacing", "0.25px");
                var controlBox = (await field.Locator(".shop-checkbox-control").BoundingBoxAsync())!;
                (await label.BoundingBoxAsync())!.X.Should().BeApproximately(controlBox.X + controlBox.Width, 0.1f);
            }
        }
        await SaveAsync($"checkboxes-{width}-figma");

        var first = fields.Nth(0);
        var firstInput = first.Locator("input");
        await first.Locator(".shop-checkbox-text").ClickAsync();
        await Assertions.Expect(firstInput).Not.ToBeCheckedAsync();
        await Assertions.Expect(firstInput).ToBeFocusedAsync();
        await Assertions.Expect(first.Locator("svg:visible")).ToHaveCSSAsync("outline-style", "none");
        var iconBox = (await first.Locator("svg:visible").BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync(iconBox.X + iconBox.Width / 2, iconBox.Y + iconBox.Height / 2);
        await Assertions.Expect(firstInput).ToBeCheckedAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(fields.Nth(1).Locator("input")).ToBeFocusedAsync();
        await Assertions.Expect(fields.Nth(1).Locator("svg:visible")).ToHaveCSSAsync("outline-style", "solid");
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(fields.Nth(1).Locator("input")).ToBeCheckedAsync();
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(fields.Nth(1).Locator("input")).Not.ToBeCheckedAsync();
        await SaveAsync($"checkboxes-{width}-keyboard");

        for (var i = 6; i < 8; i++)
        {
            await Assertions.Expect(fields.Nth(i).Locator("input")).ToBeDisabledAsync();
            var disabledLabel = fields.Nth(i).Locator(".shop-checkbox-text");
            await disabledLabel.ScrollIntoViewIfNeededAsync();
            var disabledBox = (await disabledLabel.BoundingBoxAsync())!;
            await Page.Mouse.ClickAsync(disabledBox.X + disabledBox.Width / 2, disabledBox.Y + disabledBox.Height / 2);
            await Assertions.Expect(fields.Nth(i).Locator("input")).ToBeCheckedAsync(new() { Checked = i == 6 });
            await Assertions.Expect(fields.Nth(i).Locator("svg:visible")).ToHaveCSSAsync("color", "rgb(176, 176, 176)");
        }
        await fields.Nth(5).Locator("input").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(fields.Nth(8).Locator("input")).ToBeFocusedAsync();

        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce, ForcedColors = ForcedColors.Active });
        await Assertions.Expect(fields.Nth(8).Locator("input")).ToHaveCSSAsync("opacity", "1");
        await Assertions.Expect(fields.Nth(8).Locator("input")).ToHaveCSSAsync("appearance", "auto");
        await Assertions.Expect(fields.Nth(8).Locator("input")).ToHaveCSSAsync("outline-style", "solid");
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(fields.Nth(8).Locator("input")).ToBeCheckedAsync();
        await SaveAsync($"checkboxes-{width}-forced-colors");
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce, ForcedColors = ForcedColors.None });
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '200%'");
        await Assertions.Expect(fields.Nth(8).Locator(".shop-checkbox-text")).ToHaveCSSAsync("font-size", "32px");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth")).Should().BeTrue();
        await SaveAsync($"checkboxes-{width}-large-text");
        _backendRequests.Should().Be(0);
    }

    private static async Task<string> RenderSpecimensAsync()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var value = false;
            var html = new System.Text.StringBuilder();
            bool[] states = [true, false];
            foreach (var size in Enum.GetValues<ShopSize>())
            {
                foreach (var selected in states)
                    await AddAsync(size, selected, false, "Mops");
            }
            await AddAsync(ShopSize.Medium, true, true, Strings.SignUp_AgeConfirm);
            await AddAsync(ShopSize.Medium, false, true, Strings.SignUp_AgeConfirm);
            await AddAsync(ShopSize.Medium, false, false, string.Join(' ', Enumerable.Repeat(Strings.SignUp_AgeConfirm, 3)));
            return html.ToString();

            async Task AddAsync(ShopSize size, bool selected, bool disabled, string label)
            {
                var component = await renderer.RenderComponentAsync<ShopCheckbox>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ShopCheckbox.Label)] = label,
                    [nameof(ShopCheckbox.Size)] = size,
                    [nameof(ShopCheckbox.Value)] = selected,
                    [nameof(ShopCheckbox.Disabled)] = disabled,
                    [nameof(ShopCheckbox.ValueExpression)] = (Expression<Func<bool>>)(() => value)
                }));
                html.Append(component.ToHtmlString());
            }
        });
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.Locator("#checkbox-probe").ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}
