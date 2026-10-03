using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common.Dialogs;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeDialogJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, 844)]
    [InlineData(1440, 900)]
    public async Task Confirmation_NativeBrowserMechanics_PreserveFocusDismissalAndResponsiveLayout(int width, int height)
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
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton<IJSRuntime, StaticRenderJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ShopConfirmDialog>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(ShopConfirmDialog.Options)] = new ShopConfirmationOptions(
                        Strings.ManageBrands_DeleteConfirmTitle,
                        string.Format(Strings.ManageBrands_DeleteConfirmBody, "Example brand"),
                        Strings.ManageBrands_BulkDelete, true)
                }))).ToHtmlString());

        // Real component markup + production ES module. C# dispatch/queue contracts are tested in bUnit.
        await Page.EvaluateAsync("""
            async html => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                // app.css normally hides the template error shell; disabling it must not reveal that shell.
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'dialog-probe';
                host.className = 'shop-native';
                host.innerHTML = '<button id="dialog-trigger" type="button">Open confirmation</button>';
                document.body.appendChild(host);
                const module = await import('/js/shopDialog.js');
                const probe = window.dialogProbe = { results: [], module };
                probe.open = () => {
                    document.getElementById('dialog-trigger').focus();
                    host.insertAdjacentHTML('beforeend', html);
                    const element = host.querySelector('dialog');
                    probe.element = element;
                    const finish = value => {
                        probe.results.push(value);
                        module.dispose(element);
                        element.remove();
                    };
                    element.querySelector('[data-testid="dialog-confirm"]').onclick = () => finish(true);
                    element.querySelector('[data-testid="dialog-cancel"]').onclick = () => finish(false);
                    element.querySelector('[data-testid="dialog-close"]').onclick = () => finish(false);
                    module.show(element, { invokeMethodAsync: async () => finish(false) });
                };
                await document.fonts.load('700 24px "Barlow Condensed"');
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.ready;
                probe.open();
            }
            """, html);

        var dialog = Page.Locator("dialog.shop-dialog");
        var cancel = Page.GetByTestId("dialog-cancel");
        var confirm = Page.GetByTestId("dialog-confirm");
        var trigger = Page.Locator("#dialog-trigger");
        await Assertions.Expect(cancel).ToBeFocusedAsync();
        (await dialog.EvaluateAsync<bool>("el => el.matches(':modal')")).Should().BeTrue();
        await Assertions.Expect(dialog).ToHaveAccessibleNameAsync(Strings.ManageBrands_DeleteConfirmTitle);
        var dimensions = await dialog.EvaluateAsync<double[]>("""
            el => [el.getBoundingClientRect().width, parseFloat(getComputedStyle(el.querySelector('header')).paddingTop),
                parseFloat(getComputedStyle(el.querySelector('h2')).fontSize)]
            """);
        dimensions[0].Should().BeApproximately(Math.Min(500d, width - 32d), 1,
            "Windows fractional scaling can round viewport-relative widths");
        dimensions.Skip(1).Should().Equal(24d, 24d);
        (await Page.GetByTestId("dialog-close").EvaluateAsync<double[]>("el => [el.getBoundingClientRect().width, el.getBoundingClientRect().height]"))
            .Should().Equal([48d, 48d], "the close action uses the Figma Text/Medium icon-button size");
        await trigger.FocusAsync();
        await Assertions.Expect(cancel).ToBeFocusedAsync(); // Modal background is inert.
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(confirm).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Assertions.Expect(cancel).ToBeFocusedAsync();
        await SaveAsync($"confirmation-{width}");
        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(trigger).ToBeFocusedAsync();

        await OpenAsync();
        await Page.Mouse.ClickAsync(2, 2);
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(trigger).ToBeFocusedAsync();

        await OpenAsync();
        await Page.GetByTestId("dialog-close").ClickAsync();
        await Assertions.Expect(trigger).ToBeFocusedAsync();
        await OpenAsync();
        await cancel.ClickAsync();
        await OpenAsync();
        await confirm.ClickAsync();
        (await Page.EvaluateAsync<bool[]>("() => window.dialogProbe.results")).Should().Equal(false, false, false, false, true);

        await OpenAsync();
        await Page.EvaluateAsync("() => window.dialogProbe.element.close()");
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(trigger).ToBeFocusedAsync();

        await OpenAsync();
        await Page.EvaluateAsync("""
            () => {
                document.documentElement.style.fontSize = '32px';
                document.querySelector('.shop-dialog-description').textContent = 'Long confirmation content '.repeat(80);
            }
            """);
        (await dialog.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
        (await dialog.EvaluateAsync<double>("el => el.getBoundingClientRect().height")).Should().BeLessThanOrEqualTo(height);
        var scrollGeometry = await dialog.EvaluateAsync<double[]>("""
            el => {
                const body = el.querySelector('.shop-dialog-content');
                const header = el.querySelector('header');
                const footer = el.querySelector('footer');
                const before = [header.getBoundingClientRect().top, footer.getBoundingClientRect().top];
                body.scrollTop = body.scrollHeight;
                return [body.scrollHeight - body.clientHeight, body.scrollTop, el.scrollTop,
                    header.getBoundingClientRect().top - before[0], footer.getBoundingClientRect().top - before[1],
                    header.getBoundingClientRect().top, footer.getBoundingClientRect().bottom];
            }
            """);
        scrollGeometry[0].Should().BeGreaterThan(0);
        scrollGeometry[1].Should().BeGreaterThan(0);
        scrollGeometry.Skip(2).Take(3).Should().Equal(0d, 0d, 0d);
        scrollGeometry[5].Should().BeGreaterThanOrEqualTo(0);
        scrollGeometry[6].Should().BeLessThanOrEqualTo(height);
        await Page.Locator(".shop-dialog-content").FocusAsync();
        await Page.Keyboard.PressAsync("Control+Home");
        await Assertions.Expect(Page.Locator(".shop-dialog-content")).ToBeFocusedAsync();
        await SaveAsync($"confirmation-scroll-{width}");
        await cancel.ClickAsync();
        await Assertions.Expect(trigger).ToBeFocusedAsync();

        // Removal without an explicit dispose call must still restore focus and release scroll locking.
        await OpenAsync();
        await Page.EvaluateAsync("() => window.dialogProbe.element.remove()");
        await Assertions.Expect(trigger).ToBeFocusedAsync();
        (await Page.EvaluateAsync<string>("() => getComputedStyle(document.documentElement).overflow"))
            .Should().NotBe("hidden");
        backendRequests.Should().Be(0);
    }

    [Theory]
    [InlineData(390)]
    [InlineData(1440)]
    [InlineData(3000)]
    public async Task MaxWidth_AllChoicesRespectCapsAndViewportGutters(int viewportWidth)
    {
        await Page.SetViewportSizeAsync(viewportWidth, 900);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await Page.EvaluateAsync("""
            () => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
            }
            """);
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton<IJSRuntime, StaticRenderJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        (ShopMaxWidth? Width, double Cap)[] cases =
        [
            (null, 500), (ShopMaxWidth.ExtraSmall, 444), (ShopMaxWidth.Small, 600),
            (ShopMaxWidth.Medium, 960), (ShopMaxWidth.Large, 1280), (ShopMaxWidth.ExtraLarge, 1920),
            (ShopMaxWidth.ExtraExtraLarge, 2560), (ShopMaxWidth.None, double.PositiveInfinity)
        ];
        foreach (var (width, cap) in cases)
        {
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<ShopDialog>(ParameterView.FromDictionary(
                    new Dictionary<string, object?>
                    {
                        [nameof(ShopDialog.MaxWidth)] = width,
                        [nameof(ShopDialog.TitleContent)] = (RenderFragment)(builder =>
                        {
                            builder.OpenElement(0, "h2");
                            builder.AddContent(1, "Sizing fixture");
                            builder.CloseElement();
                        }),
                        [nameof(ShopDialog.DialogContent)] = (RenderFragment)(builder => builder.AddContent(0, "Short content"))
                    }))).ToHtmlString());
            var dimensions = await Page.EvaluateAsync<double[]>("""
                async html => {
                    document.body.insertAdjacentHTML('beforeend', html);
                    const el = document.querySelector('dialog.shop-dialog');
                    const closedDisplay = getComputedStyle(el).display;
                    const module = await import('/js/shopDialog.js');
                    module.show(el, { invokeMethodAsync: async () => {} });
                    const rect = el.getBoundingClientRect();
                    const result = [rect.width, rect.left, innerWidth - rect.right, rect.height,
                        closedDisplay === 'none' ? 1 : 0];
                    module.dispose(el);
                    el.remove();
                    return result;
                }
                """, html);
            dimensions[0].Should().BeApproximately(Math.Min(cap, viewportWidth - 32d), 1, $"width choice {width}");
            dimensions[1].Should().BeGreaterThanOrEqualTo(15);
            dimensions[2].Should().BeGreaterThanOrEqualTo(15);
            dimensions[3].Should().BeLessThan(450, "short content must not stretch to viewport height");
            dimensions[4].Should().Be(1, "flex layout must not expose closed native dialogs");
        }
    }

    private Task OpenAsync() => Page.EvaluateAsync("() => window.dialogProbe.open()");

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }

    private sealed class StaticRenderJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not execute browser interop.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not execute browser interop.");
    }
}
