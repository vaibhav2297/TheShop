using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Queries.GetCategoryById;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Queries.GetCategoryById;

/// <summary>
/// Tests for <see cref="GetCategoryByIdHandler"/> — loads a category for the edit form, and fails
/// cleanly when it no longer exists (edge case: "the category being edited was already removed by
/// another staff member", AC-23).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class GetCategoryByIdHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private GetCategoryByIdHandler CreateSut() => new(_categories, _fileStorage);

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenCategoryExists_ReturnsSuccessResultWithTheCategorysDetails()
    {
        var category = Category.Create("Disposables", "Single-use vape devices.", isActive: false);
        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);

        var result = await CreateSut().Handle(new GetCategoryByIdQuery(category.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(category.Id);
        result.Value.Name.Should().Be("Disposables");
        result.Value.Description.Should().Be("Single-use vape devices.");
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenCategoryHasAnImage_ResolvesImageUrlViaFileStorage()
    {
        var category = Category.Create("Disposables", null, true);
        category.AttachImage("categories/abc/image.webp");
        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        _fileStorage.GetPublicUrl(StorageArea.CategoryImages, "categories/abc/image.webp")
                     .Returns("https://cdn.example/category-images/categories/abc/image.webp");

        var result = await CreateSut().Handle(new GetCategoryByIdQuery(category.Id), CancellationToken.None);

        result.Value.ImageUrl.Should().Be("https://cdn.example/category-images/categories/abc/image.webp");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenCategoryDoesNotExist_ReturnsNotFoundFailure()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Category?)null);

        var result = await CreateSut().Handle(new GetCategoryByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.NotFound);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-23: Handle_WhenCategoryExists_ReturnsSuccessResultWithTheCategorysDetails,
//         Handle_WhenCategoryDoesNotExist_ReturnsNotFoundFailure
