using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

public sealed class FreshDropsTests
{
    private static FreshDrops Create() => FreshDrops.Create("Fresh Drops", "View All Products", "/products",
        FreshDropsSelectionMode.Newest, [], true);

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Create_ValidDefaults_PreservesSingletonContentAndVisibility()
    {
        var drops = Create();
        drops.Id.Should().Be(1);
        drops.Title.Should().Be("Fresh Drops");
        drops.ButtonLabel.Should().Be("View All Products");
        drops.ButtonLink.Should().Be("/products");
        drops.SelectionMode.Should().Be(FreshDropsSelectionMode.Newest);
        drops.ManualProductIds.Should().BeEmpty();
        drops.IsVisible.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void ChangeContent_BlankTitleOrIncompleteButton_RejectsWithoutMutation()
    {
        var drops = Create();
        var blank = () => drops.ChangeContent(" ", null, null);
        var incomplete = () => drops.ChangeContent("Changed", "Shop", null);
        blank.Should().Throw<DomainException>().Where(ex => ex.MessageKey == "FreshDrops_TitleRequired");
        incomplete.Should().Throw<DomainException>().Where(ex => ex.MessageKey == "FreshDrops_ButtonPairRequired");
        drops.Title.Should().Be("Fresh Drops");
        drops.ButtonLink.Should().Be("/products");
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void ChangeContent_BlankButtonPair_ClearsButtonAndTrimsTitle()
    {
        var drops = Create();
        drops.ChangeContent("  New title  ", "  ", " ");
        drops.Title.Should().Be("New title");
        drops.ButtonLabel.Should().BeNull();
        drops.ButtonLink.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void ChangeContent_SafeAndUnsafeLinks_EnforcesDestinationRules()
    {
        var drops = Create();
        drops.ChangeContent("Title", "Shop", "https://example.com/products");
        drops.ButtonLink.Should().Be("https://example.com/products");
        foreach (var link in new[] { "//example.com", "javascript:alert(1)", "\\products", "/bad path", "ftp://example.com" })
        {
            var action = () => drops.ChangeContent("Title", "Shop", link);
            action.Should().Throw<DomainException>().Where(ex => ex.MessageKey == "FreshDrops_ButtonLinkInvalid");
        }
        drops.ButtonLink.Should().Be("https://example.com/products");
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void ChangeSelection_ManualOrderAndNewestSave_RetainsOrderThenClearsIds()
    {
        var drops = Create();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        drops.ChangeSelection(FreshDropsSelectionMode.Manual, [b, a]);
        drops.ManualProductIds.Should().Equal(b, a);
        drops.ChangeSelection(FreshDropsSelectionMode.Newest, [b, a]);
        drops.ManualProductIds.Should().BeEmpty();
        drops.SelectionMode.Should().Be(FreshDropsSelectionMode.Newest);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void ChangeSelection_DuplicateFourthEmptyOrInvalidMode_PreservesPriorOrder()
    {
        var drops = Create();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        drops.ChangeSelection(FreshDropsSelectionMode.Manual, [a, b]);
        foreach (var ids in new[] { new[] { a, a }, new[] { a, b, Guid.NewGuid(), Guid.NewGuid() }, new[] { Guid.Empty } })
        {
            var action = () => drops.ChangeSelection(FreshDropsSelectionMode.Manual, ids);
            action.Should().Throw<DomainException>().Where(ex => ex.MessageKey == "FreshDrops_SelectionInvalid");
        }
        var invalidMode = () => drops.ChangeSelection((FreshDropsSelectionMode)99, [a]);
        invalidMode.Should().Throw<DomainException>();
        drops.ManualProductIds.Should().Equal(a, b);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void SetVisibility_HiddenThenVisible_ChangesOnlyStatus()
    {
        var drops = Create();
        drops.SetVisibility(false);
        drops.IsVisible.Should().BeFalse();
        drops.SetVisibility(true);
        drops.IsVisible.Should().BeTrue();
        drops.Title.Should().Be("Fresh Drops");
    }
}
