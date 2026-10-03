using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.Application.Features.Products.DTOs;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeVariantImageJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, 844)]
    [InlineData(1440, 900)]
    public async Task Picker_NativeMarkupWithoutVendorCss_PreservesKeyboardGeometryAndDismissal(int width, int height)
    {
        var backendRequests = 0;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            Interlocked.Increment(ref backendRequests);
            await route.AbortAsync();
        });
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton<IJSRuntime, StaticRenderJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var images = Enumerable.Range(0, 3).Select(index =>
            new ProductImageDto(Guid.NewGuid(), string.Empty, index, index == 0)).ToArray();
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<VariantImageDialog>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(VariantImageDialog.VariantLabel)] = "Red - 15 mg",
                [nameof(VariantImageDialog.GalleryImages)] = images,
                [nameof(VariantImageDialog.CurrentImageId)] = images[0].Id,
                [nameof(VariantImageDialog.SharedScopeLabel)] = "Color = Red",
                [nameof(VariantImageDialog.SharedScopeCount)] = 2
            }))).ToHtmlString());

        // Static component markup and production modal module verify browser mechanics.
        // bUnit verifies actual Blazor selection, completion, and caller state changes.
        await Page.EvaluateAsync("""
            async html => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.className = 'shop-native';
                host.innerHTML = '<button type="button" id="picker-trigger">Open picker</button>';
                document.body.appendChild(host);
                const module = await import('/js/shopDialog.js');
                const probe = window.pickerProbe = { module, dismissed: 0, activated: 0 };
                probe.open = () => {
                    document.getElementById('picker-trigger').focus();
                    host.insertAdjacentHTML('beforeend', html);
                    const dialog = host.querySelector('dialog');
                    const finish = () => {
                        probe.dismissed++;
                        module.dispose(dialog);
                        dialog.remove();
                    };
                    dialog.querySelector('[data-testid="variant-image-cancel"]').onclick = finish;
                    dialog.querySelector('[data-testid="dialog-close"]').onclick = finish;
                    for (const option of dialog.querySelectorAll('.shop-variant-image-option'))
                        option.addEventListener('click', () => probe.activated++);
                    module.show(dialog, { invokeMethodAsync: async () => finish() });
                };
                await document.fonts.load('700 24px "Barlow Condensed"');
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.ready;
                probe.open();
            }
            """, html);

        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = Strings.VariantImage_Title });
        var options = dialog.Locator(".shop-variant-image-option");
        await Assertions.Expect(Page.GetByTestId("variant-image-cancel")).ToBeFocusedAsync();
        await Assertions.Expect(options).ToHaveCountAsync(3);
        await Assertions.Expect(options.First).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(options.First).ToHaveAccessibleNameAsync(string.Format(Strings.VariantImage_ImageLabel, 1, "Red - 15 mg"));
        var metrics = await dialog.EvaluateAsync<double[]>("""
            el => {
                const tile = el.querySelector('.shop-variant-image-option').getBoundingClientRect();
                return [el.getBoundingClientRect().width, tile.width, tile.height,
                    parseFloat(getComputedStyle(el.querySelector('.shop-variant-image')).gap),
                    parseFloat(getComputedStyle(el.querySelector('.shop-variant-image-gallery')).gap)];
            }
            """);
        metrics[0].Should().BeApproximately(Math.Min(600d, width - 32d), 1,
            "the picker explicitly selects ShopMaxWidth.Small");
        metrics.Skip(1).Should().Equal(120d, 120d, 32d, 16d);
        foreach (var option in await options.AllAsync())
        {
            var imageGeometry = await option.EvaluateAsync<double[]>("""
                el => {
                    const tile = el.getBoundingClientRect();
                    const image = el.querySelector('.shop-image').getBoundingClientRect();
                    return [image.width, image.height, image.left - tile.left, image.top - tile.top];
                }
                """);
            imageGeometry.Should().Equal(100d, 100d, 10d, 10d);
            await Assertions.Expect(option).ToHaveCSSAsync("background-color", "rgb(232, 232, 232)");
            await Assertions.Expect(option).ToHaveCSSAsync("border-top-width", "0px");
        }
        var selectedIcon = options.First.Locator(".shop-variant-image-selected-icon");
        var iconGeometry = await selectedIcon.EvaluateAsync<double[]>("""
            el => {
                const icon = el.getBoundingClientRect();
                const tile = el.parentElement.getBoundingClientRect();
                return [icon.width, icon.height, icon.top - tile.top, tile.right - icon.right];
            }
            """);
        iconGeometry.Should().Equal(18d, 18d, 3d, 3d);
        await Assertions.Expect(selectedIcon).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0)");
        await Assertions.Expect(selectedIcon).ToHaveAttributeAsync("aria-hidden", "true");
        (string Selector, string Padding)[] sectionPaddings =
        [
            (".shop-dialog-header", "16px"), (".shop-dialog-content", "24px"), (".shop-dialog-actions", "16px")
        ];
        string[] sides = ["top", "right", "bottom", "left"];
        foreach (var (selector, padding) in sectionPaddings)
            foreach (var side in sides)
                await Assertions.Expect(dialog.Locator(selector)).ToHaveCSSAsync($"padding-{side}", padding);
        (await options.First.EvaluateAsync<bool>("""
            el => {
                const style = getComputedStyle(el, '::after');
                const border = parseFloat(style.borderTopWidth);
                return border > 0 && Math.abs(border - 1) <= 1 / devicePixelRatio + 0.01
                    && style.borderTopColor === 'rgb(23, 23, 23)' && style.boxSizing === 'border-box'
                    && ['top', 'right', 'bottom', 'left'].every(side => style[side] === '0px');
            }
            """)).Should().BeTrue("the 1px selected border must survive fractional display scaling without changing image dimensions");
        (await options.Nth(1).EvaluateAsync<string>("el => getComputedStyle(el, '::after').content")).Should().Be("none");
        (await dialog.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
        await options.First.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Page.Keyboard.PressAsync("Space");
        (await Page.EvaluateAsync<int>("() => window.pickerProbe.activated")).Should().Be(2);
        var thisVariant = dialog.GetByRole(AriaRole.Checkbox, new() { Name = Strings.VariantImage_ThisVariantOnly });
        var allVariants = dialog.GetByTestId("variant-image-scope-all");
        await Assertions.Expect(dialog.GetByRole(AriaRole.Radio)).ToHaveCountAsync(0);
        await Assertions.Expect(dialog.GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(2);
        await Assertions.Expect(thisVariant).ToBeCheckedAsync();
        await Assertions.Expect(allVariants).Not.ToBeCheckedAsync();
        foreach (var checkbox in await dialog.Locator(".shop-checkbox").AllAsync())
        {
            (await checkbox.Locator(".shop-checkbox-control").BoundingBoxAsync())!.Width.Should().BeApproximately(48, 0.1f);
            (await checkbox.Locator("svg:visible").BoundingBoxAsync())!.Width.Should().BeApproximately(24, 0.1f);
            await Assertions.Expect(checkbox.Locator(".shop-checkbox-text")).ToHaveCSSAsync("font-size", "16px");
        }
        await thisVariant.FocusAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Assertions.Expect(allVariants).ToBeFocusedAsync();
        await Assertions.Expect(dialog.Locator(".shop-checkbox").First.Locator("svg:visible")).ToHaveCSSAsync("outline-style", "solid");
        // Native Space toggling is browser-owned; bUnit covers the live Blazor scope exclusivity.
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(allVariants).ToBeCheckedAsync();
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(allVariants).Not.ToBeCheckedAsync();
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"variant-image-{width}.png") });
        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("#picker-trigger")).ToBeFocusedAsync();
        await Page.EvaluateAsync("() => window.pickerProbe.open()");
        await Page.Mouse.ClickAsync(2, 2);
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Page.EvaluateAsync("() => window.pickerProbe.open()");
        await Page.GetByTestId("dialog-close").ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);

        // A new modal must dismiss the old owner, not merely hide its still-live picker.
        await Page.EvaluateAsync("""
            () => {
                window.pickerProbe.open();
                const replacement = document.createElement('dialog');
                replacement.id = 'replacement-dialog';
                document.body.appendChild(replacement);
                window.pickerProbe.module.show(replacement, { invokeMethodAsync: async () => {} });
            }
            """);
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<int>("() => window.pickerProbe.dismissed")).Should().Be(4);
        await Page.EvaluateAsync("""
            () => {
                const el = document.getElementById('replacement-dialog');
                window.pickerProbe.module.dispose(el);
                el.remove();
                window.pickerProbe.open();
                document.documentElement.style.fontSize = '32px';
                document.querySelector('.shop-variant-image-name').textContent = 'Long variant name '.repeat(30);
            }
            """);
        (await dialog.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1 && el.getBoundingClientRect().height <= innerHeight")).Should().BeTrue();
        await Page.GetByTestId("variant-image-cancel").ClickAsync();
        await Assertions.Expect(Page.Locator("#picker-trigger")).ToBeFocusedAsync();
        backendRequests.Should().Be(0);
    }

    private sealed class StaticRenderJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not execute browser interop.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not execute browser interop.");
    }
}
