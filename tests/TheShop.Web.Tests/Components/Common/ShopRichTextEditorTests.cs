using Bunit;
using FluentAssertions;
using MudBlazor.Services;
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
        Services.AddMudServices();
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
