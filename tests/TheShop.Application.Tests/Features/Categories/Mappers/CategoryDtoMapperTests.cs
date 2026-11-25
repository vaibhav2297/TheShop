using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories.Mappers;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Mappers;

/// <summary>
/// Tests for <see cref="CategoryDtoMapper"/> — the <see cref="Category"/> → <c>CategoryDto</c>
/// projection that resolves a stored image key to its public bucket URL through
/// <see cref="IFileStorage"/> (Behavior 4, AC-6).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CategoryDtoMapperTests
{
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToDto_MapsIdNameDescriptionAndIsActive()
    {
        var category = Category.Create("Disposables", "Single-use vape devices.", isActive: false);

        var dto = CategoryDtoMapper.ToDto(category, _fileStorage);

        dto.Id.Should().Be(category.Id);
        dto.Name.Should().Be("Disposables");
        dto.Description.Should().Be("Single-use vape devices.");
        dto.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToDto_WhenImagePathIsSet_ResolvesImageUrlViaFileStorage()
    {
        var category = Category.Create("Disposables", null, true);
        category.AttachImage("categories/abc/image.webp");
        _fileStorage.GetPublicUrl(StorageArea.CategoryImages, "categories/abc/image.webp")
                     .Returns("https://cdn.example/category-images/categories/abc/image.webp");

        var dto = CategoryDtoMapper.ToDto(category, _fileStorage);

        dto.ImageUrl.Should().Be("https://cdn.example/category-images/categories/abc/image.webp");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToDto_WhenImagePathIsSet_QueriesTheCategoryImagesStorageArea()
    {
        var category = Category.Create("Disposables", null, true);
        category.AttachImage("categories/abc/image.webp");

        CategoryDtoMapper.ToDto(category, _fileStorage);

        _fileStorage.Received(1).GetPublicUrl(StorageArea.CategoryImages, "categories/abc/image.webp");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToDto_WhenImagePathIsNull_MapsImageUrlAsNullWithoutCallingFileStorage()
    {
        var category = Category.Create("Disposables", null, true);

        var dto = CategoryDtoMapper.ToDto(category, _fileStorage);

        dto.ImageUrl.Should().BeNull();
        _fileStorage.DidNotReceive().GetPublicUrl(Arg.Any<StorageArea>(), Arg.Any<string>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: ToDto_WhenImagePathIsSet_ResolvesImageUrlViaFileStorage,
//        ToDto_WhenImagePathIsSet_QueriesTheCategoryImagesStorageArea, ToDto_WhenImagePathIsNull_MapsImageUrlAsNullWithoutCallingFileStorage
