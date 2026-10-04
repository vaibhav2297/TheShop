using System.Text;
using System.Text.RegularExpressions;
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
using TheShop.Web.Theme;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeDisclosureChipPaginationJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, true)]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    [InlineData(1440, false)]
    public async Task Components_FigmaGeometryAndNativeAffordances_WorkWithAndWithoutVendorCss(int width, bool withoutVendorCss)
    {
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(width, 1000);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        var html = await RenderSpecimensAsync();
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss) {
                    for (const link of document.querySelectorAll('link[rel="stylesheet"]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                document.body.style.margin = '0';
                const host = document.createElement('main');
                host.id = 'component-probe';
                host.className = 'shop-native';
                host.style.cssText = 'display:grid;grid-template-columns:minmax(0,1fr);gap:24px;padding:24px;max-width:950px;background:white';
                host.innerHTML = args.html;
                document.body.appendChild(host);
                await document.fonts.load('700 34px "Barlow Condensed"');
                await document.fonts.load('400 12px "Space Grotesk"');
                await document.fonts.load('400 14px "Space Grotesk"');
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, new { html, withoutVendorCss });

        (await Page.EvaluateAsync<bool>("""
            () => [...document.fonts].some(font => font.family.includes('Barlow Condensed') && font.status === 'loaded')
                && [...document.fonts].some(font => font.family.includes('Space Grotesk') && font.status === 'loaded')
            """)).Should().BeTrue("Figma geometry checks require the project's web fonts, not fallback fonts");

        var expanders = Page.Locator("#component-probe .shop-expander");
        (await expanders.Nth(0).BoundingBoxAsync())!.Height.Should().BeApproximately(89, 1);
        (await expanders.Nth(1).BoundingBoxAsync())!.Height.Should().BeApproximately(241, 1);
        await Assertions.Expect(expanders.Nth(0).Locator(".shop-expander-content")).ToBeHiddenAsync();
        await Assertions.Expect(expanders.Nth(1).Locator(".shop-expander-content")).ToBeVisibleAsync();
        await Assertions.Expect(expanders.Nth(0).Locator(".shop-expander-title")).ToHaveCSSAsync("font-size", "34px");
        await Assertions.Expect(expanders.Nth(0).Locator(".shop-expander-icon")).ToHaveCSSAsync("width", "24px");
        await Assertions.Expect(expanders.Nth(0).Locator(".shop-expander-icon")).ToHaveCSSAsync("padding", "3px");
        var trigger = expanders.Nth(0).Locator(".shop-expander-trigger");
        await trigger.FocusAsync();
        await Assertions.Expect(trigger).ToBeFocusedAsync();
        await Assertions.Expect(trigger).ToHaveCSSAsync("outline-style", "solid");
        await Page.Keyboard.PressAsync("Tab");
        (await Page.EvaluateAsync<bool>("() => !document.activeElement.closest('[hidden], [inert]')")).Should().BeTrue();

        var motion = await expanders.First.EvaluateAsync<float[]>("""
            async el => {
                const body = el.querySelector('.shop-expander-content');
                const frame = () => new Promise(requestAnimationFrame);
                const sample = async expanded => {
                    el.classList.toggle('shop-expander-expanded', expanded);
                    await frame(); await frame();
                    const animation = body.getAnimations().find(a => a.transitionProperty === 'grid-template-rows');
                    if (!animation) throw new Error('Expected a height transition');
                    animation.pause();
                    animation.currentTime = 140;
                    const middle = body.getBoundingClientRect().height;
                    animation.finish();
                    await frame();
                    return [middle, body.getBoundingClientRect().height];
                };
                return [...await sample(true), ...await sample(false)];
            }
            """);
        motion[0].Should().BeGreaterThan(0).And.BeLessThan(motion[1]);
        motion[2].Should().BeGreaterThan(0).And.BeLessThan(motion[1]);
        motion[3].Should().BeApproximately(0, 1);

        var chips = Page.Locator("#component-probe .shop-chip");
        int[] heights = [24, 32, 40];
        int[] widths = [65, 79, 98];
        int[] iconSizes = [18, 20, 24];
        for (var size = 0; size < 3; size++)
        {
            for (var selected = 0; selected < 2; selected++)
            {
                var chip = chips.Nth(size * 2 + selected);
                var bounds = (await chip.BoundingBoxAsync())!;
                bounds.Height.Should().BeApproximately(heights[size], 1);
                bounds.Width.Should().BeApproximately(widths[size], 2);
                await Assertions.Expect(chip.Locator("svg")).ToHaveCSSAsync("width", iconSizes[size] + "px");
                var shadow = await chip.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow");
                shadow.Should().Contain(selected == 1 ? "rgb(23, 23, 23)" : "rgb(224, 224, 224)");
            }
        }
        var toggle = Page.Locator("#chip-toggle");
        await toggle.EvaluateAsync("el => { window.chipActivations = 0; el.addEventListener('click', () => window.chipActivations++); }");
        await toggle.FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        await Page.Keyboard.PressAsync("Enter");
        (await Page.EvaluateAsync<int>("() => window.chipActivations")).Should().Be(2);
        await Assertions.Expect(Page.Locator("#chip-disabled")).ToBeDisabledAsync();

        var navigations = Page.Locator("#component-probe .shop-pagination");
        string[] expected = ["1,2,3,4,5", "1,2,3,4,5,20", "1,9,10,11,20", "1,16,17,18,19,20"];
        for (var index = 0; index < 4; index++)
        {
            var nav = navigations.Nth(index);
            var labels = await nav.Locator("button:not(.shop-pagination-arrow)").AllTextContentsAsync();
            string.Join(",", labels.Select(x => x.Trim())).Should().Be(expected[index]);
            await Assertions.Expect(nav.Locator("[aria-current='page']")).ToHaveCountAsync(1);
            await Assertions.Expect(nav.Locator(".shop-pagination-control").First).ToHaveCSSAsync("min-width", "64px");
            await Assertions.Expect(nav.Locator(".shop-pagination-arrow").First).ToHaveCSSAsync("height", "44px");
            await Assertions.Expect(nav.Locator("button:not(.shop-pagination-arrow)").First).ToHaveCSSAsync("height", "40px");
            await Assertions.Expect(nav.Locator(".shop-pagination-items")).ToHaveCSSAsync("gap", "6px");
            foreach (var ellipsis in await nav.Locator(".shop-pagination-ellipsis").AllAsync())
            {
                await Assertions.Expect(ellipsis).ToHaveTextAsync("");
                await Assertions.Expect(ellipsis).ToHaveCSSAsync("width", "64px");
                await Assertions.Expect(ellipsis).ToHaveCSSAsync("height", "40px");
                var icon = ellipsis.Locator("svg.shop-pagination-icon");
                await Assertions.Expect(icon).ToHaveCSSAsync("width", "22px");
                await Assertions.Expect(icon).ToHaveCSSAsync("height", "22px");
                await Assertions.Expect(icon).ToHaveCSSAsync("color", "rgb(122, 122, 122)");
                await Assertions.Expect(icon).ToHaveAttributeAsync("aria-hidden", "true");
            }
            if (width == 1440)
                (await nav.Locator("ul").BoundingBoxAsync())!.Height.Should().BeApproximately(44, 1);
        }
        var overflow = await Page.EvaluateAsync<string>("""
            () => JSON.stringify([...document.querySelectorAll('#component-probe, #component-probe *')]
                .filter(el => el.getBoundingClientRect().right > innerWidth)
                .map(el => ({ name: el.className.baseVal ?? el.className, width: el.getBoundingClientRect().width,
                    right: el.getBoundingClientRect().right })).slice(0, 12))
            """);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue(overflow);
        await SaveAsync($"components-{width}-{withoutVendorCss}");

        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active, ReducedMotion = ReducedMotion.Reduce });
        await Assertions.Expect(expanders.First.Locator(".shop-expander-content")).ToHaveCSSAsync("transition-duration", "0s");
        await Assertions.Expect(toggle).ToHaveCSSAsync("box-shadow", "none");
        await Assertions.Expect(navigations.First.Locator("[aria-current]")).ToHaveCSSAsync("outline-style", "solid");
        await SaveAsync($"components-forced-colors-{width}-{withoutVendorCss}");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.None });
        await Page.EvaluateAsync("""
            () => {
                document.documentElement.style.fontSize = '32px';
                document.querySelector('.shop-expander-title').textContent = 'Long heading that wraps on a narrow screen';
                document.querySelector('.shop-chip-label').textContent = 'A longer chip label that must wrap';
            }
            """);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1")).Should().BeTrue();
        await SaveAsync($"components-large-text-{width}-{withoutVendorCss}");
    }

    [Theory]
    [InlineData(390)]
    [InlineData(1440)]
    public async Task Catalogue_RealBlazorDisclosureFiltersAndPagination_PreserveUrlAndHistory(int width)
    {
        var unexpectedRequests = 0;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path.EndsWith("/rest/v1/products", StringComparison.Ordinal) && route.Request.Method == "GET")
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Headers = new Dictionary<string, string>
                    {
                        ["Content-Range"] = "0-0/240",
                        ["Access-Control-Expose-Headers"] = "Content-Range"
                    },
                    Body = """
                        [{"id":"c596b652-a878-43de-bfe7-7b1f876978e7","name":"Component test product","description":"","sku":"COMPONENT-TEST","original_price":20,"currency":"CAD","is_published":true,"created_at":"2026-01-01T00:00:00Z","category_id":"aeb9e3d7-df19-49aa-9c24-519563ecdf8b","brand_id":"0d69781e-7314-4207-84e9-a64027b3f73d","categories":{"id":"aeb9e3d7-df19-49aa-9c24-519563ecdf8b","name":"Test category"},"brands":{"id":"0d69781e-7314-4207-84e9-a64027b3f73d","name":"Test brand"}}]
                        """
                });
            else if (path.EndsWith("/rpc/get_catalogue_filters", StringComparison.Ordinal))
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Body = """
                        {"categories":[{"id":"aeb9e3d7-df19-49aa-9c24-519563ecdf8b","name":"Test category"}],"brands":[{"id":"0d69781e-7314-4207-84e9-a64027b3f73d","name":"Test brand"}],"option_types":[],"price_min":0,"price_max":100}
                        """
                });
            else
            {
                Interlocked.Increment(ref unexpectedRequests);
                await route.AbortAsync();
            }
        });
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.Products);
        var category = Page.Locator(".shop-expander").Filter(new() { HasText = Strings.Filter_Category });
        var trigger = category.Locator(".shop-expander-trigger");
        await trigger.WaitForAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");
        var body = category.Locator(".shop-expander-content");
        await Assertions.Expect(body).ToHaveAttributeAsync("inert", "");
        var groupExpanders = Page.Locator(".shop-filter-groups > .shop-expander");
        var firstShadow = await groupExpanders.First.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow");
        var nextShadow = await groupExpanders.Nth(1).EvaluateAsync<string>("el => getComputedStyle(el).boxShadow");
        Regex.Matches(firstShadow, "inset").Count.Should().Be(2);
        Regex.Matches(nextShadow, "inset").Count.Should().Be(1);
        var titleBox = (await category.Locator(".shop-filter-title").BoundingBoxAsync())!;
        var iconBox = (await category.Locator(".shop-expander-icon").BoundingBoxAsync())!;
        (titleBox.Y + titleBox.Height / 2).Should().BeApproximately(iconBox.Y + iconBox.Height / 2, 1);
        await trigger.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
        var input = category.GetByRole(AriaRole.Checkbox, new() { Name = "Test category" });
        await input.EvaluateAsync("el => window.retainedFilterInput = el");
        await input.CheckAsync();
        await Assertions.Expect(category.Locator(".shop-chip")).ToHaveTextAsync("1");
        var overviewBox = (await category.Locator(".shop-expander-overview").BoundingBoxAsync())!;
        iconBox = (await category.Locator(".shop-expander-icon").BoundingBoxAsync())!;
        (overviewBox.Y + overviewBox.Height / 2).Should().BeApproximately(iconBox.Y + iconBox.Height / 2, 1);
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
        await trigger.FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(body).ToHaveAttributeAsync("inert", "");
        await category.Locator("input").EvaluateAsync("el => el.focus()");
        await Assertions.Expect(trigger).ToBeFocusedAsync();
        await Assertions.Expect(input).ToBeHiddenAsync();
        await Page.Keyboard.PressAsync("Space");
        await Assertions.Expect(input).ToBeCheckedAsync();
        (await input.EvaluateAsync<bool>("el => el === window.retainedFilterInput")).Should().BeTrue();

        var pagination = Page.GetByRole(AriaRole.Navigation, new() { Name = Strings.Pagination_Label });
        await pagination.GetByRole(AriaRole.Button, new() { Name = string.Format(Strings.Pagination_Page, 3), Exact = true }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("page=3"));
        await Assertions.Expect(pagination.Locator("[aria-current]")).ToHaveTextAsync("3");
        await pagination.GetByRole(AriaRole.Button, new() { Name = Strings.Pagination_Next, Exact = true }).FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(pagination.Locator("[aria-current]")).ToHaveTextAsync("4");
        await Page.GoBackAsync();
        await Assertions.Expect(pagination.Locator("[aria-current]")).ToHaveTextAsync("3");
        await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Clear, Exact = true }).ClickAsync();
        await Assertions.Expect(pagination.Locator("[aria-current]")).ToHaveTextAsync("1");
        await Assertions.Expect(input).Not.ToBeCheckedAsync();
        await Assertions.Expect(category.Locator(".shop-chip")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        unexpectedRequests.Should().Be(0);
        await SaveAsync($"components-catalogue-{width}");
    }

    private static async Task<string> RenderSpecimensAsync()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var html = new StringBuilder();
            foreach (var expanded in new[] { false, true })
            {
                var expander = await renderer.RenderComponentAsync<ShopExpander>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ShopExpander.Title)] = "BAG",
                    [nameof(ShopExpander.OverviewContent)] = (RenderFragment)(b => b.AddContent(0, "2 Items")),
                    [nameof(ShopExpander.Expanded)] = expanded,
                    [nameof(ShopExpander.Style)] = "width:605.25px;max-width:100%",
                    [nameof(ShopExpander.ChildContent)] = (RenderFragment)(b => b.AddMarkupContent(0, "<div style='height:120px'><button type='button'>Body action</button></div>"))
                }));
                html.Append(expander.ToHtmlString());
            }
            foreach (var size in Enum.GetValues<ShopSize>())
            {
                html.Append("<div>");
                foreach (var selected in new[] { false, true })
                    html.Append((await renderer.RenderComponentAsync<ShopChip>(ParameterView.FromDictionary(new Dictionary<string, object?>
                    {
                        [nameof(ShopChip.Size)] = size,
                        [nameof(ShopChip.Selected)] = selected,
                        [nameof(ShopChip.StartIcon)] = ShopIcons.Outlined.Arrow_Left_MD,
                        [nameof(ShopChip.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "Chip"))
                    }))).ToHtmlString());
                html.Append("</div>");
            }
            foreach (var disabled in new[] { false, true })
            {
                html.Append("<div>");
                html.Append((await renderer.RenderComponentAsync<ShopChip>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ShopChip.Disabled)] = disabled,
                    [nameof(ShopChip.SelectedChanged)] = EventCallback.Factory.Create<bool>(new object(), _ => { }),
                    [nameof(ShopChip.AdditionalAttributes)] = new Dictionary<string, object> { ["id"] = disabled ? "chip-disabled" : "chip-toggle" },
                    [nameof(ShopChip.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "Selectable chip"))
                }))).ToHtmlString());
                html.Append("</div>");
            }
            foreach (var state in new[] { (3, 5), (3, 20), (10, 20), (18, 20) })
                html.Append((await renderer.RenderComponentAsync<ShopPagination>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ShopPagination.Page)] = state.Item1,
                    [nameof(ShopPagination.TotalPages)] = state.Item2
                }))).ToHtmlString());
            return html.ToString();
        });
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
    }
}
