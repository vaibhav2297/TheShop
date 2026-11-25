using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageCategory;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageCategory;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryChoices;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryForAdmin;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HomepageCategoryHandlerTests
{
    private static readonly Guid[] Ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
    private static Category Category(Guid id, string name, bool active = true, string? image = null) =>
        TheShop.Domain.Entities.Category.Rehydrate(id, name, imagePath: image, isActive: active);

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Create_ThreeActiveCategories_SavesOneCompleteOrderedSection()
    {
        var repository = Substitute.For<IHomepageCategoryRepository>();
        var categories = Substitute.For<ICategoryRepository>();
        foreach (var id in Ids) categories.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Category(id, id.ToString()));
        repository.SaveAsync(Arg.Any<HomepageCategorySection>(), true, Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var result = await new CreateHomepageCategoryHandler(repository, categories)
            .Handle(new(" Title ", " View ", " /categories ", Ids, true), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).SaveAsync(Arg.Is<HomepageCategorySection>(section =>
            section.Id == 1 && section.Title == "Title" && section.ButtonLabel == "View" &&
            section.ButtonLink == "/categories" && section.CategoryIds.SequenceEqual(Ids) && section.IsVisible),
            true, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Update_InactiveOrDeletedCategory_RejectsWithoutWriting()
    {
        var repository = Substitute.For<IHomepageCategoryRepository>();
        var categories = Substitute.For<ICategoryRepository>();
        categories.GetByIdAsync(Ids[0], Arg.Any<CancellationToken>()).Returns(Category(Ids[0], "First"));
        categories.GetByIdAsync(Ids[1], Arg.Any<CancellationToken>()).Returns(Category(Ids[1], "Inactive", active: false));
        var result = await new UpdateHomepageCategoryHandler(repository, categories)
            .Handle(new("Title", "View", "/categories", Ids, false), TestContext.Current.CancellationToken);
        result.Error.Should().Be(HomepageCategoryErrorKeys.CategoryUnavailable);
        await repository.DidNotReceive().SaveAsync(Arg.Any<HomepageCategorySection>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Update_HiddenSection_PreservesOrderAndRepositoryConflict()
    {
        var repository = Substitute.For<IHomepageCategoryRepository>();
        var categories = Substitute.For<ICategoryRepository>();
        foreach (var id in Ids) categories.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Category(id, id.ToString()));
        repository.SaveAsync(Arg.Any<HomepageCategorySection>(), false, Arg.Any<CancellationToken>())
            .Returns(Result.Fail(HomepageCategoryErrorKeys.NotFound));
        var result = await new UpdateHomepageCategoryHandler(repository, categories)
            .Handle(new("Saved", "View", "/categories", [Ids[2], Ids[0], Ids[1]], false),
                TestContext.Current.CancellationToken);
        result.Error.Should().Be(HomepageCategoryErrorKeys.NotFound);
        await repository.Received(1).SaveAsync(Arg.Is<HomepageCategorySection>(section =>
            !section.IsVisible && section.CategoryIds.SequenceEqual(new[] { Ids[2], Ids[0], Ids[1] })),
            false, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task AdminRead_AbsentSection_ReturnsSuccessfulNull()
    {
        var repository = Substitute.For<IHomepageCategoryRepository>();
        var categories = Substitute.For<ICategoryRepository>();
        var files = Substitute.For<IFileStorage>();
        var result = await new GetHomepageCategoryForAdminHandler(repository, categories, files)
            .Handle(new(), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task AdminRead_HiddenSectionPreservesIdsAndMarksDeletedCategoryUnavailable()
    {
        var repository = Substitute.For<IHomepageCategoryRepository>();
        var categories = Substitute.For<ICategoryRepository>();
        var files = Substitute.For<IFileStorage>();
        repository.GetForAdminAsync(Arg.Any<CancellationToken>())
            .Returns(HomepageCategorySection.Create("Saved", "View", "/categories", Ids, false));
        categories.GetByIdAsync(Ids[0], Arg.Any<CancellationToken>())
            .Returns(Category(Ids[0], "First", image: "first.png"));
        categories.GetByIdAsync(Ids[2], Arg.Any<CancellationToken>())
            .Returns(Category(Ids[2], "Third", active: false));
        files.GetPublicUrl(StorageArea.CategoryImages, "first.png").Returns("https://example.com/first.png");

        var result = await new GetHomepageCategoryForAdminHandler(repository, categories, files)
            .Handle(new(), TestContext.Current.CancellationToken);
        result.Value.IsVisible.Should().BeFalse();
        result.Value.CategoryIds.Should().Equal(Ids);
        result.Value.Categories.Select(category => category.Id).Should().Equal(Ids[0], Ids[2]);
        result.Value.Categories[0].ImageUrl.Should().Be("https://example.com/first.png");
        result.Value.Categories[1].IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Choices_ActiveCategoriesKeepImageUrlAndImageFreePlaceholderInput()
    {
        var categories = Substitute.For<ICategoryRepository>();
        var files = Substitute.For<IFileStorage>();
        categories.GetActiveForHomepageAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Category(Ids[0], "First", image: "first.png"), Category(Ids[1], "Second") });
        files.GetPublicUrl(StorageArea.CategoryImages, "first.png").Returns("https://example.com/first.png");
        var result = await new GetHomepageCategoryChoicesHandler(categories, files)
            .Handle(new(), TestContext.Current.CancellationToken);
        result.Value.Select(category => category.Name).Should().Equal("First", "Second");
        result.Value[0].ImageUrl.Should().Be("https://example.com/first.png");
        result.Value[1].ImageUrl.Should().BeNull();
    }
}
