using FluentAssertions;
using MudBlazor;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Common;

/// <summary>
/// Tests for <see cref="BreadcrumbTrail"/> fluent builder.
/// The builder is the structural heart of the feature: it constructs trails from the
/// fixed site hierarchy and is the sole source of root labels, hrefs, and the
/// disabled-item convention. Every spec constraint expressed through the builder
/// is verified here.
/// <see href=".specs/breadcrumbs/spec.md"/>
/// </summary>
public class BreadcrumbTrailTests
{
    // =========================================================================
    // Storefront() — root seeding (AC-1, FR-1, FR-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Storefront_RootItem_HasHomeLabel()
    {
        // FR-1 / spec §4: the first item of a storefront trail is always Home.
        var trail = BreadcrumbTrail.Storefront().Current("Products");

        trail[0].Text.Should().Be(Strings.Nav_Home);
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Storefront_RootItem_HasHomeHref()
    {
        // AC-3: every non-final level must be a link to its page.
        var trail = BreadcrumbTrail.Storefront().Current("Products");

        trail[0].Href.Should().Be(Routes.Home);
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Storefront_RootItem_IsEnabled()
    {
        // Home is a parent level — it must be clickable (AC-3).
        var trail = BreadcrumbTrail.Storefront().Current("Products");

        trail[0].Disabled.Should().BeFalse("the Home root must be an enabled, clickable link");
    }

    // =========================================================================
    // Admin() — root seeding (AC-2, FR-2, FR-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Admin_RootItem_HasDashboardLabel()
    {
        // FR-2 / spec §4: the first item of an admin trail is always the Dashboard.
        var trail = BreadcrumbTrail.Admin().Current("Products");

        trail[0].Text.Should().Be(Strings.Nav_Dashboard);
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Admin_RootItem_HasDashboardHref()
    {
        // AC-3: Dashboard root must link to the admin dashboard route.
        var trail = BreadcrumbTrail.Admin().Current("Products");

        trail[0].Href.Should().Be(Routes.Admin.Dashboard);
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Admin_RootItem_IsEnabled()
    {
        // Dashboard is a parent level — it must be clickable.
        var trail = BreadcrumbTrail.Admin().Current("Products");

        trail[0].Disabled.Should().BeFalse("the Dashboard root must be an enabled, clickable link");
    }

    // =========================================================================
    // Current() — final item (AC-4, FR-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Current_FinalItem_HasSuppliedText()
    {
        // AC-6: a trail level for a specific item shows that item's actual name.
        var trail = BreadcrumbTrail.Storefront().Current("Wool Parka");

        trail[^1].Text.Should().Be("Wool Parka");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Current_FinalItem_IsDisabled()
    {
        // AC-4 / FR-4: the current page is the last item and is never a link.
        var trail = BreadcrumbTrail.Storefront().Current("Wool Parka");

        trail[^1].Disabled.Should().BeTrue("the current-page item must be disabled (non-link)");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Current_FinalItem_HasNullHref()
    {
        var trail = BreadcrumbTrail.Storefront().Current("Wool Parka");

        trail[^1].Href.Should().BeNull("the current-page item must have no href");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Current_ReturnsList_WhoseCountIsRootPlusOne()
    {
        // Storefront() seeds one root; Current() appends one item → total = 2.
        var trail = BreadcrumbTrail.Storefront().Current("Products");

        trail.Should().HaveCount(2);
    }

    // =========================================================================
    // Add() — intermediate levels (AC-3, AC-6, FR-3, FR-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Add_WithHref_AddsEnabledItemWithCorrectTextAndHref()
    {
        // AC-3 / AC-6: an intermediate level must be a link with the supplied text.
        var trail = BreadcrumbTrail.Storefront()
            .Add("Outerwear", "/categories/outerwear")
            .Current("Wool Parka");

        var intermediate = trail[1];
        intermediate.Text.Should().Be("Outerwear");
        intermediate.Href.Should().Be("/categories/outerwear");
        intermediate.Disabled.Should().BeFalse("a level with an href must be enabled");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Add_WithNullHref_AddsDisabledItemWithCorrectText()
    {
        // Spec edge case: a parent that no longer exists is shown as plain text (href = null).
        var trail = BreadcrumbTrail.Storefront()
            .Add("Removed Category", null)
            .Current("Wool Parka");

        var removedLevel = trail[1];
        removedLevel.Text.Should().Be("Removed Category");
        removedLevel.Href.Should().BeNull();
        removedLevel.Disabled.Should().BeTrue("a level with href=null must be disabled (non-link)");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Add_MultipleIntermediateLevels_PreservesOrderAndCount()
    {
        // Spec Behavior 1: Home / Categories / Outerwear / Wool Parka
        var trail = BreadcrumbTrail.Storefront()
            .Add("Categories", "/categories")
            .Add("Outerwear", "/categories/outerwear")
            .Current("Wool Parka");

        trail.Should().HaveCount(4);
        trail[0].Text.Should().Be(Strings.Nav_Home);
        trail[1].Text.Should().Be("Categories");
        trail[2].Text.Should().Be("Outerwear");
        trail[3].Text.Should().Be("Wool Parka");
    }

    // =========================================================================
    // Storefront trail ordering — spec Behavior 1
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Storefront_DeepCategoryTrail_HasCorrectStructure()
    {
        // Full trail: Home / Categories / Outerwear / Wool Parka
        // Home and intermediate levels are enabled links; product is disabled.
        var trail = BreadcrumbTrail.Storefront()
            .Add("Categories", "/categories")
            .Add("Outerwear", "/categories/outerwear")
            .Current("Wool Parka");

        trail[0].Disabled.Should().BeFalse("Home must be a link");
        trail[1].Disabled.Should().BeFalse("Categories must be a link");
        trail[2].Disabled.Should().BeFalse("Outerwear must be a link");
        trail[3].Disabled.Should().BeTrue("Wool Parka (current page) must not be a link");
    }

    // =========================================================================
    // Admin trail — spec Behavior 3
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Admin_DeepTrail_HasCorrectStructure()
    {
        // Spec Behavior 3: Dashboard / Products / Wool Parka
        var trail = BreadcrumbTrail.Admin()
            .Add("Products", "/admin/products")
            .Current("Wool Parka");

        trail.Should().HaveCount(3);
        trail[0].Text.Should().Be(Strings.Nav_Dashboard);
        trail[0].Disabled.Should().BeFalse("Dashboard must be a link");
        trail[1].Text.Should().Be("Products");
        trail[1].Disabled.Should().BeFalse("Products must be a link");
        trail[2].Text.Should().Be("Wool Parka");
        trail[2].Disabled.Should().BeTrue("Wool Parka (current page) must not be a link");
    }

    // =========================================================================
    // Uncategorized product fallback (spec §4 constraint)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Storefront_UncategorizedProductFallback_HasHomeProductsAndProductName()
    {
        // Spec §4: a product that belongs to no category falls back to
        // Home → Products → {Product}.
        var trail = BreadcrumbTrail.Storefront()
            .Add(Strings.Nav_Products, Routes.Products)
            .Current("Puffco Peak Pro");

        trail.Should().HaveCount(3);
        trail[0].Text.Should().Be(Strings.Nav_Home);
        trail[1].Text.Should().Be(Strings.Nav_Products);
        trail[1].Href.Should().Be(Routes.Products);
        trail[1].Disabled.Should().BeFalse();
        trail[2].Text.Should().Be("Puffco Peak Pro");
        trail[2].Disabled.Should().BeTrue();
    }

    // =========================================================================
    // Structural identity — AC-5 (same input, same trail regardless of nav history)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Storefront_SameArguments_ProduceSameTrail()
    {
        // AC-5: the trail is derived from the hierarchy, not from navigation history.
        // Two calls with identical arguments must produce identical trails.
        var trail1 = BreadcrumbTrail.Storefront()
            .Add("Outerwear", "/categories/outerwear")
            .Current("Wool Parka");

        var trail2 = BreadcrumbTrail.Storefront()
            .Add("Outerwear", "/categories/outerwear")
            .Current("Wool Parka");

        trail1.Select(i => (i.Text, i.Href, i.Disabled))
            .Should().Equal(trail2.Select(i => (i.Text, i.Href, i.Disabled)),
                "structural trails built from the same inputs must be identical regardless of call sequence");
    }

    // =========================================================================
    // Build() — returns items built so far without a current-page crumb
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Build_WithoutCurrentCall_ReturnsItemsAddedSoFar()
    {
        var trail = BreadcrumbTrail.Storefront()
            .Add("Categories", "/categories")
            .Build();

        trail.Should().HaveCount(2, "Build() must return everything chained before it");
        trail[0].Text.Should().Be(Strings.Nav_Home);
        trail[1].Text.Should().Be("Categories");
    }

    // =========================================================================
    // Page directly below root (short trail edge case — spec §5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Storefront_PageDirectlyBelowHome_ProducesTwoItemTrail()
    {
        // Spec edge case: "Home / Products" — no intermediate levels.
        var trail = BreadcrumbTrail.Storefront().Current("Products");

        trail.Should().HaveCount(2);
        trail[0].Text.Should().Be(Strings.Nav_Home);
        trail[1].Text.Should().Be("Products");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Admin_PageDirectlyBelowDashboard_ProducesTwoItemTrail()
    {
        // Admin short trail: Dashboard / Products
        var trail = BreadcrumbTrail.Admin().Current("Products");

        trail.Should().HaveCount(2);
        trail[0].Text.Should().Be(Strings.Nav_Dashboard);
        trail[1].Text.Should().Be("Products");
    }
}

// AC → Test mapping
// AC-1: Storefront_RootItem_HasHomeLabel, Storefront_RootItem_HasHomeHref, Storefront_DeepCategoryTrail_HasCorrectStructure
// AC-2: Admin_RootItem_HasDashboardLabel, Admin_RootItem_HasDashboardHref, Admin_DeepTrail_HasCorrectStructure
// AC-3: Add_WithHref_AddsEnabledItemWithCorrectTextAndHref, Storefront_RootItem_IsEnabled, Admin_RootItem_IsEnabled
// AC-4: Current_FinalItem_IsDisabled, Current_FinalItem_HasNullHref
// AC-5: Storefront_SameArguments_ProduceSameTrail
// AC-6: Current_FinalItem_HasSuppliedText, Add_WithHref_AddsEnabledItemWithCorrectTextAndHref
// AC-7: (no trail on root — see BreadcrumbStateTests and ShopBreadcrumbsTests)
// AC-8: Storefront_RootItem_HasHomeLabel (Strings.Nav_Home), Admin_RootItem_HasDashboardLabel (Strings.Nav_Dashboard)
// AC-9: (truncation — see ShopBreadcrumbsTests)
// AC-10: (aria attributes — see ShopBreadcrumbsTests)
