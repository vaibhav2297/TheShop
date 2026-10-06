using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Components.Common;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeRichTextEditorJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false, false)]
    [InlineData(1440, false, false)]
    [InlineData(1440, true, false)]
    [InlineData(1440, true, true)]
    public async Task RichTextEditor_UsesFieldChromeAndKeepsQuillBehavior(int width, bool withoutVendorCss, bool disabled)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", r => r.AbortAsync());
        await Page.SetViewportSizeAsync(width, 844);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton<IJSRuntime, StaticRenderJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ShopRichTextEditor>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ShopRichTextEditor.Disabled)] = disabled,
                ["data-testid"] = "editor-probe"
            }))).ToHtmlString());

        // Production host markup and the real Quill module; bUnit covers the C# interop contract.
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const wrapper = document.createElement('section');
                wrapper.style.padding = '16px';
                wrapper.innerHTML = '<h2 id="probe-label">Description</h2>' + args.html;
                document.body.prepend(wrapper);
                window.__editorChanges = [];
                const dotNetRef = { invokeMethodAsync: (name, ...values) => { window.__editorChanges.push({ name, values }); return Promise.resolve(); } };
                const module = await import('./js/shop-rich-text-editor.js');
                await module.init(document.querySelector('[data-testid=editor-probe]'), dotNetRef, {
                    formats: ['header', 'bold', 'italic', 'list', 'link'],
                    disabled: args.disabled,
                    initialHtml: args.disabled ? '<p>Locked copy</p>' : null,
                    placeholder: 'Describe the product',
                    ariaLabelledBy: 'probe-label'
                });
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, new { html, withoutVendorCss, disabled });

        var host = Page.GetByTestId("editor-probe");
        var toolbar = host.Locator(".ql-toolbar");
        var editor = host.Locator(".ql-editor");
        await Assertions.Expect(editor).ToHaveAttributeAsync("aria-labelledby", "probe-label");
        await Assertions.Expect(host).ToHaveCSSAsync("box-shadow", "rgb(224, 224, 224) 0px 0px 0px 1px inset");
        await Assertions.Expect(toolbar).ToHaveCSSAsync("border-top-width", "0px");
        await Assertions.Expect(toolbar).ToHaveCSSAsync("box-shadow", "rgb(224, 224, 224) 0px -1px 0px 0px inset");
        await Assertions.Expect(host.Locator(".ql-container")).ToHaveCSSAsync("border-top-width", "0px");
        await Assertions.Expect(editor).ToHaveCSSAsync("min-height", "160px");
        await Assertions.Expect(editor).ToHaveCSSAsync("padding", "12px 14px");
        await Assertions.Expect(editor).ToHaveCSSAsync("font-size", "16px");
        await Assertions.Expect(editor).ToHaveCSSAsync("letter-spacing", "0.25px");
        (await editor.EvaluateAsync<string>("el => getComputedStyle(el).fontFamily")).Should().StartWith("\"Space Grotesk\"");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1")).Should().BeTrue();

        if (disabled)
        {
            await Assertions.Expect(editor).ToHaveAttributeAsync("contenteditable", "false");
            await Assertions.Expect(editor).ToHaveCSSAsync("color", "rgb(122, 122, 122)");
            await Assertions.Expect(toolbar).ToHaveCSSAsync("pointer-events", "none");
            await Assertions.Expect(toolbar.Locator(".ql-bold .ql-stroke").First).ToHaveCSSAsync("stroke", "rgb(176, 176, 176)");
            await SaveAsync($"rich-text-{width}-{withoutVendorCss}-disabled");
        }
        else
        {
            (await editor.EvaluateAsync<string>("el => getComputedStyle(el, '::before').color")).Should().Be("rgb(122, 122, 122)");
            (await editor.EvaluateAsync<string>("el => getComputedStyle(el, '::before').fontStyle")).Should().Be("normal");
            await SaveAsync($"rich-text-{width}-{withoutVendorCss}-idle");

            await editor.ClickAsync();
            await Assertions.Expect(host).ToHaveCSSAsync("box-shadow", "rgb(23, 23, 23) 0px 0px 0px 2px inset");
            await Assertions.Expect(editor).ToHaveCSSAsync("outline-style", "none");
            await toolbar.Locator(".ql-bold").FocusAsync();
            await Page.Keyboard.PressAsync("Shift+Tab");
            await Page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(toolbar.Locator(".ql-bold")).ToHaveCSSAsync("outline-style", "solid");
            await editor.ClickAsync();
            await toolbar.Locator(".ql-bold").ClickAsync();
            await Page.Keyboard.TypeAsync("Bold copy");
            await Assertions.Expect(editor.Locator("strong")).ToHaveTextAsync("Bold copy");
            var last = await Page.EvaluateAsync<string>("() => JSON.stringify(window.__editorChanges.filter(c => c.name === 'OnTextChanged').at(-1).values)");
            // Quill 2.0.3 getSemanticHTML emits &nbsp; for spaces; that serialization predates this migration.
            last.Should().Be("[\"<p><strong>Bold&nbsp;copy</strong></p>\",9]");
            await SaveAsync($"rich-text-{width}-{withoutVendorCss}-focused");
        }

        errors.Should().BeEmpty();
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }

    // Static rendering never runs OnAfterRenderAsync, so the component's import is never invoked.
    private sealed class StaticRenderJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException();
    }
}
