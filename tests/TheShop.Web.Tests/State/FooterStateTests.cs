using FluentAssertions;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.State;

/// <summary>
/// Tests for <see cref="FooterState"/> scoped store.
/// Verifies the Visible / Hide / Show / OnChange contract used by MainLayout to render
/// the footer slot by default and let a page opt out, with visibility reset on navigation.
/// <see href=".specs/footer/spec.md"/>
/// </summary>
public class FooterStateTests
{
    // =========================================================================
    // Initial state — footer is shown by default (opt-out model)
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void OnConstruction_VisibleIsTrue()
    {
        var state = new FooterState();

        state.Visible.Should().BeTrue("the footer is shown by default; pages opt out explicitly");
    }

    // =========================================================================
    // Hide — suppresses the footer and fires OnChange
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Hide_WhenVisible_SetsVisibleFalse()
    {
        var state = new FooterState();

        state.Hide();

        state.Visible.Should().BeFalse("Hide must suppress the footer for the current page");
    }

    [Fact]
    [Trait("Feature", "footer")]
    public void Hide_WhenVisible_FiresOnChange()
    {
        var state = new FooterState();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Hide();

        changeCount.Should().Be(1, "Hide must notify subscribers so the layout re-renders");
    }

    [Fact]
    [Trait("Feature", "footer")]
    public void Hide_WhenAlreadyHidden_IsIdempotentAndDoesNotFire()
    {
        var state = new FooterState();
        state.Hide();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Hide();

        state.Visible.Should().BeFalse();
        changeCount.Should().Be(0, "a redundant Hide must not fire OnChange");
    }

    // =========================================================================
    // Show — restores the footer (called by the layout on navigation)
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Show_AfterHide_SetsVisibleTrue()
    {
        var state = new FooterState();
        state.Hide();

        state.Show();

        state.Visible.Should().BeTrue("Show restores the footer so the next page starts visible");
    }

    [Fact]
    [Trait("Feature", "footer")]
    public void Show_AfterHide_FiresOnChange()
    {
        var state = new FooterState();
        state.Hide();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Show();

        changeCount.Should().Be(1, "Show must notify subscribers so the layout re-renders");
    }

    [Fact]
    [Trait("Feature", "footer")]
    public void Show_WhenAlreadyVisible_IsIdempotentAndDoesNotFire()
    {
        var state = new FooterState();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Show();

        state.Visible.Should().BeTrue();
        changeCount.Should().Be(0, "a redundant Show must not fire OnChange");
    }

    // =========================================================================
    // OnChange fires once per real transition
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void OnChange_WhenHideAndShowCalledAlternately_FiresEachTime()
    {
        var state = new FooterState();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        state.Hide();
        state.Show();
        state.Hide();

        changeCount.Should().Be(3, "each real visibility transition must fire OnChange exactly once");
    }
}
