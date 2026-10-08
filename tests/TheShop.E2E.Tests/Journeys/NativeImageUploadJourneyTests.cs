using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeImageUploadJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    public async Task Upload_PickDropReorderRemoveAndReselect_WorkInTheLiveComponent(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await SignInLocallyAsync();
        await Page.SetViewportSizeAsync(width, 1000);
        var upload = Page.Locator(".shop-image-upload");
        await upload.WaitForAsync(new() { Timeout = 30000 });
        if (withoutVendorCss)
        {
            await Page.EvaluateAsync("""
                () => {
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                """);
        }
        // Isolate the real mounted control without replacing its Blazor handlers or state.
        await upload.EvaluateAsync("""
            el => { el.style.cssText = 'position:fixed;inset:24px auto auto 50%;transform:translateX(-50%);width:min(448px,calc(100vw - 48px));z-index:10000;background:white;padding:0'; }
            """);
        await Page.EvaluateAsync("""
            () => {
                window.uploadUrls = {created: [], revoked: []};
                const create = URL.createObjectURL.bind(URL), revoke = URL.revokeObjectURL.bind(URL);
                URL.createObjectURL = value => { const url = create(value); window.uploadUrls.created.push(url); return url; };
                URL.revokeObjectURL = url => { window.uploadUrls.revoked.push(url); revoke(url); };
            }
            """);
        await SaveAsync(upload, $"upload-empty-{width}");
        // Keyboard activation must open the native picker, not submit the surrounding form.
        await upload.Locator(".shop-file-dropzone-trigger").FocusAsync();
        var chooserTask = Page.WaitForFileChooserAsync();
        await Page.Keyboard.PressAsync("Enter");
        var chooser = await chooserTask;
        await chooser.SetFilesAsync(new[] { await PngAsync("first.png"), await PngAsync("second.png"), await PngAsync("third.png") });
        var items = upload.Locator(".shop-image-upload-item");
        await Assertions.Expect(items).ToHaveCountAsync(3);
        await Assertions.Expect(upload.Locator(".shop-image-tile-loader")).ToHaveCountAsync(0);
        await Assertions.Expect(upload.Locator("input[type=file]")).ToHaveValueAsync("");
        await Assertions.Expect(items.First.Locator(".shop-image-tile-primary")).ToHaveTextAsync(Strings.AddProduct_ImagePrimary);
        var boxes = await items.EvaluateAllAsync<double[][]>("nodes => nodes.map(el => { const r=el.getBoundingClientRect(); return [r.x,r.y,r.width,r.height]; })");
        boxes[0][2].Should().BeApproximately(boxes[1][2] * 2 + 8, 1);
        boxes[0][3].Should().BeApproximately(boxes[0][2], 1);
        boxes[1][0].Should().BeApproximately(boxes[2][0], 1);
        await SaveAsync(upload, $"upload-grid-{width}");

        var last = items.Nth(2).Locator(".shop-image-tile-surface");
        await last.FocusAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowLeft");
        await Assertions.Expect(items.Nth(1).Locator(".shop-image-tile-surface")).ToHaveAttributeAsync("aria-label", "third.png");
        await Assertions.Expect(items.Nth(1).Locator(".shop-image-tile-surface")).ToBeFocusedAsync();
        await items.Nth(1).Locator(".shop-image-tile-surface").DragToAsync(items.First.Locator(".shop-image-tile-surface"));
        await Assertions.Expect(items.First.Locator(".shop-image-tile-surface")).ToHaveAttributeAsync("aria-label", "third.png");

        await Page.Mouse.MoveAsync(0, 0);
        await Page.EvaluateAsync("document.activeElement?.blur()");
        await Assertions.Expect(items.First.Locator(".shop-image-tile-remove")).ToHaveCSSAsync("opacity", "0");
        await items.First.HoverAsync();
        await Assertions.Expect(items.First.Locator(".shop-image-tile-remove")).ToHaveCSSAsync("opacity", "1");
        await items.First.Locator(".shop-image-tile-remove").ClickAsync();
        await Assertions.Expect(items).ToHaveCountAsync(2);
        (await Page.EvaluateAsync<int>("window.uploadUrls.revoked.length")).Should().Be(1);
        await SaveAsync(upload, $"upload-focus-{width}");

        // Real external file drop travels through the native InputFile change event.
        await upload.Locator(".shop-file-dropzone").EvaluateAsync("""
            el => {
                const data = new DataTransfer();
                data.items.add(new File(['bad'], 'invalid.txt', {type:'text/plain'}));
                el.dispatchEvent(new DragEvent('dragenter', {bubbles:true, dataTransfer:data}));
                el.dispatchEvent(new DragEvent('drop', {bubbles:true, cancelable:true, dataTransfer:data}));
            }
            """);
        await Assertions.Expect(items).ToHaveCountAsync(3);
        await Assertions.Expect(items.Last.Locator(".shop-image-tile-error")).ToBeVisibleAsync();
        await Assertions.Expect(items.Last.Locator(".shop-image-tile-remove")).ToHaveCSSAsync("opacity", "1");
        await SaveAsync(upload, $"upload-error-{width}");
        (await Page.EvaluateAsync<int>("window.uploadUrls.created.length")).Should().Be(3, "rejected files must not allocate previews");

        await items.Last.Locator(".shop-image-tile-remove").ClickAsync();
        await upload.Locator("input[type=file]").SetInputFilesAsync(await PngAsync("first.png"));
        await Assertions.Expect(items).ToHaveCountAsync(3);
        while (await items.CountAsync() > 0)
        {
            await items.First.HoverAsync();
            await items.First.Locator(".shop-image-tile-remove").ClickAsync();
        }
        await Assertions.Expect(upload.Locator(".shop-file-dropzone-trigger")).ToBeVisibleAsync();
        (await Page.EvaluateAsync<int>("window.uploadUrls.revoked.length")).Should().Be(4);
        await upload.Locator("input[type=file]").SetInputFilesAsync(await PngAsync("first.png"));
        await Assertions.Expect(items).ToHaveCountAsync(1);
        await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
        (await upload.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active, ReducedMotion = ReducedMotion.Reduce });
        await items.First.Locator(".shop-image-tile-surface").FocusAsync();
        await SaveAsync(upload, $"upload-accessibility-{width}");
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Touch_RemoveIsVisibleWithoutHover_AndKeyboardActionsAreAvailable()
    {
        await using var context = await ShopBrowser.NewContextAsync(Playwright.Browser, options: new()
        {
            HasTouch = true,
            IsMobile = true,
            ViewportSize = new() { Width = 390, Height = 1000 }
        });
        var page = await context.NewPageAsync();
        await SignInLocallyAsync(page, context);
        var upload = page.Locator(".shop-image-upload");
        await upload.WaitForAsync();
        await upload.Locator("input[type=file]").SetInputFilesAsync(new[] { await PngAsync("first.png"), await PngAsync("second.png") });
        var items = upload.Locator(".shop-image-upload-item");
        await Assertions.Expect(items).ToHaveCountAsync(2);
        await Assertions.Expect(upload.Locator(".shop-image-tile-loader")).ToHaveCountAsync(0);
        await Assertions.Expect(items.First.Locator(".shop-image-tile-remove")).ToHaveCSSAsync("opacity", "1");
        await items.Nth(1).Locator(".shop-image-tile-surface").TapAsync();
        await upload.GetByRole(AriaRole.Button, new() { Name = Strings.ImageUpload_Earlier }).TapAsync();
        await Assertions.Expect(items.First.Locator(".shop-image-tile-surface")).ToHaveAttributeAsync("aria-label", "second.png");
        (await upload.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue("the uploader must fit the real mobile form column");
        await SaveAsync(upload, "upload-touch-390");
        await items.First.Locator(".shop-image-tile-remove").TapAsync();
        await Assertions.Expect(items).ToHaveCountAsync(1);
    }

    private async Task SignInLocallyAsync(IPage? page = null, IBrowserContext? context = null)
    {
        page ??= Page;
        context ??= Context;
        const string userId = "db8d270f-b94e-4a29-bda1-cf95646d1457";
        const string email = "upload-probe@example.test";
        var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Encode(new { alg = "HS256", typ = "JWT" }) + "." + Encode(new
        {
            sub = userId,
            email,
            exp = expiry,
            aud = "authenticated",
            role = "authenticated",
            perms = new[] { "products.create", "products.view", "brands.view", "categories.view" },
            app_roles = new[] { "Admin" }
        }) + ".dGVzdA";
        var session = JsonSerializer.Serialize(new { access_token = token, refresh_token = "test-only-not-a-real-token", token_type = "bearer", expires_in = 3600, expires_at = expiry, user = new { id = userId, email, aud = "authenticated", role = "authenticated" } });
        await context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            string? body = null;
            if (path.EndsWith("/auth/v1/otp")) body = "{}";
            else if (path.EndsWith("/rpc/customer_exists")) body = "true";
            else if (path.EndsWith("/auth/v1/verify")) body = session;
            else if (path.EndsWith("/rest/v1/customers")) body = JsonSerializer.Serialize(new[] { new { id = userId, email, first_name = "Upload", last_name = "Probe", date_of_birth = "1990-01-01", created_at = "2026-01-01T00:00:00Z" } });
            else if (route.Request.Method == "GET" && path.Contains("/rest/v1/")) body = "[]";
            if (body is not null) await route.FulfillAsync(new() { ContentType = "application/json", Body = body });
            else await route.AbortAsync();
        });
        await page.GotoAsync(WebRoutes.Auth.SignIn + "?returnUrl=" + Uri.EscapeDataString(WebRoutes.Admin.AddProduct));
        await page.GetByTestId("signin-email").FillAsync(email);
        await page.GetByTestId("signin-submit").ClickAsync();
        await page.GetByTestId("otp-input").Locator("input").First.FocusAsync();
        await page.Keyboard.TypeAsync("123456");
        var dismiss = page.GetByRole(AriaRole.Button, new() { Name = Strings.Notification_Dismiss });
        if (await dismiss.CountAsync() > 0) await dismiss.First.ClickAsync();
        await page.GetByTestId("otp-submit").ClickAsync();
    }

    private async Task<FilePayload> PngAsync(string name)
    {
        // Transparent, non-square fixtures make containment and empty space visible.
        var encoded = await Page.EvaluateAsync<string>("""
            name => {
                const canvas = document.createElement('canvas');
                canvas.width = name === 'second.png' ? 320 : 160;
                canvas.height = name === 'second.png' ? 160 : 320;
                const ctx = canvas.getContext('2d');
                ctx.fillStyle = name === 'third.png' ? '#396754' : '#5e4773';
                ctx.fillRect(24, 40, canvas.width - 48, canvas.height - 64);
                ctx.fillStyle = '#29272c';
                ctx.fillRect(canvas.width / 2 - 20, 8, 40, 32);
                ctx.fillStyle = '#ffffff';
                ctx.fillRect(40, canvas.height / 2, canvas.width - 80, 16);
                return canvas.toDataURL('image/png').split(',')[1];
            }
            """, name);
        return new() { Name = name, MimeType = "image/png", Buffer = Convert.FromBase64String(encoded) };
    }

    private Task SaveAsync(ILocator target, string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return target.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}
