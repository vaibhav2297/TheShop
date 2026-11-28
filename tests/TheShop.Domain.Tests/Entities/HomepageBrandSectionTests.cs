using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

public sealed class HomepageBrandSectionTests
{
    private static readonly Guid[] Ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Create_VisibleSelection_TrimsFieldsAndPreservesOrder()
    {
        var section = HomepageBrandSection.Create(" Shop by Brand ", " Browse ", " /brands ", [Ids[2], Ids[0]], true);
        section.Id.Should().Be(1);
        section.Title.Should().Be("Shop by Brand");
        section.ButtonLabel.Should().Be("Browse");
        section.ButtonLink.Should().Be("/brands");
        section.BrandIds.Should().Equal(Ids[2], Ids[0]);
        section.IsVisible.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Create_HiddenEmptySelection_AcceptsZeroButVisibleRequiresOne()
    {
        var hidden = HomepageBrandSection.Create("Title", "View", "https://shop.test/brands", [], false);
        hidden.BrandIds.Should().BeEmpty();
        hidden.IsVisible.Should().BeFalse();
        Action visible = () => HomepageBrandSection.Create("Title", "View", "/brands", [], true);
        visible.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageBrand_SelectionInvalid");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Create_DuplicateEmptyOrSeventhBrand_Rejects()
    {
        foreach (var selected in new Guid[][] { [Ids[0], Ids[0]], [Guid.Empty], Ids })
        {
            Action action = () => HomepageBrandSection.Create("Title", "View", "/brands", selected, false);
            action.Should().Throw<DomainException>().Which.MessageKey.Should().Be("HomepageBrand_SelectionInvalid");
        }
        HomepageBrandSection.Create("Title", "View", "/brands", Ids.Take(6), true).BrandIds.Should().HaveCount(6);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Create_MissingFieldsOrUnsafeLink_Rejects()
    {
        foreach (var (title, label, link, key) in new[]
        {
            (" ", "View", "/brands", "HomepageBrand_TitleRequired"),
            ("Title", " ", "/brands", "HomepageBrand_ButtonLabelRequired"),
            ("Title", "View", " ", "HomepageBrand_ButtonLinkRequired"),
            ("Title", "View", "javascript:alert(1)", "HomepageBrand_ButtonLinkInvalid"),
            ("Title", "View", "//shop.test", "HomepageBrand_ButtonLinkInvalid"),
            ("Title", "View", "/bad path", "HomepageBrand_ButtonLinkInvalid")
        })
        {
            Action action = () => HomepageBrandSection.Create(title, label, link, [Ids[0]], true);
            action.Should().Throw<DomainException>().Which.MessageKey.Should().Be(key);
        }
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Update_InvalidReplacementPreservesAllFieldsThenValidReplacementChangesTogether()
    {
        var section = HomepageBrandSection.Create("Saved", "View", "/brands", [Ids[0], Ids[1]], true);
        Action invalid = () => section.Update("Changed", "Explore", "/new", [Ids[0], Ids[0]], false);
        invalid.Should().Throw<DomainException>();
        section.Title.Should().Be("Saved");
        section.ButtonLabel.Should().Be("View");
        section.ButtonLink.Should().Be("/brands");
        section.BrandIds.Should().Equal(Ids[0], Ids[1]);
        section.IsVisible.Should().BeTrue();

        section.Update(" Changed ", " Explore ", " https://shop.test/new ", [Ids[1], Ids[0]], false);
        section.Title.Should().Be("Changed");
        section.ButtonLabel.Should().Be("Explore");
        section.ButtonLink.Should().Be("https://shop.test/new");
        section.BrandIds.Should().Equal(Ids[1], Ids[0]);
        section.IsVisible.Should().BeFalse();
    }
}
