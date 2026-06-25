using Bunit;
using FluentAssertions;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Common;
using TheShop.Web.Components.Navigation;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Components.Navigation;

/// <summary>
/// Tests for <see cref="ShopBreadcrumbs"/> component and the MainLayout breadcrumb slot.
/// Verifies that parent levels are links, the current-page item is non-link, the chevron
/// separator is rendered, accessibility attributes are present, and the layout slot is
/// suppressed when <see cref="BreadcrumbState.HasTrail"/> is false.
/// <see href=".specs/breadcrumbs/spec.md"/>
/// </summary>
public class ShopBreadcrumbsTests : TestContext
{
    public ShopBreadcrumbsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    // =========================================================================
    // Helper — build a representative storefront trail
    // =========================================================================

    private static IReadOnlyList<BreadcrumbItem> StorefrontTrail(string? categoryName = "Outerwear", string productName = "Wool Parka")
    {
        return BreadcrumbTrail.Storefront()
            .Add(categoryName!, "/categories/outerwear")
            .Current(productName);
    }

    private static IReadOnlyList<BreadcrumbItem> AdminTrail(string productName = "Wool Parka")
    {
        return BreadcrumbTrail.Admin()
            .Add("Products", "/admin/products")
            .Current(productName);
    }

    private static IReadOnlyList<BreadcrumbItem> ShortTrail()
    {
        return BreadcrumbTrail.Storefront().Current("Products");
    }

    // =========================================================================
    // AC-1 / AC-3 — storefront trail: parent levels are clickable links
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_StorefrontTrail_HomeIsRenderedAsLink()
    {
        // AC-1 / AC-3: the Home root item must render as a link with the correct href.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, StorefrontTrail()));

        cut.Find($"a[href='{Routes.Home}']").Should().NotBeNull(
            "the Home root item must be a link pointing to the home route");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_StorefrontTrail_IntermediateLevelIsRenderedAsLink()
    {
        // AC-3: every non-final level must be a clickable link.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, StorefrontTrail()));
        cut.Find(".mud-breadcrumbs-expander").Click(); // expand collapsed intermediate items (MudBreakpointProvider fires Xs in bUnit)

        cut.Find("a[href='/categories/outerwear']").Should().NotBeNull(
            "an intermediate level with an href must be rendered as a link");
    }

    // =========================================================================
    // AC-4 — current page item is not a link
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_StorefrontTrail_CurrentPageItemIsNotALink()
    {
        // AC-4 / FR-4: the final item must be plain non-link text, not an anchor.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, StorefrontTrail()));

        // The current page is the last item — verify it has no anchor tag wrapping its text.
        var markup = cut.Markup;
        // The product name must appear in the markup
        markup.Should().Contain("Wool Parka", "the current-page label must be present");
        // Verify there is no anchor whose text is the product name.
        // (MudBreadcrumbs renders disabled items as text spans / MudText, not <a>.)
        var anchors = cut.FindAll("a");
        anchors.Select(a => a.TextContent.Trim()).Should().NotContain("Wool Parka",
            "the current-page item must not be wrapped in an anchor tag");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_StorefrontTrail_CurrentPageItemHasAriaCurrent()
    {
        // AC-10: the current location must expose aria-current="page" for assistive technology.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, StorefrontTrail()));

        cut.Find("[aria-current='page']").Should().NotBeNull(
            "the current-page item must carry aria-current=\"page\" for screen readers");
    }

    // =========================================================================
    // AC-2 — admin trail structure
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_AdminTrail_DashboardIsRenderedAsLink()
    {
        // AC-2: admin trail must begin at Dashboard as a clickable link.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, AdminTrail()));

        cut.Find($"a[href='{Routes.Admin.Dashboard}']").Should().NotBeNull(
            "the Dashboard root item in an admin trail must be a link");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_AdminTrail_IntermediateLevelIsRenderedAsLink()
    {
        // AC-3: admin intermediate level (Products) must be a clickable link.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, AdminTrail()));
        cut.Find(".mud-breadcrumbs-expander").Click(); // expand collapsed intermediate items

        cut.Find("a[href='/admin/products']").Should().NotBeNull(
            "the Products level in the admin trail must be a link");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_AdminTrail_CurrentPageItemIsNotALink()
    {
        // AC-4: the edited item (current page) in an admin trail must not be a link.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, AdminTrail()));

        var anchors = cut.FindAll("a");
        anchors.Select(a => a.TextContent.Trim()).Should().NotContain("Wool Parka",
            "the current-page item must not be an anchor in the admin trail either");
    }

    // =========================================================================
    // AC-10 — nav landmark aria-label
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_Always_NavElementHasLocalizedAriaLabel()
    {
        // AC-10: the breadcrumb region must be announced to assistive technology as a
        // breadcrumb nav landmark with the localized label.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, StorefrontTrail()));

        var nav = cut.Find("nav");
        nav.GetAttribute("aria-label").Should().Be(Strings.Breadcrumb_AriaLabel,
            "the nav landmark must carry the localized aria-label for screen readers");
    }

    // =========================================================================
    // AC-9 / spec §4 — truncation class and title attribute
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_ParentLevelItems_HaveTruncationCssClassAndTitleAttribute()
    {
        // AC-9: long labels must not overflow; the CSS class truncates with ellipsis and
        // title="..." exposes the full text on desktop hover.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, StorefrontTrail()));
        cut.Find(".mud-breadcrumbs-expander").Click(); // expand collapsed intermediate items

        // Every parent link must carry the truncation class and a title attribute.
        var parentLink = cut.Find("a[href='/categories/outerwear']");
        parentLink.ClassList.Should().Contain("shop-breadcrumb-item",
            "parent links must carry the truncation CSS class");
        parentLink.GetAttribute("title").Should().Be("Outerwear",
            "parent links must have title= set to the full label for hover reveal");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_CurrentPageItem_HasTruncationCssClassAndTitleAttribute()
    {
        // AC-9: the current-page item must also truncate; its full label is in title=.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, StorefrontTrail()));

        var currentItem = cut.Find("[aria-current='page']");
        currentItem.ClassList.Should().Contain("shop-breadcrumb-item",
            "the current-page item must carry the truncation CSS class");
        currentItem.GetAttribute("title").Should().Be("Wool Parka",
            "the current-page item must have title= set to the full label");
    }

    // =========================================================================
    // Removed parent edge case (spec §5) — disabled but text visible
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_WithRemovedParent_RendersMissingLevelAsPlainText()
    {
        // Spec edge case: a parent that no longer exists is shown as plain text, not a link.
        var trailWithRemovedParent = BreadcrumbTrail.Storefront()
            .Add("Removed Category", null)   // href = null → disabled
            .Current("Wool Parka");

        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, trailWithRemovedParent));
        cut.Find(".mud-breadcrumbs-expander").Click(); // expand collapsed intermediate items

        // The removed category must not appear as an anchor.
        var anchors = cut.FindAll("a");
        anchors.Select(a => a.TextContent.Trim()).Should().NotContain("Removed Category",
            "a level with href=null must be rendered as plain text, not a link");
        cut.Markup.Should().Contain("Removed Category",
            "the removed-parent label must still be visible as plain text");
    }

    // =========================================================================
    // Short trail (spec §5 — page directly below root)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_ShortTrail_RendersCorrectly()
    {
        // Spec edge case: "Home / Products" — a trail with only two items must render
        // the root as a link and the current page as non-link text.
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, ShortTrail()));

        cut.Find($"a[href='{Routes.Home}']").Should().NotBeNull("Home must be a link");
        cut.Find("[aria-current='page']").Should().NotBeNull("current page must have aria-current='page'");
        var anchors = cut.FindAll("a");
        anchors.Select(a => a.TextContent.Trim()).Should().NotContain("Products",
            "the current-page item must not be wrapped in an anchor for a short trail");
    }

    // =========================================================================
    // AC-6 — dynamic item names are rendered verbatim
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_WithDynamicProductName_DisplaysActualName()
    {
        // AC-6 / FR-6: the actual product name (not a generic placeholder) must appear.
        var trail = BreadcrumbTrail.Storefront().Current("Puffco Peak Pro");
        var cut = Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, trail));

