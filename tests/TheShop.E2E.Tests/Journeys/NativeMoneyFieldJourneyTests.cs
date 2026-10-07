using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeMoneyFieldJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    public async Task MoneyFields_NativeChromeAndKeyboard_WorkWithoutVendorStyles(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = "";
            for (var index = 0; index < 4; index++)
            {
                decimal? amount = index == 1 ? null : 1234.5m;
                result += (await renderer.RenderComponentAsync<ShopMoneyField>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ShopMoneyField.Value)] = amount,
                    [nameof(ShopMoneyField.ValueExpression)] = (Expression<Func<decimal?>>)(() => amount),
                    [nameof(ShopMoneyField.Label)] = Strings.AddProduct_PriceLabel,
                    [nameof(ShopMoneyField.Disabled)] = index == 2,
                    [nameof(ShopMoneyField.Error)] = index == 3,
                    [nameof(ShopMoneyField.ErrorText)] = Strings.Product_PriceRequired,
                    [nameof(ShopMoneyField.AdditionalAttributes)] = new Dictionary<string, object> { ["id"] = $"money-{index}" }
                }))).ToHtmlString();
            }
            return result;
        });
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const form = document.createElement('form');
                form.id = 'money-probe';
                form.style.cssText = 'display:grid;gap:24px;padding:24px;max-width:640px;margin:auto';
                form.innerHTML = args.html + '<button type="submit">Save</button>';
                window.moneySubmits = 0;
                form.addEventListener('submit', event => { event.preventDefault(); window.moneySubmits++; });
                document.body.prepend(form);
                await document.fonts.ready;
            }
            """, new { html, withoutVendorCss });
        var first = Page.Locator("#money-0");
        await Assertions.Expect(first).ToHaveAccessibleNameAsync(Strings.AddProduct_PriceLabel);
        await Assertions.Expect(first).ToHaveValueAsync(CurrencyFormatter.Format(1234.5m));
        await Assertions.Expect(first).ToHaveAttributeAsync("type", "text");
        await Assertions.Expect(first).ToHaveAttributeAsync("inputmode", "decimal");
        await Assertions.Expect(Page.Locator("#money-2")).ToBeDisabledAsync();
        await Assertions.Expect(Page.Locator("#money-3")).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(Page.Locator("#money-3-error")).ToHaveTextAsync(Strings.Product_PriceRequired);
        await first.FocusAsync();
        var control = Page.Locator("#money-probe .shop-field-control").First;
        await Assertions.Expect(control).ToHaveCSSAsync("box-shadow", "rgb(23, 23, 23) 0px 0px 0px 2px inset");
        await first.PressAsync("Enter");
        (await Page.EvaluateAsync<int>("window.moneySubmits")).Should().Be(0);
        await first.PressAsync("Tab");
        await Assertions.Expect(Page.Locator("#money-1")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Page.Locator("#money-3")).ToBeFocusedAsync();
        await Page.Locator("#money-probe button").ClickAsync();
        (await Page.EvaluateAsync<int>("window.moneySubmits")).Should().Be(1);
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await Page.Locator("#money-probe").ScreenshotAsync(new() { Path = Path.Combine(directory, $"money-{width}-{withoutVendorCss}.png") });
        await Page.EvaluateAsync("document.documentElement.style.fontSize = '200%'");
        (await Page.Locator("#money-probe").EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce, ForcedColors = ForcedColors.Active });
        await first.FocusAsync();
        await Assertions.Expect(control).ToHaveCSSAsync("outline-style", "solid");
        await Assertions.Expect(Page.Locator("#money-probe .shop-field-label").First).ToHaveCSSAsync("transition-duration", "0s");
        errors.Should().BeEmpty();
    }
}
