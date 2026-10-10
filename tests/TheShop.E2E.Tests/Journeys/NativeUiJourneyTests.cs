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
public sealed class NativeUiJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private int _backendRequests;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        // Never allow this visual/input-only journey to reach the configured backend.
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            Interlocked.Increment(ref _backendRequests);
            await route.AbortAsync();
        });
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(1440, 900)]
    public async Task SignIn_Submit_ValidatesAndTypingWithoutBlurClearsErrors(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await OpenSignInAsync();
        var email = Page.GetByTestId("signin-email");
        var submit = Page.GetByTestId("signin-submit");
        var error = Page.Locator("#signin-email-error");

        await Assertions.Expect(submit).ToBeEnabledAsync();
        await Assertions.Expect(Page.Locator("label[for='signin-email']")).ToHaveTextAsync(Strings.Email_Label);
        await Assertions.Expect(email).ToHaveAttributeAsync("aria-describedby", "signin-instruction signin-email-error");
        await AssertNoOverflowAsync();
        await SavePageAsync($"signin-{width}-empty");

        await Page.Locator("label[for='signin-email']").ClickAsync();
        await Assertions.Expect(email).ToBeFocusedAsync();
        await email.FillAsync("native-ui@example.invalid");
        await Assertions.Expect(submit).ToBeEnabledAsync();
        await Assertions.Expect(email).ToBeFocusedAsync(); // Still focused: proves oninput, not blur/change.
        await Assertions.Expect(email).ToHaveCSSAsync("outline-style", "none");
        (await Page.Locator(".shop-field-control").EvaluateAsync<string>("el => getComputedStyle(el).boxShadow"))
            .Should().Contain("rgb(23, 23, 23)").And.Contain("inset");
        await SavePageAsync($"signin-{width}-field-focus");
        await Assertions.Expect(error).ToBeEmptyAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(submit).ToBeFocusedAsync();
        await SavePageAsync($"signin-{width}-valid");
        var focusDetails = await submit.EvaluateAsync<string>("el => JSON.stringify({visible:el.matches(':focus-visible'),outline:getComputedStyle(el).outline,token:getComputedStyle(el).getPropertyValue('--shop-focus-width'),dpr:devicePixelRatio})");
        (await submit.EvaluateAsync<bool>("""
            el => {
                const style = getComputedStyle(el);
                const width = parseFloat(style.outlineWidth);
                // Chromium can snap outlines down to device pixels at fractional Windows scaling.
                return el.matches(':focus-visible') && style.outlineStyle === 'solid' &&
                    parseFloat(style.getPropertyValue('--shop-focus-width')) === 2 && width > 0 &&
                    Math.abs(width - 2) <= 1 / devicePixelRatio + 0.01;
            }
            """)).Should().BeTrue($"keyboard focus needs a visible outline; observed {focusDetails}");
        await AssertNoOverflowAsync();
        await SavePageAsync($"signin-{width}-valid");

        await email.FillAsync("invalid-address");
        await Assertions.Expect(email).ToBeFocusedAsync();
        await Assertions.Expect(submit).ToBeEnabledAsync();
        await Assertions.Expect(error).ToBeEmptyAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(email).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(error).ToContainTextAsync(Strings.Email_Invalid);
        (await error.Locator(".validation-message").EvaluateAsync<string>("el => getComputedStyle(el).color"))
            .Should().Be("rgb(255, 66, 66)", "validation text must use the owner-selected base error token; its contrast gap is documented");
        await Assertions.Expect(email).ToHaveCSSAsync("outline-style", "none");
        (await Page.Locator(".shop-field-control").EvaluateAsync<string>("el => getComputedStyle(el).boxShadow"))
            .Should().Contain("rgb(255, 66, 66)").And.Contain("inset", "invalid styling belongs on the field chrome");
        await AssertNoOverflowAsync();
        await SavePageAsync($"signin-{width}-invalid");
        await email.FillAsync("still-invalid");
        await Assertions.Expect(error).ToBeEmptyAsync();
        await Assertions.Expect(email).Not.ToHaveAttributeAsync("aria-invalid", "true");
        _backendRequests.Should().Be(0, "typing and visual checks must not request an OTP or backend data");
    }

    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    public async Task SignIn_OutlinedField_AnimatesLabelAndPreservesValueAndFocusStates(int width, bool withoutVendorCss)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await OpenSignInAsync();
        if (withoutVendorCss)
        {
            await Page.EvaluateAsync("""
                () => {
                    for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                    }
                    document.getElementById('blazor-error-ui').style.display = 'none';
                }
                """);
        }
        var field = Page.Locator(".shop-field-control");
        var label = field.Locator("label");
        var input = Page.GetByTestId("signin-email");
        await Assertions.Expect(input).ToHaveAccessibleNameAsync(Strings.Email_Label);
        await Assertions.Expect(input).ToHaveAttributeAsync("placeholder", " ");
        await Assertions.Expect(label).ToHaveCSSAsync("font-size", "16px");
        await Assertions.Expect(label).ToHaveCSSAsync("font-weight", "400");
        await Assertions.Expect(label).ToHaveCSSAsync("color", "rgb(122, 122, 122)");
        await Assertions.Expect(field).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0)");
        (await field.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow"))
            .Should().Be("rgb(224, 224, 224) 0px 0px 0px 1px inset");
        var initial = (await field.BoundingBoxAsync())!;
        initial.Height.Should().BeApproximately(61, 0.1f);
        var initialLabel = (await label.BoundingBoxAsync())!;
        (initialLabel.Y + initialLabel.Height / 2).Should().BeApproximately(initial.Y + initial.Height / 2, 0.5f);
        (initialLabel.X - initial.X).Should().BeApproximately(38, 0.1f, "label padding places its text at x=42 after the 24px icon");
        (await input.EvaluateAsync<string>("el => getComputedStyle(el, '::placeholder').opacity")).Should().Be("0");
        await SavePageAsync($"outlined-field-{width}-placeholder");

        // Inspect the actual transition while it runs, rather than just its declared duration.
        (await input.EvaluateAsync<bool>("""
            async el => {
                el.focus();
                await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
                return el.parentElement.querySelector('label').getAnimations()
                    .some(animation => animation.effect.getTiming().duration === 240);
            }
            """)).Should().BeTrue();
        await Assertions.Expect(input).ToBeFocusedAsync();
        await Assertions.Expect(input).ToHaveValueAsync("");
        await Assertions.Expect(label).ToHaveCSSAsync("font-size", "12px");
        await Assertions.Expect(label).ToHaveCSSAsync("top", "0px");
        await Assertions.Expect(label).ToHaveCSSAsync("left", "10px");
        await Assertions.Expect(label).ToHaveCSSAsync("color", "rgb(23, 23, 23)");
        await Assertions.Expect(label).ToHaveCSSAsync("transition-timing-function", "cubic-bezier(0.4, 0, 0.2, 1)");
        await Assertions.Expect(label).ToHaveCSSAsync("background-color", "rgb(255, 255, 255)");
        await Assertions.Expect(input).ToHaveCSSAsync("outline-style", "none");
        (await field.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow"))
            .Should().Be("rgb(23, 23, 23) 0px 0px 0px 2px inset");
        (await input.EvaluateAsync<string>("el => getComputedStyle(el, '::placeholder').opacity")).Should().Be("0");
        await SavePageAsync($"outlined-field-{width}-focused-empty");

        await input.FillAsync("native-ui@example.invalid");
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(label).ToHaveCSSAsync("font-size", "12px");
        await Assertions.Expect(label).ToHaveCSSAsync("color", "rgb(122, 122, 122)");
        await SavePageAsync($"outlined-field-{width}-normal");
        await label.ClickAsync();
        await Assertions.Expect(input).ToBeFocusedAsync();
        await input.FillAsync("");
        await Assertions.Expect(label).ToHaveCSSAsync("font-size", "12px");
        // Sample the return transition deterministically, then let it finish normally.
        var midpoint = await input.EvaluateAsync<double[]>("""
            async el => {
                el.blur();
                const label = el.parentElement.querySelector('label');
                await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
                const animations = label.getAnimations();
                try {
                    for (const animation of animations) { animation.pause(); animation.currentTime = 120; }
                    const style = getComputedStyle(label);
                    const alpha = style.backgroundColor.match(/[\d.]+/g);
                    return [parseFloat(style.top), parseFloat(style.left), parseFloat(style.fontSize),
                        alpha.length === 4 ? Number(alpha[3]) : 1, animations.length];
                } finally {
                    for (const animation of animations) animation.play();
                }
            }
            """);
        midpoint[0].Should().BeGreaterThan(0).And.BeLessThan(30.5);
        midpoint[1].Should().BeGreaterThan(10).And.BeLessThan(38);
        midpoint[2].Should().BeGreaterThan(12).And.BeLessThan(16);
        midpoint[3].Should().BeGreaterThan(0).And.BeLessThan(1);
        midpoint[4].Should().BeGreaterThan(0);
        await Assertions.Expect(label).ToHaveCSSAsync("font-size", "16px");
        await Assertions.Expect(label).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0)");
        var afterClear = (await field.BoundingBoxAsync())!;
        afterClear.Width.Should().BeApproximately(initial.Width, 0.1f);
        afterClear.Height.Should().BeApproximately(initial.Height, 0.1f);

        // Native value restoration (including browser autofill) must not need a Blazor focus flag.
        await input.EvaluateAsync("el => el.value = 'restored@example.invalid'");
        await Assertions.Expect(label).ToHaveCSSAsync("font-size", "12px");
        await Assertions.Expect(label).ToHaveCSSAsync("top", "0px");
        await input.FillAsync("invalid");
        await Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(label).ToHaveCSSAsync("color", "rgb(255, 66, 66)");
        (await field.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow"))
            .Should().Be("rgb(255, 66, 66) 0px 0px 0px 2px inset");

        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await Assertions.Expect(label).ToHaveCSSAsync("transition-duration", "0s");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await Assertions.Expect(field).ToHaveCSSAsync("outline-style", "solid");
        await Assertions.Expect(input).ToHaveCSSAsync("outline-style", "none");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.None });
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        await input.FillAsync("native-ui@example.invalid");
        await Assertions.Expect(label).ToHaveCSSAsync("font-size", "24px");
        (await field.BoundingBoxAsync())!.Height.Should().BeApproximately(122, 0.1f);
        await AssertNoOverflowAsync();
        _backendRequests.Should().Be(0);
    }

    [Fact]
    public async Task TextFields_WithoutVendorStyles_KeepIndependentLabelsAndDescriptions()
    {
        await Page.SetViewportSizeAsync(390, 900);
        await OpenSignInAsync();
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string? value = null;
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(ShopTextField.Label)] = Strings.Email_Label,
                [nameof(ShopTextField.HelperText)] = Strings.Login_Instruction,
                [nameof(ShopTextField.ValueExpression)] = (System.Linq.Expressions.Expression<Func<string?>>)(() => value)
            };
            var first = await renderer.RenderComponentAsync<ShopTextField>(ParameterView.FromDictionary(parameters));
            parameters[nameof(ShopTextField.Value)] = "saved@example.invalid";
            parameters[nameof(ShopTextField.Disabled)] = true;
            parameters[nameof(ShopTextField.StartIcon)] = ShopIcons.Outlined.Mention;
            var second = await renderer.RenderComponentAsync<ShopTextField>(ParameterView.FromDictionary(parameters));
            return first.ToHtmlString() + second.ToHtmlString();
        });
        await Page.EvaluateAsync("""
            html => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'text-field-probe';
                host.className = 'shop-native';
                host.style.cssText = 'display:grid;gap:32px;padding:24px';
                host.innerHTML = html;
                document.body.appendChild(host);
            }
            """, html);
        var fields = Page.Locator("#text-field-probe .shop-field");
        var firstInput = fields.Nth(0).Locator("input");
        var secondInput = fields.Nth(1).Locator("input");
        (await firstInput.GetAttributeAsync("id")).Should().NotBe(await secondInput.GetAttributeAsync("id"));
        foreach (var field in await fields.AllAsync())
        {
            var input = field.Locator("input");
            await Assertions.Expect(input).ToHaveAccessibleNameAsync(Strings.Email_Label);
            await Assertions.Expect(input).ToHaveAttributeAsync("placeholder", " ");
            (await input.GetAttributeAsync("aria-describedby")).Should().Be(await field.Locator(".shop-field-hint").GetAttributeAsync("id"));
            (await field.Locator(".shop-field-control").BoundingBoxAsync())!.Height.Should().BeApproximately(61, 0.1f);
        }
        await Assertions.Expect(fields.Nth(0).Locator("svg")).ToHaveCountAsync(0);
        await Assertions.Expect(fields.Nth(0).Locator("label")).ToHaveCSSAsync("left", "10px");
        await fields.Nth(0).Locator("label").ClickAsync();
        await Assertions.Expect(firstInput).ToBeFocusedAsync();
        await Assertions.Expect(fields.Nth(0).Locator("label")).ToHaveCSSAsync("font-size", "12px");
        await Assertions.Expect(firstInput).ToHaveValueAsync("");
        await Assertions.Expect(secondInput).ToBeDisabledAsync();
        await Assertions.Expect(secondInput).ToHaveValueAsync("saved@example.invalid");
        await Assertions.Expect(fields.Nth(1).Locator("label")).ToHaveCSSAsync("font-size", "12px");
        await Page.Locator("#text-field-probe").ScreenshotAsync(new() { Path = EvidencePath("text-fields-label-only") });
        await AssertNoOverflowAsync();
        _backendRequests.Should().Be(0);
    }

    [Theory]
    [InlineData(390, 844, false)]
    [InlineData(1440, 900, true)]
    public async Task SignIn_FocusAndBusyState_UsesFieldChromeAndSpinnerWithoutVisibleStatusText(
        int width, int height, bool withoutVendorCss)
    {
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        await Context.RouteAsync("**/rest/v1/rpc/customer_exists", async route =>
        {
            Interlocked.Increment(ref requestCount);
            await releaseResponse.Task;
            await route.FulfillAsync(new() { ContentType = "application/json", Body = "false" });
        });
        await Page.SetViewportSizeAsync(width, height);
        await OpenSignInAsync();
        if (withoutVendorCss)
        {
            await Page.EvaluateAsync("""
                () => {
                    for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                    }
                    document.getElementById('blazor-error-ui').style.display = 'none';
                }
                """);
        }
        var email = Page.GetByTestId("signin-email");
        var submit = Page.GetByTestId("signin-submit");
        await email.FillAsync("native-ui@example.invalid");
        await Assertions.Expect(email).ToHaveCSSAsync("outline-style", "none");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await Assertions.Expect(Page.Locator(".shop-field-control")).ToHaveCSSAsync("outline-style", "solid");
        await SavePageAsync($"signin-{width}-forced-colors-focus");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.None });
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(submit).ToBeFocusedAsync();
        await Assertions.Expect(submit).ToHaveCSSAsync("outline-style", "solid");
        await Assertions.Expect(submit.Locator("svg")).ToHaveCSSAsync("outline-style", "none");
        var restingBounds = (await submit.BoundingBoxAsync())!;
        await submit.EvaluateAsync("el => { window.loadingContent = el.firstElementChild; window.loadingStatus = el.nextElementSibling; }");
        try
        {
            await Page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(submit).ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(submit).ToBeDisabledAsync();
            await Assertions.Expect(email).ToBeDisabledAsync();
            await Assertions.Expect(submit.Locator(".shop-loader")).ToBeVisibleAsync();
            await Assertions.Expect(submit.Locator(".shop-button-content")).ToHaveCSSAsync("opacity", "0");
            await Assertions.Expect(submit).ToHaveAccessibleNameAsync(Strings.Auth_Login_Submit);
            var loadingBounds = (await submit.BoundingBoxAsync())!;
            loadingBounds.Width.Should().BeApproximately(restingBounds.Width, 0.1f);
            loadingBounds.Height.Should().BeApproximately(restingBounds.Height, 0.1f);
            (await submit.EvaluateAsync<bool>("el => el.firstElementChild === window.loadingContent && el.nextElementSibling === window.loadingStatus"))
                .Should().BeTrue("content and primed live region must survive the loading transition");
            await Assertions.Expect(submit.Locator("svg.shop-icon")).ToHaveCSSAsync("outline-style", "none");
            var status = Page.Locator(".shop-auth-actions [role=status]");
            await Assertions.Expect(status).ToHaveTextAsync(Strings.Loading);
            await Assertions.Expect(status).ToHaveCSSAsync("clip-path", "inset(50%)");
            await Assertions.Expect(status).ToHaveCSSAsync("position", "absolute");
            await Assertions.Expect(status).ToHaveCSSAsync("width", "1px");
            await SavePageAsync($"signin-{width}-busy");
        }
        finally
        {
            releaseResponse.TrySetResult();
        }
        await Assertions.Expect(submit).ToBeEnabledAsync();
        await Assertions.Expect(Page.Locator(".shop-auth-actions [role=status]")).ToBeEmptyAsync();
        await Assertions.Expect(submit.Locator(".shop-button-content")).ToHaveCSSAsync("opacity", "1");
        await Assertions.Expect(submit.Locator(".shop-loader")).ToHaveCountAsync(0);
        requestCount.Should().Be(1);
        _backendRequests.Should().Be(0, "the held response is local; no OTP or backend write is allowed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Buttons_DirectIconClick_KeepsFocusOnOwningButton(bool withoutVendorCss)
    {
        await OpenSignInAsync();
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var textButton = await renderer.RenderComponentAsync<ShopButton>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(ShopButton.EndIcon)] = ShopIcons.Outlined.Arrow_Right_MD,
                    [nameof(ShopButton.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, "Login"))
                }));
            var iconButton = await renderer.RenderComponentAsync<ShopIconButton>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(ShopIconButton.Icon)] = ShopIcons.Outlined.Close_MD,
                    [nameof(ShopIconButton.Label)] = "Close"
                }));
            return textButton.ToHtmlString() + iconButton.ToHtmlString();
        });
        await Page.EvaluateAsync("""
            ({ html, withoutVendorCss }) => {
                if (withoutVendorCss) {
                    for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                    }
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('div');
                host.id = 'icon-click-probe';
                host.className = 'shop-native';
                host.style.cssText = 'display:flex;gap:24px;padding:32px';
                host.innerHTML = html;
                document.body.appendChild(host);
                for (const button of host.querySelectorAll('button')) {
                    button.dataset.clicks = '0';
                    button.addEventListener('click', () => button.dataset.clicks = String(+button.dataset.clicks + 1));
                }
            }
            """, new { html, withoutVendorCss });
        foreach (var button in await Page.Locator("#icon-click-probe button").AllAsync())
        {
            var icon = button.Locator("svg");
            var bounds = (await icon.BoundingBoxAsync())!;
            // Use real coordinates: the icon should delegate hit testing to its parent.
            await Page.Mouse.MoveAsync(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            await Page.Mouse.DownAsync();
            try
            {
                await Assertions.Expect(button).ToBeFocusedAsync();
                await Assertions.Expect(icon).ToHaveCSSAsync("outline-style", "none");
                await Assertions.Expect(button).ToHaveCSSAsync("outline-style", "none");
            }
            finally
            {
                await Page.Mouse.UpAsync();
            }
            await Assertions.Expect(button).ToHaveAttributeAsync("data-clicks", "1");
            await Page.Keyboard.PressAsync("Shift+Tab");
            await Page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(button).ToBeFocusedAsync();
            await Assertions.Expect(button).ToHaveCSSAsync("outline-style", "solid");
            await Assertions.Expect(icon).ToHaveCSSAsync("outline-style", "none");
        }
        await SavePageAsync($"buttons-direct-icon-click-{withoutVendorCss}");
        _backendRequests.Should().Be(0);
    }

    [Fact]
    public async Task Buttons_WithoutVendorStyles_MatchMeasuredFigmaRestingGeometry()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await OpenSignInAsync();
        await Page.EvaluateAsync("""
            () => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'native-button-evidence';
                host.className = 'shop-native';
                host.style.cssText = 'display:grid;grid-template-columns:repeat(3,220px);gap:32px;' +
                    'align-items:start;justify-items:start;padding:32px;background:white;width:max-content';
                document.body.appendChild(host);
            }
            """);

        (await Page.EvaluateAsync<bool>("""
            () => [...document.querySelectorAll('link[rel="stylesheet"]')]
                .filter(link => /MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href))
                .every(link => link.disabled)
            """)).Should().BeTrue();
        (await Page.EvaluateAsync<bool>("""
            () => [...document.querySelectorAll('link[rel="stylesheet"]')]
                .some(link => /\/TheShop\.css(?:\?|$)/i.test(link.href) && !link.disabled && link.sheet)
            """)).Should().BeTrue("the test must use the actual compiled project stylesheet");

        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        ShopVariant[] variants = [ShopVariant.Filled, ShopVariant.Outlined, ShopVariant.Text];
        ShopSize[] sizes = [ShopSize.Small, ShopSize.Medium, ShopSize.Large];
        double[][] widths = [[128, 144, 160], [126, 142, 158], [118, 128, 138]];
        double[][] heights = [[34, 40, 46], [32, 38, 44], [26, 40, 46]];

        for (var variant = 0; variant < variants.Length; variant++)
        {
            for (var size = 0; size < sizes.Length; size++)
            {
                var id = $"button-{variants[variant]}-{sizes[size]}";
                var parameters = new Dictionary<string, object?>
                {
                    [nameof(ShopButton.Variant)] = variants[variant],
                    [nameof(ShopButton.Size)] = sizes[size],
                    [nameof(ShopButton.StartIcon)] = ShopIcons.Outlined.Arrow_Left_MD,
                    [nameof(ShopButton.EndIcon)] = ShopIcons.Outlined.Arrow_Right_MD,
                    [nameof(ShopButton.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, "Button")),
                    [nameof(ShopButton.AdditionalAttributes)] = new Dictionary<string, object> { ["data-testid"] = id }
                };
                var html = await renderer.Dispatcher.InvokeAsync(async () =>
                    (await renderer.RenderComponentAsync<ShopButton>(ParameterView.FromDictionary(parameters))).ToHtmlString());
                await Page.Locator("#native-button-evidence").EvaluateAsync(
                    "(host, html) => host.insertAdjacentHTML('beforeend', html)", html);
            }
        }

        await WaitForFontsAsync();
        for (var variant = 0; variant < variants.Length; variant++)
        {
            for (var size = 0; size < sizes.Length; size++)
            {
                var button = Page.GetByTestId($"button-{variants[variant]}-{sizes[size]}");
                var measurements = await button.EvaluateAsync<double[]>("""
                    el => {
                        const rect = el.getBoundingClientRect(), style = getComputedStyle(el);
                        const icons = [...el.querySelectorAll('svg')].map(icon => icon.getBoundingClientRect());
                        return [rect.width, rect.height, parseFloat(style.fontSize), parseFloat(style.borderRadius),
                            parseFloat(style.columnGap), ...icons.flatMap(icon => [icon.width, icon.height])];
                    }
                    """);
                var subject = $"Figma 157:24 {variants[variant]} {sizes[size]}";
                measurements[0].Should().BeApproximately(widths[variant][size], 1, subject);
                measurements[1].Should().BeApproximately(heights[variant][size], 1, subject);
                measurements[2].Should().Be(14, subject);
                measurements[3].Should().Be(0, subject);
                measurements[4].Should().Be(12, subject);
                measurements.Skip(5).Should().HaveCount(4).And.AllBeEquivalentTo(18d + 2 * size, subject);
                (await button.EvaluateAsync<string>("el => getComputedStyle(el).fontFamily")).Should().Contain("Space Grotesk");
                (await button.EvaluateAsync<string>("el => getComputedStyle(el).fontWeight")).Should().Be("500");
                (await button.EvaluateAsync<bool>("""
                    el => {
                        const bounds = el.getBoundingClientRect();
                        const inside = rect => rect.left >= bounds.left - 1 && rect.right <= bounds.right + 1 &&
                            rect.top >= bounds.top - 1 && rect.bottom <= bounds.bottom + 1;
                        const text = [...el.querySelector('.shop-button-content').childNodes].filter(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim());
                        return el.scrollWidth <= el.clientWidth + 1 && el.scrollHeight <= el.clientHeight + 1 &&
                            [...el.querySelectorAll('svg')].every(icon => inside(icon.getBoundingClientRect())) &&
                            text.every(node => { const range = document.createRange(); range.selectNodeContents(node);
                                return inside(range.getBoundingClientRect()); });
                    }
                    """)).Should().BeTrue($"{subject} must not clip its label or icons");
            }
        }

        await Page.Locator("#native-button-evidence").ScreenshotAsync(new() { Path = EvidencePath("buttons-no-vendor-css") });
        await VerifySurfaceIconButtonsAsync(renderer);
        await VerifyIconButtonSizesAsync(renderer);
        _backendRequests.Should().Be(0, "static button composition must not call application services");
    }

    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    public async Task Buttons_Loading_PreservesGeometryAndNamesAcrossSizesAndVariants(int width, bool withoutVendorCss)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await OpenSignInAsync();
        await Page.EvaluateAsync("""
            withoutVendorCss => {
                if (withoutVendorCss) {
                    for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                    }
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'loading-button-probe';
                host.className = 'shop-native';
                host.style.cssText = 'display:flex;flex-wrap:wrap;align-items:center;gap:16px;padding:16px';
                document.body.appendChild(host);
            }
            """, withoutVendorCss);
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var host = Page.Locator("#loading-button-probe");
        foreach (var iconOnly in new[] { false, true })
            foreach (var variant in Enum.GetValues<ShopVariant>())
                foreach (var size in Enum.GetValues<ShopSize>())
                {
                    var id = $"loading-{iconOnly}-{variant}-{size}";
                    foreach (var loading in new[] { false, true })
                    {
                        var parameters = new Dictionary<string, object?>
                        {
                            [nameof(ShopButton.Loading)] = loading,
                            [nameof(ShopButton.Variant)] = variant,
                            [nameof(ShopButton.Size)] = size,
                            [nameof(ShopButton.AdditionalAttributes)] = new Dictionary<string, object> { ["data-testid"] = $"{id}-{loading}" }
                        };
                        if (iconOnly)
                        {
                            parameters[nameof(ShopIconButton.Icon)] = ShopIcons.Outlined.Close_MD;
                            parameters[nameof(ShopIconButton.Label)] = Strings.Close;
                        }
                        else
                        {
                            parameters[nameof(ShopButton.StartIcon)] = ShopIcons.Outlined.Arrow_Left_MD;
                            parameters[nameof(ShopButton.EndIcon)] = ShopIcons.Outlined.Arrow_Right_MD;
                            parameters[nameof(ShopButton.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, Strings.Save));
                        }
                        var html = await renderer.Dispatcher.InvokeAsync(async () =>
                            (await renderer.RenderComponentAsync(iconOnly ? typeof(ShopIconButton) : typeof(ShopButton),
                                ParameterView.FromDictionary(parameters))).ToHtmlString());
                        await host.EvaluateAsync("(host, html) => host.insertAdjacentHTML('beforeend', html)", html);
                    }
                    var idle = Page.GetByTestId($"{id}-False");
                    var busy = Page.GetByTestId($"{id}-True");
                    await Assertions.Expect(busy).ToHaveAccessibleNameAsync(iconOnly ? Strings.Close : Strings.Save);
                    await Assertions.Expect(busy).ToBeDisabledAsync();
                    await Assertions.Expect(busy).ToHaveAttributeAsync("aria-busy", "true");
                    await Assertions.Expect(busy.Locator(".shop-button-content")).ToHaveCSSAsync("opacity", "0");
                    await Assertions.Expect(idle.Locator(".shop-button-content")).ToHaveCSSAsync("opacity", "1");
                    var resting = (await idle.BoundingBoxAsync())!;
                    var loadingBounds = (await busy.BoundingBoxAsync())!;
                    loadingBounds.Width.Should().BeApproximately(resting.Width, 0.1f, id);
                    loadingBounds.Height.Should().BeApproximately(resting.Height, 0.1f, id);
                    var spinner = (await busy.Locator(".shop-loader").BoundingBoxAsync())!;
                    (spinner.X + spinner.Width / 2).Should().BeApproximately(loadingBounds.X + loadingBounds.Width / 2, 0.5f, id);
                    (spinner.Y + spinner.Height / 2).Should().BeApproximately(loadingBounds.Y + loadingBounds.Height / 2, 0.5f, id);
                    (await busy.Locator(".shop-loader").EvaluateAsync<double>("el => parseFloat(getComputedStyle(el).width)"))
                        .Should().BeApproximately((await idle.Locator("svg").First.BoundingBoxAsync())!.Width, 0.1, id);
                }
        await AssertNoOverflowAsync();
        await host.ScreenshotAsync(new() { Path = EvidencePath($"buttons-loading-{width}") });
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await Assertions.Expect(host.Locator(".shop-loader svg").First).ToHaveCSSAsync("animation-name", "none");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await Assertions.Expect(host.Locator(".shop-loader circle").First).ToHaveCSSAsync("stroke-linecap", "round");
        _backendRequests.Should().Be(0);
    }

    [Fact]
    public async Task Typography_WithoutVendorStyles_MatchesFigmaTextStylesAndNativeConsumers()
    {
        await Page.SetViewportSizeAsync(1440, 1200);
        await OpenSignInAsync();
        await Page.EvaluateAsync("""
            async () => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'native-typography-evidence';
                host.className = 'shop-native';
                host.style.cssText = 'padding:32px;background:white;width:max-content';
                document.body.appendChild(host);
                for (const font of ['800 96px "Barlow Condensed"', '700 34px "Barlow Condensed"',
                    '500 20px "Space Grotesk"', '400 16px "Space Grotesk"']) {
                    const loaded = await document.fonts.load(font);
                    if (!loaded.length) throw new Error(`Required font unavailable: ${font}`);
                }
                await document.fonts.ready;
            }
            """);

        // Independent expectations from Figma Typography/*, not generated from the Sass map.
        (string Name, string Family, int Size, int Weight, double Tracking, int Height)[] styles =
        [
            ("h1", "Barlow Condensed", 96, 800, 3, 115),
            ("h2", "Barlow Condensed", 60, 800, 3, 72),
            ("h3", "Barlow Condensed", 48, 800, 3, 58),
            ("h4", "Barlow Condensed", 34, 700, 2, 41),
            ("h5", "Barlow Condensed", 24, 700, 2, 29),
            ("h6", "Space Grotesk", 20, 500, 0.25, 26),
            ("subtitle-1", "Space Grotesk", 16, 500, 0.25, 20),
            ("subtitle-2", "Space Grotesk", 14, 500, 0.25, 18),
            ("body-1", "Space Grotesk", 16, 400, 0.25, 20),
            ("body-2", "Space Grotesk", 14, 400, 0.25, 18),
            ("button", "Space Grotesk", 14, 500, 0.25, 18),
            ("caption", "Space Grotesk", 12, 400, 0.25, 15),
            ("overline", "Space Grotesk", 10, 400, 0.25, 13)
        ];
        var host = Page.Locator("#native-typography-evidence");
        foreach (var expected in styles)
        {
            await host.EvaluateAsync("""
                (host, name) => {
                    const sample = document.createElement('div');
                    sample.dataset.style = name;
                    sample.textContent = `${name}: Women's Clothes`;
                    for (const property of ['font-family', 'font-size', 'font-weight', 'line-height',
                        'letter-spacing', 'text-transform']) {
                        sample.style.setProperty(property, `var(--shop-typography-${name}-${property})`);
                    }
                    host.appendChild(sample);
                }
                """, expected.Name);
            var sample = host.Locator($"[data-style='{expected.Name}']");
            var metrics = await sample.EvaluateAsync<double[]>("""
                el => {
                    const style = getComputedStyle(el);
                    return [parseFloat(style.fontSize), parseFloat(style.fontWeight),
                        parseFloat(style.letterSpacing), el.getBoundingClientRect().height];
                }
                """);
            metrics[0].Should().Be(expected.Size, expected.Name);
            metrics[1].Should().Be(expected.Weight, expected.Name);
            metrics[2].Should().Be(expected.Tracking, expected.Name);
            metrics[3].Should().BeApproximately(expected.Height, 1, $"{expected.Name} AUTO line box with the supplied font");
            (await sample.EvaluateAsync<string>("el => getComputedStyle(el).fontFamily")).Should().Contain(expected.Family);
            (await sample.EvaluateAsync<string>("el => getComputedStyle(el).lineHeight")).Should().Be("normal");
            (await sample.EvaluateAsync<string>("el => getComputedStyle(el).textTransform"))
                .Should().Be(expected.Name == "button" ? "capitalize" : "none");
        }

        // Exercise real owned selectors, including the unbound product-title exception and <s> semantics.
        await host.EvaluateAsync("""
            host => host.insertAdjacentHTML('beforeend', `
                <div class="shop-product-card-brand">PUFFCO</div>
                <div class="shop-product-card-title">Product title</div>
                <div class="shop-product-card-price">$100.00</div>
                <s class="shop-product-card-original-price">$120.00</s>
                <div class="shop-field-control">
                    <input id="typography-field" class="shop-field-input" placeholder=" " />
                    <label for="typography-field" class="shop-field-label">Email</label>
                </div>
                <div class="shop-image-label">Image unavailable</div>`)
            """);
        (string Class, int Size, int Weight)[] consumers =
        [
            ("shop-product-card-brand", 16, 500), ("shop-product-card-title", 20, 400),
            ("shop-product-card-price", 20, 500), ("shop-product-card-original-price", 16, 500),
            ("shop-field-label", 16, 400), ("shop-image-label", 12, 400)
        ];
        foreach (var expected in consumers)
        {
            (await host.Locator($".{expected.Class}").EvaluateAsync<double[]>("""
                el => [parseFloat(getComputedStyle(el).fontSize), parseFloat(getComputedStyle(el).fontWeight)]
                """)).Should().Equal(expected.Size, expected.Weight);
        }

        (await host.Locator("s").EvaluateAsync<string>("el => getComputedStyle(el).textDecorationLine"))
            .Should().Be("line-through");
        await host.ScreenshotAsync(new() { Path = EvidencePath("typography-no-vendor-css") });

        // Token sizes remain scalable, without changing the application's root size.
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        foreach (var expected in styles)
        {
            (await host.Locator($"[data-style='{expected.Name}']").EvaluateAsync<double>(
                "el => parseFloat(getComputedStyle(el).fontSize)"))
                .Should().Be(expected.Size * 2, $"{expected.Name} must scale with the root font size");
        }

        _backendRequests.Should().Be(0, "typography checks must not request backend data");
    }

    private async Task VerifySurfaceIconButtonsAsync(HtmlRenderer renderer)
    {
        foreach (var disabled in new[] { false, true })
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(ShopIconButton.Color)] = ShopColor.Surface,
                [nameof(ShopIconButton.Disabled)] = disabled,
                [nameof(ShopIconButton.Icon)] = ShopIcons.Outlined.Shopping_Bag_01,
                [nameof(ShopIconButton.Label)] = Strings.AddToCart,
                [nameof(ShopIconButton.AdditionalAttributes)] = new Dictionary<string, object>
                {
                    ["data-testid"] = disabled ? "surface-disabled" : "surface-enabled"
                }
            };
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<ShopIconButton>(ParameterView.FromDictionary(parameters))).ToHtmlString());
            await Page.Locator("#native-button-evidence").EvaluateAsync(
                "(host, html) => host.insertAdjacentHTML('beforeend', html)", html);
        }

        var enabled = Page.GetByTestId("surface-enabled");
        var disabledButton = Page.GetByTestId("surface-disabled");
        foreach (var button in new[] { enabled, disabledButton })
        {
            var geometry = await button.EvaluateAsync<double[]>("""
                el => {
                    const box = el.getBoundingClientRect(), icon = el.querySelector('svg').getBoundingClientRect();
                    return [box.width, box.height, icon.width, icon.height, parseFloat(getComputedStyle(el).paddingTop)];
                }
                """);
            geometry.Should().Equal(36d, 36d, 24d, 24d, 6d);
            (await button.GetAttributeAsync("class")).Should().NotContain("shop-product-card",
                "shared icon-button appearance must work independently of the product card");
        }

        (await enabled.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor")).Should().Be("rgb(255, 255, 255)");
        (await enabled.EvaluateAsync<string>("el => getComputedStyle(el).color")).Should().Be("rgb(23, 23, 23)");
        await enabled.HoverAsync();
        (await enabled.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor")).Should().Be("rgb(245, 245, 245)");
        await Page.Mouse.DownAsync();
        try
        {
            (await enabled.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor")).Should().Be("rgb(232, 232, 232)");
        }
        finally
        {
            await Page.Mouse.UpAsync();
        }

        await Assertions.Expect(disabledButton).ToBeDisabledAsync();
        await disabledButton.HoverAsync();
        (await disabledButton.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor")).Should().Be("rgb(232, 232, 232)");
        (await disabledButton.EvaluateAsync<string>("el => getComputedStyle(el).color")).Should().Be("rgb(122, 122, 122)");
        await Page.Locator("#native-button-evidence").ScreenshotAsync(new() { Path = EvidencePath("surface-icon-buttons-no-vendor-css") });
    }

    private async Task VerifyIconButtonSizesAsync(HtmlRenderer renderer)
    {
        (ShopVariant Variant, ShopSize Size, int Frame, int Padding, int Icon)[] cases =
        [
            (ShopVariant.Filled, ShopSize.Small, 30, 5, 20),
            (ShopVariant.Filled, ShopSize.Medium, 36, 6, 24),
            (ShopVariant.Filled, ShopSize.Large, 42, 5, 32),
            (ShopVariant.Outlined, ShopSize.Small, 28, 4, 20),
            (ShopVariant.Outlined, ShopSize.Medium, 34, 5, 24),
            (ShopVariant.Outlined, ShopSize.Large, 40, 4, 32),
            (ShopVariant.Text, ShopSize.Small, 24, 3, 18),
            (ShopVariant.Text, ShopSize.Medium, 48, 12, 24),
            (ShopVariant.Text, ShopSize.Large, 56, 12, 32)
        ];
        var host = Page.Locator("#native-button-evidence");
        foreach (var expected in cases)
        {
            var id = $"icon-button-{expected.Variant}-{expected.Size}";
            var parameters = new Dictionary<string, object?>
            {
                [nameof(ShopIconButton.Variant)] = expected.Variant,
                [nameof(ShopIconButton.Size)] = expected.Size,
                [nameof(ShopIconButton.Icon)] = ShopIcons.Outlined.Close_MD,
                [nameof(ShopIconButton.Label)] = Strings.Close,
                [nameof(ShopIconButton.AdditionalAttributes)] = new Dictionary<string, object> { ["data-testid"] = id }
            };
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<ShopIconButton>(ParameterView.FromDictionary(parameters))).ToHtmlString());
            await host.EvaluateAsync("(host, html) => host.insertAdjacentHTML('beforeend', html)", html);
            var button = Page.GetByTestId(id);
            await Assertions.Expect(button).ToHaveAccessibleNameAsync(Strings.Close);
            var metrics = await button.EvaluateAsync<double[]>("""
                el => {
                    const box = el.getBoundingClientRect(), icon = el.querySelector('svg').getBoundingClientRect();
                    const style = getComputedStyle(el);
                    return [box.width, box.height, icon.width, icon.height,
                        parseFloat(style.paddingTop), parseFloat(style.paddingLeft)];
                }
                """);
            var subject = $"Figma 322:6450 {expected.Variant} {expected.Size}";
            metrics[0].Should().BeApproximately(expected.Frame, 1, subject);
            metrics[1].Should().BeApproximately(expected.Frame, 1, subject);
            metrics.Skip(2).Should().Equal(expected.Icon, expected.Icon, expected.Padding, expected.Padding);
            (await button.EvaluateAsync<bool>("el => el.querySelectorAll('.shop-button-content > svg').length === 1 && el.scrollWidth <= el.clientWidth + 1 && el.scrollHeight <= el.clientHeight + 1"))
                .Should().BeTrue(subject + " renders a single unclipped icon within the shared content slot");
        }
        await host.ScreenshotAsync(new() { Path = EvidencePath("icon-button-sizes-no-vendor-css") });
    }

    private async Task OpenSignInAsync()
    {
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await WaitForFontsAsync();
    }

    private Task WaitForFontsAsync() => Page.EvaluateAsync("""
        async () => {
            await document.fonts.load('500 14px "Space Grotesk"');
            await document.fonts.ready;
            if (![...document.fonts].some(font => font.family.includes('Space Grotesk') && font.status === 'loaded'))
                throw new Error('Space Grotesk must load before geometry can be measured.');
        }
        """);

    private async Task AssertNoOverflowAsync() =>
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"))
            .Should().BeTrue("the sign-in page must fit the viewport without horizontal scrolling");

    private Task SavePageAsync(string name) => Page.ScreenshotAsync(new() { Path = EvidencePath(name), FullPage = true });

    private static string EvidencePath(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name + ".png");
    }
}
