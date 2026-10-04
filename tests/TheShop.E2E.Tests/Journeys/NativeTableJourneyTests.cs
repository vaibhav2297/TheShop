using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeTableJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, true)]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    [InlineData(1440, false)]
    public async Task Table_FigmaGeometryOverflowAndFocus_WorkWithoutVendorCss(int width, bool withoutVendorCss)
    {
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        var html = await RenderSpecimenAsync();
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                document.body.style.margin = '0';
                const host = document.createElement('main');
                host.id = 'table-probe';
                host.style.cssText = 'display:grid;grid-template-columns:minmax(0,1fr);padding:24px;max-width:900px;background:white';
                host.innerHTML = args.html;
                document.body.appendChild(host);
                await document.fonts.load('400 14px "Space Grotesk"');
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, new { html, withoutVendorCss });
        var table = Page.Locator("#table-probe table");
        var header = table.Locator("th").First;
        await Assertions.Expect(header).ToHaveCSSAsync("padding", "10px 12px");
        await Assertions.Expect(header).ToHaveCSSAsync("font-size", "14px");
        await Assertions.Expect(header).ToHaveCSSAsync("font-weight", "500");
        await Assertions.Expect(header).ToHaveCSSAsync("background-color", "rgb(232, 232, 232)");
        var border = await header.EvaluateAsync<double>("el => parseFloat(getComputedStyle(el).borderTopWidth)");
        border.Should().BeInRange(0.5, 1.01, "Chromium snaps the authored 1px stroke to physical pixels at fractional display scaling");
        await Assertions.Expect(table).ToHaveCSSAsync("border-collapse", "separate");
        await Assertions.Expect(table).ToHaveCSSAsync("border-spacing", "0px");
        await Assertions.Expect(header).ToHaveCSSAsync("background-clip", "padding-box");
        foreach (var cell in await table.Locator("tbody td").AllAsync())
        {
            await Assertions.Expect(cell).ToHaveCSSAsync("border-top-width", "0px");
            (await cell.EvaluateAsync<double>("el => parseFloat(getComputedStyle(el).borderBottomWidth)"))
                .Should().BeApproximately(border, 0.01);
        }
        foreach (var cell in await table.Locator("tr > :not(:last-child)").AllAsync())
            await Assertions.Expect(cell).ToHaveCSSAsync("border-right-width", "0px");
        foreach (var cell in await table.Locator(".shop-table-column-compact").AllAsync())
        {
            await Assertions.Expect(cell).ToHaveCSSAsync("white-space", "nowrap");
            (await cell.BoundingBoxAsync())!.Width.Should().BeLessThan(100);
        }
        await Assertions.Expect(table.Locator("tbody td").First).ToHaveCSSAsync("font-weight", "400");
        (await header.BoundingBoxAsync())!.Height.Should().BeApproximately(39, 2);
        var scroll = Page.Locator("#table-probe .shop-table-scroll");
        await scroll.FocusAsync();
        await Assertions.Expect(scroll).ToHaveCSSAsync("outline-style", "solid");
        if (width == 390)
        {
            (await scroll.EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth")).Should().BeTrue();
            await Page.Keyboard.PressAsync("End");
            await scroll.EvaluateAsync("el => el.scrollLeft = el.scrollWidth");
            (await scroll.EvaluateAsync<double>("el => el.scrollLeft")).Should().BeGreaterThan(0);
        }
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        await SaveAsync($"table-{width}-{withoutVendorCss}");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await Assertions.Expect(header).ToHaveCSSAsync("border-style", "solid");
        await SaveAsync($"table-forced-{width}-{withoutVendorCss}");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.None });
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        await SaveAsync($"table-zoom-{width}-{withoutVendorCss}");
    }

    [Fact]
    public async Task Brands_RealBlazorSelection_HandlesMixedKeyboardAndPageResetWithoutBackendWrites()
    {
        const string userId = "db8d270f-b94e-4a29-bda1-cf95646d1456";
        const string email = "table-probe@example.test";
        var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Encode(new { alg = "HS256", typ = "JWT" }) + "." + Encode(new
        {
            sub = userId,
            email,
            exp = expiry,
            aud = "authenticated",
            role = "authenticated",
            perms = new[] { "brands.view", "brands.edit", "brands.delete", "brands.create" },
            app_roles = new[] { "Admin" }
        }) + ".dGVzdA";
        var session = JsonSerializer.Serialize(new
        {
            access_token = token,
            refresh_token = "test-only-not-a-real-token",
            token_type = "bearer",
            expires_in = 3600,
            expires_at = expiry,
            user = new { id = userId, email, aud = "authenticated", role = "authenticated" }
        });
        var unexpected = new List<string>();
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            string? body = null;
            var headers = new Dictionary<string, string>();
            if (path.EndsWith("/auth/v1/otp") && route.Request.Method == "POST") body = "{}";
            else if (path.EndsWith("/rpc/customer_exists") && route.Request.Method == "POST") body = "true";
            else if (path.EndsWith("/auth/v1/verify") && route.Request.Method == "POST") body = session;
            else if (path.EndsWith("/rest/v1/customers") && route.Request.Method == "GET")
                body = JsonSerializer.Serialize(new[] { new { id = userId, email, first_name = "Table", last_name = "Probe", date_of_birth = "1990-01-01", created_at = "2026-01-01T00:00:00Z" } });
            else if (path.EndsWith("/rest/v1/brands") && route.Request.Method == "GET")
            {
                headers["Content-Range"] = "0-1/20";
                headers["Access-Control-Expose-Headers"] = "Content-Range";
                body = """
                    [{"id":"ac09dac2-bc69-4e50-8f1a-49c98a963bc1","name":"Table brand A","description":"Longer description wraps naturally without a fixed row height.","is_active":true,"created_at":"2026-01-01T00:00:00Z"},
                     {"id":"ac09dac2-bc69-4e50-8f1a-49c98a963bc2","name":"Table brand B","description":"Another description","is_active":false,"created_at":"2026-01-01T00:00:00Z"}]
                    """;
            }
            else if (path.EndsWith("/rpc/brand_product_counts") && route.Request.Method == "POST") body = "[]";
            if (body is null)
            {
                unexpected.Add(route.Request.Method + " " + path);
                await route.AbortAsync();
            }
            else await route.FulfillAsync(new() { ContentType = "application/json", Body = body, Headers = headers });
        });
        await Page.SetViewportSizeAsync(1440, 1000);
        await Page.GotoAsync(WebRoutes.Auth.SignIn + "?returnUrl=" + Uri.EscapeDataString(WebRoutes.Admin.ManageBrands));
        await Page.GetByTestId("signin-email").FillAsync(email);
        await Page.GetByTestId("signin-submit").ClickAsync();
        await Page.GetByTestId("otp-input").Locator("input").First.FocusAsync();
        await Page.Keyboard.TypeAsync("123456");
        await Page.GetByTestId("otp-submit").ClickAsync();
        var table = Page.Locator(".shop-table");
        await table.WaitForAsync(new() { Timeout = 30_000 });
        var all = table.GetByRole(AriaRole.Checkbox, new() { Name = Strings.Table_SelectPage, Exact = true });
        var first = table.GetByRole(AriaRole.Checkbox, new() { Name = "Select Table brand A", Exact = true });
        await first.FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(first).ToBeCheckedAsync();
        await Assertions.Expect(all).ToHaveAttributeAsync("aria-checked", "mixed");
        await Assertions.Expect(all).ToHaveJSPropertyAsync("indeterminate", true);
        await all.FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(all).ToBeCheckedAsync();
        await Assertions.Expect(all).ToHaveJSPropertyAsync("indeterminate", false);
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(first).Not.ToBeCheckedAsync();
        await first.CheckAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = string.Format(Strings.Pagination_Page, 2), Exact = true }).ClickAsync();
        await Assertions.Expect(first).Not.ToBeCheckedAsync();
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        unexpected.Should().BeEmpty();
        await SaveAsync("table-brands-live");

        // Exercise the appbar's native activator against the retained account dropdown.
        var account = Page.Locator($".shop-appbar button[aria-label='{Strings.Nav_Account}']");
        await account.ClickAsync();
        await Assertions.Expect(Page.GetByText(Strings.Nav_MyProfile, new() { Exact = true })).ToBeVisibleAsync();
        await account.ClickAsync();
        await Assertions.Expect(Page.GetByText(Strings.Nav_MyProfile, new() { Exact = true })).ToBeHiddenAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddBrand_Heading, Exact = true }).ClickAsync();
        var trail = Page.Locator(".shop-breadcrumbs");
        await Assertions.Expect(trail.Locator("[aria-current='page']")).ToHaveTextAsync(Strings.AddBrand_Heading);
        await Page.SetViewportSizeAsync(390, 900);
        var expand = trail.Locator(".shop-breadcrumb-expand");
        var parent = trail.GetByRole(AriaRole.Link, new() { Name = Strings.ManageBrands_Heading, Exact = true });
        await Assertions.Expect(parent).ToBeHiddenAsync();
        (await expand.BoundingBoxAsync())!.Width.Should().Be(24);
        (await expand.Locator("svg").BoundingBoxAsync())!.Width.Should().Be(18);
        await SaveAsync("shell-breadcrumb-collapsed-live");
        await expand.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(parent).ToBeVisibleAsync();
        await Assertions.Expect(expand).ToHaveCountAsync(0);
        await Assertions.Expect(parent).ToBeFocusedAsync();
        await Assertions.Expect(trail.Locator(".shop-breadcrumb-separator")).ToHaveCountAsync(2);
        await SaveAsync("shell-breadcrumb-expanded-live");
        await parent.ClickAsync();
        await Assertions.Expect(trail.Locator("[aria-current='page']")).ToHaveTextAsync(Strings.ManageBrands_Heading);
        await Assertions.Expect(trail.Locator("button")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        unexpected.Should().BeEmpty();
    }

    private static async Task<string> RenderSpecimenAsync()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<ShopTable<int>>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ShopTable<int>.Items)] = new[] { 1, 2, 3 },
                [nameof(ShopTable<int>.ItemKey)] = (Func<int, object>)(id => id),
                [nameof(ShopTable<int>.Caption)] = Strings.ManageBrands_Heading,
                [nameof(ShopTable<int>.ColumnCount)] = 4,
                [nameof(ShopTable<int>.HeaderContent)] = (RenderFragment)(b => b.AddMarkupContent(0,
                    "<th scope='col'>Name</th><th scope='col'>Description</th><th scope='col' class='shop-table-column-compact'>Status</th><th scope='col' class='shop-table-column-compact'>Actions</th>")),
                [nameof(ShopTable<int>.RowTemplate)] = (RenderFragment<int>)(_ => b => b.AddMarkupContent(0,
                    "<td>Brand</td><td>ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789</td><td class='shop-table-column-compact'>Active</td><td class='shop-table-column-compact'>Details</td>"))
            }));
            return component.ToHtmlString();
        });
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
    }
}
