using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeNotificationJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, 844)]
    [InlineData(1440, 900)]
    public async Task Notifications_RealBlazorFlow_PreservesPauseDismissalExpiryAndNavigation(int width, int height)
    {
        var accountExists = false;
        var otpRequests = 0;
        var unexpectedRequests = 0;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path == "/rest/v1/rpc/customer_exists")
                await route.FulfillAsync(new() { ContentType = "application/json", Body = accountExists ? "true" : "false" });
            else if (path == "/auth/v1/otp")
            {
                Interlocked.Increment(ref otpRequests);
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "{}" });
            }
            else
            {
                Interlocked.Increment(ref unexpectedRequests);
                await route.AbortAsync();
            }
        });
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").FillAsync("notification@example.invalid");
        await Page.GetByTestId("signin-submit").ClickAsync();
        var message = Page.GetByTestId("notification");
        await Assertions.Expect(message).ToContainTextAsync(Strings.Auth_AccountNotFound);
        await Assertions.Expect(Page.Locator(".shop-notification-host")).ToHaveCountAsync(1);
        await Assertions.Expect(Page.Locator(".shop-notification-host")).ToHaveAttributeAsync("aria-live", "polite");
        await Page.BringToFrontAsync();
        await message.HoverAsync();
        (await message.EvaluateAsync<bool>("el => el.matches(':hover')")).Should().BeTrue();
        // Deliberately exceed the production timeout while hovered, then while focused.
        await Page.WaitForTimeoutAsync(5500);
        await Assertions.Expect(message).ToBeVisibleAsync();
        var dismiss = Page.GetByTestId("notification-dismiss");
        await dismiss.FocusAsync();
        await Page.Mouse.MoveAsync(0, 0);
        await Page.WaitForTimeoutAsync(5500);
        await Assertions.Expect(message).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(message).ToHaveCountAsync(0);

        accountExists = true;
        await Page.GetByTestId("signin-submit").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("otp-input")).ToBeVisibleAsync();
        await Assertions.Expect(message).ToContainTextAsync(Strings.Auth_CodeSent);
        await Assertions.Expect(Page.Locator(".shop-notification-host")).ToHaveCountAsync(1);
        // On mobile the new notification can cover the old submit button's cursor position.
        // Expiry is expected only after moving away from that correctly paused notification.
        await Page.Mouse.MoveAsync(0, 0);
        await Assertions.Expect(message).ToHaveCountAsync(0, new() { Timeout = 8000 });
        otpRequests.Should().Be(1, "the OTP response is fulfilled locally, never sent to the backend");
        unexpectedRequests.Should().Be(0);
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(1440, 900)]
    public async Task Notifications_WithoutVendorCss_WrapAllKindsAtLargeText(int width, int height)
    {
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var messages = new List<string>();
        foreach (var kind in Enum.GetValues<ShopNotificationKind>())
        {
            messages.Add(await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<ShopNotification>(ParameterView.FromDictionary(
                    new Dictionary<string, object?>
                    {
                        [nameof(ShopNotification.Message)] = new ShopNotificationMessage(Guid.NewGuid(),
                            "Hello! I am a snackbar", kind)
                    }))).ToHtmlString()));
        }
        await Page.EvaluateAsync("""
            async html => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('div');
                host.className = 'shop-native shop-notification-host';
                host.id = 'notification-probe';
                host.innerHTML = html;
                document.body.appendChild(host);
                await document.fonts.load('400 14px "Space Grotesk"');
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, string.Join("", messages));
        await Assertions.Expect(Page.Locator("#notification-probe script")).ToHaveCountAsync(0);
        var host = Page.Locator("#notification-probe");
        await Assertions.Expect(host).ToHaveCSSAsync("z-index", "1500");
        // Fixed positioning centers within the layout viewport, excluding its scrollbar.
        var viewportWidth = await Page.EvaluateAsync<float>("() => document.documentElement.clientWidth");
        var normalBounds = (await host.BoundingBoxAsync())!;
        (normalBounds.X + normalBounds.Width / 2).Should().BeApproximately(viewportWidth / 2, 1);
        (normalBounds.Y + normalBounds.Height).Should().BeApproximately(height - 16, 1);
        foreach (var bar in await host.GetByTestId("notification").AllAsync())
        {
            await Assertions.Expect(bar).ToHaveAttributeAsync("class", "shop-notification");
            await Assertions.Expect(bar).ToHaveCSSAsync("background-color", "rgb(23, 23, 23)");
            await Assertions.Expect(bar).ToHaveCSSAsync("color", "rgb(255, 255, 255)");
            await Assertions.Expect(bar).ToHaveCSSAsync("padding", "16px");
            await Assertions.Expect(bar).ToHaveCSSAsync("gap", "24px");
            await Assertions.Expect(bar).ToHaveCSSAsync("border-top-width", "0px");
            await Assertions.Expect(bar.Locator(".shop-notification-message")).ToHaveCSSAsync("font-size", "14px");
            await Assertions.Expect(bar.Locator(".shop-notification-message")).ToHaveCSSAsync("font-weight", "500");
            await Assertions.Expect(bar.Locator("svg")).ToHaveCSSAsync("width", "18px");
            var barBounds = (await bar.BoundingBoxAsync())!;
            barBounds.Width.Should().BeApproximately(225, 2);
            barBounds.Height.Should().BeApproximately(50, 1);
            (barBounds.X + barBounds.Width / 2).Should().BeApproximately(viewportWidth / 2, 1);
            var dismissBounds = (await bar.GetByTestId("notification-dismiss").BoundingBoxAsync())!;
            dismissBounds.Width.Should().BeGreaterThanOrEqualTo(24);
            dismissBounds.Height.Should().BeGreaterThanOrEqualTo(24);
        }
        await SaveAsync($"notifications-{width}");
        await Page.EvaluateAsync("""
            () => {
                for (const message of document.querySelectorAll('#notification-probe .shop-notification-message')) {
                    message.textContent = 'Example notification with longer text and <script>plain encoded content</script>.';
                }
                document.documentElement.style.fontSize = '32px';
            }
            """);
        (await host.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
        var bounds = (await host.BoundingBoxAsync())!;
        bounds.X.Should().BeGreaterThanOrEqualTo(0);
        (bounds.X + bounds.Width).Should().BeLessThanOrEqualTo(width);
        (bounds.Y + bounds.Height).Should().BeLessThanOrEqualTo(height);
        await host.GetByTestId("notification-dismiss").Last.FocusAsync();
        await Assertions.Expect(host.GetByTestId("notification-dismiss").Last).ToBeInViewportAsync();
        await Assertions.Expect(host.GetByTestId("notification-dismiss").Last).ToHaveCSSAsync("outline-color", "rgb(255, 255, 255)");
        await SaveAsync($"notifications-large-text-{width}");
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}