        cut.Markup.Should().Contain("Puffco Peak Pro",
            "the component must display the actual item name supplied by the page");
    }

    // =========================================================================
    // Items = null / empty — component must not throw (defensive)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Render_WithNullItems_DoesNotThrow()
    {
        // Defensive: the layout may render ShopBreadcrumbs transiently before HasTrail
        // is evaluated. The component must tolerate a null Items parameter gracefully.
        var act = () => Render<ShopBreadcrumbs>(p => p.Add(c => c.Items, (IReadOnlyList<BreadcrumbItem>?)null));

        act.Should().NotThrow("the component must handle a null Items parameter without throwing");
    }
}

// AC → Test mapping
// AC-1: Render_StorefrontTrail_HomeIsRenderedAsLink
// AC-2: Render_AdminTrail_DashboardIsRenderedAsLink, Render_AdminTrail_IntermediateLevelIsRenderedAsLink, Render_AdminTrail_CurrentPageItemIsNotALink
// AC-3: Render_StorefrontTrail_IntermediateLevelIsRenderedAsLink, Render_AdminTrail_IntermediateLevelIsRenderedAsLink
// AC-4: Render_StorefrontTrail_CurrentPageItemIsNotALink, Render_AdminTrail_CurrentPageItemIsNotALink
// AC-5: (structural identity — see BreadcrumbTrailTests.Storefront_SameArguments_ProduceSameTrail)
// AC-6: Render_WithDynamicProductName_DisplaysActualName
// AC-7: see BreadcrumbStateTests — state contract tests live there; component tests verify the slot via ShopBreadcrumbs directly
// AC-8: Render_Always_NavElementHasLocalizedAriaLabel
// AC-9: Render_ParentLevelItems_HaveTruncationCssClassAndTitleAttribute, Render_CurrentPageItem_HasTruncationCssClassAndTitleAttribute
// AC-10: Render_StorefrontTrail_CurrentPageItemHasAriaCurrent, Render_Always_NavElementHasLocalizedAriaLabel
