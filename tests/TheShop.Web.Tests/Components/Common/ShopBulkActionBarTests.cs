using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopBulkActionBar"/> — the viewport-pinned bar that appears while a list
/// has a live multi-selection (FR-20, FR-21, FR-23; Figma 2611:7585). Covers the visibility gate,
/// the selected-count label, and the fact that the bar renders whatever actions the calling page
/// supplies without knowing anything about them — which is what lets the page keep its own
/// permission gates around each action (AC-16, AC-26).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class ShopBulkActionBarTests : TestContext
{
    public ShopBulkActionBarTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    // =========================================================================
    // Visibility gate (FR-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WhenNotVisible_RendersNothing()
    {
        var cut = Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, false)
            .Add(c => c.SelectedCount, 0)
            .Add(c => c.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<em>action</em>"))));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WhenVisible_RendersTheBar()
    {
        var cut = Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.SelectedCount, 1));

        cut.Find(".shop-bulk-action-bar").Should().NotBeNull();
    }

    // =========================================================================
    // Selected-count label (FR-20)
    // =========================================================================

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [Trait("Feature", "manage-brands")]
    public void Render_WhenVisible_ReportsTheSelectedCount(int count)
    {
        var cut = Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.SelectedCount, count));

        cut.Find(".shop-bulk-action-bar").TextContent.Should().Contain(count.ToString());
    }

    // =========================================================================
    // Caller-supplied actions (FR-23 — gating stays with the page)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithActions_RendersTheCallerSuppliedActions()
    {
        var cut = Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.SelectedCount, 3)
            .Add(c => c.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<em id=\"bulk-action\">Delete</em>"))));

        cut.Find("#bulk-action").TextContent.Should().Be("Delete");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithNoActions_StillRendersTheCountAndDoesNotThrow()
    {
        var act = () => Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.SelectedCount, 4));

        act.Should().NotThrow();
    }

    // =========================================================================
    // Styling contract (reusable-component rule: Class/Style forwarded)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithClass_ForwardsTheCallerClassAlongsideItsOwn()
    {
        var cut = Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.SelectedCount, 1)
            .Add(c => c.Class, "my-custom-class"));

        var bar = cut.Find(".shop-bulk-action-bar");
        bar.ClassList.Should().Contain("my-custom-class");
        bar.ClassList.Should().Contain("mud-theme-dark");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WithStyle_ForwardsTheCallerStyle()
    {
        var cut = Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.SelectedCount, 1)
            .Add(c => c.Style, "opacity:0.5"));

        cut.Find(".shop-bulk-action-bar").GetAttribute("style").Should().Contain("opacity:0.5");
    }

    // =========================================================================
    // Layout contract — the content row must span the bar
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Render_WhenVisible_StretchesTheContentRowAcrossTheFullBarWidth()
    {
        // Regression: the stretch used to come from a `.shop-bulk-action-bar > .mud-stack` SCSS
        // rule, but MudStack renders `d-flex flex-row ...` and never emits a `mud-stack` class — so
        // the rule matched nothing, the row shrank to its content, and the count and actions bunched
        // up at the start instead of sitting at either end of the bar. The stretch now rides on the
        // component itself via MudBlazor's flex-grow-1 utility, which this pins.
        var cut = Render<ShopBulkActionBar>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.SelectedCount, 2));

        var contentRow = cut.Find(".shop-bulk-action-bar > div");

        contentRow.ClassList.Should().Contain("flex-grow-1",
            "the bar is a flex container, so its content row only fills the width if it grows");
        contentRow.ClassList.Should().Contain("justify-space-between",
            "the count sits at the start and the actions at the end of that stretched row");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-22: Render_WhenNotVisible_RendersNothing, Render_WhenVisible_RendersTheBar,
//         Render_WhenVisible_ReportsTheSelectedCount
// AC-16 / AC-26 (gating stays with the page): Render_WithActions_RendersTheCallerSuppliedActions,
//         Render_WithNoActions_StillRendersTheCountAndDoesNotThrow
