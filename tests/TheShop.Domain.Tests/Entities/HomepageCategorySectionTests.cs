using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

public sealed class HomepageCategorySectionTests
{
    private static readonly Guid[] Ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    private static HomepageCategorySection Section() =>
        HomepageCategorySection.Create(" Discover by Category ", " View All Categories ", " /categories ", Ids, true);

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Create_CompleteConfiguration_TrimsFieldsAndPreservesOrder()
    {
        var section = Section();
        section.Id.Should().Be(1);
        section.Title.Should().Be("Discover by Category");
        section.ButtonLabel.Should().Be("View All Categories");
        section.ButtonLink.Should().Be("/categories");
        section.CategoryIds.Should().Equal(Ids);
        section.IsVisible.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Create_MissingRequiredFieldsAndUnsafeLink_Rejects()
    {
        Action title = () => HomepageCategorySection.Create(" ", "View", "/categories", Ids, true);
        Action label = () => HomepageCategorySection.Create("Title", " ", "/categories", Ids, true);
        Action link = () => HomepageCategorySection.Create("Title", "View", " ", Ids, true);
        title.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageCategory_TitleRequired");
        label.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageCategory_ButtonLabelRequired");
        link.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageCategory_ButtonLinkRequired");
        foreach (var unsafeLink in new[] { "//example.com", "javascript:alert(1)", "/bad path", "\\bad" })
        {
            Action unsafeAction = () => HomepageCategorySection.Create("Title", "View", unsafeLink, Ids, true);
            unsafeAction.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageCategory_ButtonLinkInvalid");
        }
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Create_FewerFourthDuplicateOrEmptyCategory_Rejects()
    {
        foreach (var ids in new[]
        {
            Array.Empty<Guid>(), [Ids[0], Ids[1]], [Ids[0], Ids[1], Ids[2], Guid.NewGuid()],
            [Ids[0], Ids[0], Ids[2]], [Ids[0], Guid.Empty, Ids[2]]
        })
        {
            Action action = () => HomepageCategorySection.Create("Title", "View", "/categories", ids, true);
            action.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageCategory_SelectionInvalid");
        }
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Update_InvalidReplacementPreservesSavedValuesThenValidReplacementChangesAll()
    {
        var section = Section();
        Action invalid = () => section.Update("Changed", "Shop", "/products", [Ids[0], Ids[0], Ids[2]], false);
        invalid.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageCategory_SelectionInvalid");
        section.Title.Should().Be("Discover by Category");
        section.CategoryIds.Should().Equal(Ids);
        section.IsVisible.Should().BeTrue();

        section.Update(" New title ", " Shop ", " https://example.com/categories ", [Ids[2], Ids[1], Ids[0]], false);
        section.Title.Should().Be("New title");
        section.ButtonLabel.Should().Be("Shop");
        section.ButtonLink.Should().Be("https://example.com/categories");
        section.CategoryIds.Should().Equal(Ids[2], Ids[1], Ids[0]);
        section.IsVisible.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void SetVisibility_HiddenThenVisible_LeavesSelectionAndContentIntact()
    {
        var section = Section();
        section.SetVisibility(false);
        section.IsVisible.Should().BeFalse();
        section.SetVisibility(true);
        section.IsVisible.Should().BeTrue();
        section.CategoryIds.Should().Equal(Ids);
        section.Title.Should().Be("Discover by Category");
    }
}
