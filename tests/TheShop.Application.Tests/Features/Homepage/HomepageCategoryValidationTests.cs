using FluentAssertions;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageCategory;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageCategory;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HomepageCategoryValidationTests
{
    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Create_BlankFieldsAndUnderfilledSelection_ReportsEachField()
    {
        var command = new CreateHomepageCategoryCommand(" ", " ", " ", [Guid.NewGuid()], true);
        var result = new CreateHomepageCategoryCommandValidator().Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(["Title", "ButtonLabel", "ButtonLink", "CategoryIds"]);
        result.Errors.Should().Contain(error =>
            error.PropertyName == "CategoryIds" && error.ErrorMessage == HomepageCategoryErrorKeys.SelectionInvalid);
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Update_ThreeDistinctCategoriesAndSafeDestination_AcceptsNormalizedInput()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new UpdateHomepageCategoryCommand(" Title ", " Shop ", " /categories ", ids, false);
        new UpdateHomepageCategoryCommandValidator().Validate(command).IsValid.Should().BeTrue();
        command.Title.Should().Be("Title");
        command.ButtonLabel.Should().Be("Shop");
        command.ButtonLink.Should().Be("/categories");
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Create_DuplicateFourthEmptyCategoryAndUnsafeDestination_Rejects()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var validator = new CreateHomepageCategoryCommandValidator();
        foreach (var selected in new[]
        {
            new[] { ids[0], ids[0], ids[2] },
            new[] { ids[0], ids[1], ids[2], Guid.NewGuid() },
            new[] { ids[0], ids[1], Guid.Empty }
        })
            validator.Validate(new CreateHomepageCategoryCommand("Title", "Shop", "/categories", selected, true)).Errors
                .Should().Contain(error => error.PropertyName == "CategoryIds");
        validator.Validate(new CreateHomepageCategoryCommand("Title", "Shop", "//example.com", ids, true)).Errors
            .Should().Contain(error => error.PropertyName == "ButtonLink"
                && error.ErrorMessage == HomepageCategoryErrorKeys.ButtonLinkInvalid);
    }
}
