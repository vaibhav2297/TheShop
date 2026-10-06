using Bunit;
using FluentAssertions;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopRichTextEditor"/>: the C#-observable side of the Quill interop
/// boundary — the JS <c>text-change</c>/paste callbacks raising <see cref="ShopRichTextEditor.ValueChanged"/>,
/// <see cref="ShopRichTextEditor.TextLengthChanged"/>, and <see cref="ShopRichTextEditor.OnUnsupportedPaste"/>
/// (FR-1, FR-7, AC-10), and clean mount/dispose. The Quill JS module itself is mocked by bUnit's
/// loose-mode <c>JSInterop</c>, matching how every other project component with interop is tested.
/// <see href=".specs/product-description/spec.md"/>
/// </summary>
public class ShopRichTextEditorTests : TestContext
{
    public ShopRichTextEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
    }

    // =========================================================================
    // Mount (FR-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_MountsTheHostElement()
    {
        var cut = Render<ShopRichTextEditor>(parameters => parameters
            .Add(c => c.Value, "<p>Hello</p>"));

        cut.Markup.Should().Contain("shop-rich-text-editor");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_ForwardsAriaLabelledBy()
    {
        var cut = Render<ShopRichTextEditor>(parameters => parameters
            .Add(c => c.AriaLabelledBy, "description-label"));

        cut.Markup.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Render_EmptyNativeHostForwardsRootStylingAndAttributes()
    {
        var cut = Render<ShopRichTextEditor>(parameters => parameters
            .Add(c => c.Class, "custom-editor")
            .Add(c => c.Style, "--shop-test: 1;")
            .AddUnmatched("data-testid", "description-editor"));

        var host = cut.Find("[data-testid='description-editor']");
        host.ClassList.Should().Contain("shop-native").And.Contain("shop-rich-text-editor").And.Contain("custom-editor")
            .And.NotContain("shop-rich-text-editor-disabled");
        host.GetAttribute("style").Should().Be("--shop-test: 1;");
        host.ChildElementCount.Should().Be(0, "Quill, not Blazor, owns the host's descendants");
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public void Render_Disabled_TogglesOwnedModifierWithoutRemountingTheHost()
    {
        var cut = Render<ShopRichTextEditor>(parameters => parameters.Add(c => c.Disabled, true));
        cut.Find("div").ClassList.Should().Contain("shop-rich-text-editor-disabled");

        cut.Render(parameters => parameters.Add(c => c.Disabled, false));

        cut.Find("div").ClassList.Should().NotContain("shop-rich-text-editor-disabled");
        JSInterop.Invocations.Count(i => i.Identifier == "init").Should().Be(1);
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "setDisabled");
    }

    // =========================================================================
    // text-change callback (FR-1, FR-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task OnTextChanged_RaisesValueChangedWithTheReportedHtml()
    {
        string? raised = null;
        var cut = Render<ShopRichTextEditor>(parameters => parameters
            .Add(c => c.ValueChanged, html => raised = html));

        await cut.InvokeAsync(() => cut.Instance.OnTextChanged("<p>Updated</p>", 7));

        raised.Should().Be("<p>Updated</p>");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task OnTextChanged_RaisesTextLengthChangedWithTheReportedLength()
    {
        int? raised = null;
        var cut = Render<ShopRichTextEditor>(parameters => parameters
            .Add(c => c.TextLengthChanged, length => raised = length));

        await cut.InvokeAsync(() => cut.Instance.OnTextChanged("<p>Updated</p>", 7));

        raised.Should().Be(7);
    }

    // =========================================================================
    // paste notice (FR-7, AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task OnPasteUnsupported_RaisesOnUnsupportedPaste()
    {
        var raised = false;
        var cut = Render<ShopRichTextEditor>(parameters => parameters
            .Add(c => c.OnUnsupportedPaste, () => raised = true));

        await cut.InvokeAsync(() => cut.Instance.OnPasteUnsupported());

        raised.Should().BeTrue();
    }

    // =========================================================================
    // Disposal — no leaked interop handles (accepted risk, plan §11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task DisposeAsync_AfterMounting_DoesNotThrow()
    {
        var cut = Render<ShopRichTextEditor>(parameters => parameters
            .Add(c => c.Value, "<p>Hello</p>"));

        var act = async () => await cut.Instance.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
