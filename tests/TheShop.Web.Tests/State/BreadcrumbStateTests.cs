using FluentAssertions;
using MudBlazor;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.State;

/// <summary>
/// Tests for <see cref="BreadcrumbState"/> scoped store.
/// Verifies Set / Clear / HasTrail / OnChange contract used by MainLayout to render
/// the breadcrumb slot only when a trail is active (AC-7).
/// <see href=".specs/breadcrumbs/spec.md"/>
/// </summary>
public class BreadcrumbStateTests
{
    // =========================================================================
    // Initial state
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void OnConstruction_TrailIsEmpty()
    {
        var state = new BreadcrumbState();

        state.Trail.Should().BeEmpty("a freshly constructed store must have an empty trail");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void OnConstruction_HasTrailIsFalse()
    {
        // AC-7: no trail on root pages — the store must default to no-trail
        var state = new BreadcrumbState();

        state.HasTrail.Should().BeFalse("HasTrail must be false when no trail has been set");
    }

    // =========================================================================
    // HasTrail — depends on item count
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void HasTrail_WhenTrailHasSingleItem_ReturnsFalse()
    {
        // A trail of exactly one item (only a root) is not considered a meaningful trail —
        // the layout should not render breadcrumbs in this state (spec §4: root pages show no trail).
        var state = new BreadcrumbState();
        state.Set([new BreadcrumbItem("Home", "/")]);

        state.HasTrail.Should().BeFalse("a single-item trail has no parent levels and must not show");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void HasTrail_WhenTrailHasTwoOrMoreItems_ReturnsTrue()
    {
        // AC-1 / AC-2: once a page supplies a full trail (root + current), HasTrail must be true.
        var state = new BreadcrumbState();
        state.Set([
            new BreadcrumbItem("Home", "/"),
            new BreadcrumbItem("Products", null, disabled: true)
        ]);

        state.HasTrail.Should().BeTrue("two items constitute a genuine trail that should be rendered");
    }

    // =========================================================================
    // Set — stores the trail and fires OnChange
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Set_WithItems_StoresAllItems()
    {
        var state = new BreadcrumbState();
        var items = new List<BreadcrumbItem>
        {
            new("Home", "/"),
            new("Products", "/products"),
            new("Wool Parka", null, disabled: true)
        };

        state.Set(items);

        state.Trail.Should().HaveCount(3);
        state.Trail[0].Text.Should().Be("Home");
        state.Trail[1].Text.Should().Be("Products");
        state.Trail[2].Text.Should().Be("Wool Parka");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Set_WhenCalled_FiresOnChange()
    {
        var state = new BreadcrumbState();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Products", null, disabled: true)]);

        changeCount.Should().Be(1, "Set must notify subscribers so the layout re-renders");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Set_CalledRepeatedly_OverwritesPreviousTrail()
    {
        var state = new BreadcrumbState();
        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Products", null, disabled: true)]);
        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Wool Parka", null, disabled: true)]);

        state.Trail.Should().HaveCount(2);
        state.Trail[1].Text.Should().Be("Wool Parka", "the second Set must overwrite the first trail");
    }

    // =========================================================================
    // Clear — resets the trail and fires OnChange
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Clear_AfterSet_MakesTrailEmpty()
    {
        var state = new BreadcrumbState();
        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Products", null, disabled: true)]);

        state.Clear();

        state.Trail.Should().BeEmpty("Clear must remove all items from the trail");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Clear_AfterSet_HasTrailReturnsFalse()
    {
        // AC-7 runtime: navigating away (which triggers Clear) must suppress the slot
        var state = new BreadcrumbState();
        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Products", null, disabled: true)]);

        state.Clear();

        state.HasTrail.Should().BeFalse("HasTrail must be false after Clear so the slot disappears");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Clear_WhenCalled_FiresOnChange()
    {
        var state = new BreadcrumbState();
        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Products", null, disabled: true)]);
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Clear();

        changeCount.Should().Be(1, "Clear must notify subscribers so the layout re-renders");
    }

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void Clear_WhenCalledOnAlreadyEmptyState_IsIdempotent()
    {
        var state = new BreadcrumbState();
        var act = () =>
        {
            state.Clear();
            state.Clear();
        };

        act.Should().NotThrow("calling Clear on an already-empty store must not throw");
        state.Trail.Should().BeEmpty();
    }

    // =========================================================================
    // OnChange fires multiple times correctly
    // =========================================================================

    [Fact]
    [Trait("Feature", "breadcrumbs")]
    public void OnChange_WhenSetAndClearCalledAlternately_FiresEachTime()
    {
        var state = new BreadcrumbState();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Products", null, disabled: true)]);
        state.Clear();
        state.Set([new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Brands", null, disabled: true)]);

        changeCount.Should().Be(3, "each Set and Clear must fire OnChange exactly once");
    }
}

// AC → Test mapping
// AC-1: OnConstruction_HasTrailIsFalse (state must default false), HasTrail_WhenTrailHasTwoOrMoreItems_ReturnsTrue (set makes it true)
// AC-2: (see BreadcrumbTrailTests — Admin root), (see ShopBreadcrumbsTests — admin trail)
// AC-3: (see BreadcrumbTrailTests — Add with href), (see ShopBreadcrumbsTests — intermediate links)
// AC-4: (see BreadcrumbTrailTests — Current disabled), (see ShopBreadcrumbsTests — current not link)
// AC-5: (see BreadcrumbTrailTests — SameArguments)
// AC-6: (see BreadcrumbTrailTests — Current text), (see ShopBreadcrumbsTests — dynamic name)
// AC-7: OnConstruction_HasTrailIsFalse, Clear_AfterSet_HasTrailReturnsFalse
// AC-8: (see BreadcrumbTrailTests — Strings.*), (see ShopBreadcrumbsTests — aria-label)
// AC-9: (see ShopBreadcrumbsTests — truncation class + title)
// AC-10: (see ShopBreadcrumbsTests — aria-current, nav aria-label)
