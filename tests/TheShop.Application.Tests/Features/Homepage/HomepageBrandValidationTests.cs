using FluentAssertions;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageBrand;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HomepageBrandValidationTests
{
    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Create_BlankFieldsAndVisibleEmptySelection_ReportsAllFields()
    {
        var result = new CreateHomepageBrandCommandValidator().Validate(new CreateHomepageBrandCommand(" ", " ", " ", [], true));
        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(["Title", "ButtonLabel", "ButtonLink", "BrandIds"]);
        result.Errors.Should().Contain(error => error.PropertyName == "BrandIds" && error.ErrorMessage == HomepageBrandErrorKeys.SelectionInvalid);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Update_HiddenEmptyOrVisibleSix_AcceptsNormalizedFields()
    {
        var validator = new UpdateHomepageBrandCommandValidator();
        var hidden = new UpdateHomepageBrandCommand(" Title ", " View ", " /brands ", [], false);
        validator.Validate(hidden).IsValid.Should().BeTrue();
        hidden.Title.Should().Be("Title");
        hidden.ButtonLabel.Should().Be("View");
        hidden.ButtonLink.Should().Be("/brands");
        validator.Validate(new UpdateHomepageBrandCommand("Title", "View", "https://shop.test/brands", Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray(), true))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Create_DuplicateSeventhEmptyIdAndUnsafeLink_Rejects()
    {
        var first = Guid.NewGuid();
        var validator = new CreateHomepageBrandCommandValidator();
        foreach (var ids in new Guid[][] { [first, first], [Guid.Empty], Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray() })
            validator.Validate(new CreateHomepageBrandCommand("Title", "View", "/brands", ids, false)).Errors
                .Should().Contain(error => error.PropertyName == "BrandIds");
        validator.Validate(new CreateHomepageBrandCommand("Title", "View", "//shop.test", [first], true)).Errors
            .Should().Contain(error => error.PropertyName == "ButtonLink" && error.ErrorMessage == HomepageBrandErrorKeys.ButtonLinkInvalid);
    }
}
