using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.CreateProduct;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.CreateProduct;

/// <summary>
/// Tests for <see cref="CreateProductHandler"/>'s general save flow: the happy path (AC-4), name
/// and SKU uniqueness guards (RULE-2, RULE-8), missing category/brand (RULE-3), the
/// upload-compensation path on a persistence failure (RULE-18), and technical-failure handling
/// (AC-32). Variant image pin resolution and the publish-refusal detail are covered separately in
/// <c>CreateProductHandlerPinTests</c>.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class CreateProductHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _brandId = Guid.NewGuid();

    public CreateProductHandlerTests()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Category.Rehydrate(_categoryId, "Disposables"));
        _brands.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Brand.Rehydrate(_brandId, "Elf Bar", "elf-bar"));
        _products.FindConflictsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ProductConflicts.None);
    }

    private CreateProductHandler CreateSut() => new(_products, _categories, _brands, _fileStorage);

    private CreateProductCommand Command(
        bool isPublished = false,
        decimal? originalPrice = 24.99m,
        decimal? salePrice = null,
        IReadOnlyList<ProductGalleryEntry>? gallery = null,
        IReadOnlyList<SpecificationInput>? specifications = null) =>
        new("Elf Bar BC5000", "A long-lasting disposable vape.", "ELF-BC5000",
            _categoryId, _brandId, originalPrice, salePrice, isPublished, gallery ?? [], [], specifications ?? [], []);

    // =========================================================================
    // Happy path (AC-4, AC-19)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithValidData_PersistsTheProductAndReturnsItsDto()
    {
        _products.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-1"));

        var result = await CreateSut().Handle(Command(isPublished: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Elf Bar BC5000");
        result.Value.IsPublished.Should().BeTrue();
        result.Value.RowVersion.Should().Be("row-version-1");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithOnlyANameCategoryAndBrand_SavesAsADraft()
    {
        _products.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-1"));

        var result = await CreateSut().Handle(Command(isPublished: false, originalPrice: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("RULE-15: an Unpublished product may be saved at any level of completeness");
        result.Value.OriginalPrice.Should().BeNull();
        result.Value.IsPublished.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_UploadsEveryGalleryImage()
    {
        _products.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-1"));
        _fileStorage.UploadAsync(
            StorageArea.ProductImages, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("products/photo.png");
        var gallery = new[]
        {
            new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "photo.png", "image/png"), 0, true, Guid.NewGuid()),
        };

        var result = await CreateSut().Handle(Command(gallery: gallery), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Images.Should().ContainSingle();
        await _fileStorage.Received(1).UploadAsync(
            StorageArea.ProductImages, Arg.Any<Guid>(), Arg.Any<Stream>(), "photo.png", "image/png", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Specifications (product-description RULE-3, RULE-4, AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Handle_WithSpecificationRows_PersistsThemInGivenOrder()
    {
        _products.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-1"));

        var result = await CreateSut().Handle(Command(specifications: [
            new SpecificationInput(null, "Material", "Stainless steel", 0),
            new SpecificationInput(null, "Capacity", "750 ml", 1),
        ]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Specifications.Should().SatisfyRespectively(
            first => first.Name.Should().Be("Material"),
            second => second.Name.Should().Be("Capacity"));
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Handle_WithDuplicateSpecificationNames_FailsWithSpecificationNameDuplicated()
    {
        var result = await CreateSut().Handle(Command(specifications: [
            new SpecificationInput(null, "Material", "Stainless steel", 0),
            new SpecificationInput(null, "material", "Aluminum", 1),
        ]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.SpecificationNameDuplicated);
        await _products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Name uniqueness (RULE-2, AC-22)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithADuplicateName_FailsWithNameAlreadyExists()
    {
        _products.FindConflictsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new ProductConflicts(NameTaken: true, SkusTaken: []));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.NameAlreadyExists);
        await _products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // SKU uniqueness (RULE-8, AC-27)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithADuplicateSku_FailsWithSkuAlreadyExists()
    {
        _products.FindConflictsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new ProductConflicts(NameTaken: false, SkusTaken: ["elf-bc5000"]));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.SkuAlreadyExists);
        await _products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Category / brand missing (RULE-3, AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenTheCategoryDoesNotExist_FailsWithCategoryRequired()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Category?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.CategoryRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenTheBrandDoesNotExist_FailsWithBrandRequired()
    {
        _brands.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Brand?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.BrandRequired);
    }

    // =========================================================================
    // Save failure preserves nothing partially created, and cleans up uploads (RULE-18, AC-32)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenPersistenceFails_DeletesEveryImageUploadedThisRequest()
    {
        _fileStorage.UploadAsync(
            StorageArea.ProductImages, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("products/photo.png");
        _products.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(ProductErrorKeys.CreateFailed));
        var gallery = new[]
        {
            new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "photo.png", "image/png"), 0, true, Guid.NewGuid()),
        };

        var result = await CreateSut().Handle(Command(gallery: gallery), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _fileStorage.Received(1).DeleteAsync(StorageArea.ProductImages, "products/photo.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenAnUnexpectedExceptionIsThrown_FailsWithCreateFailed()
    {
        _products.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("connection problem"));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.CreateFailed,
            "a save failing for a reason outside the staff member's control must say so clearly rather than leak a technical error");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-4 (create a published product end to end): Handle_WithValidData_PersistsTheProductAndReturnsItsDto,
//        Handle_UploadsEveryGalleryImage
// AC-19 (draft with only name/category/brand saves): Handle_WithOnlyANameCategoryAndBrand_SavesAsADraft
// AC-22 (duplicate product name refused): Handle_WithADuplicateName_FailsWithNameAlreadyExists
// AC-23 (category/brand required): Handle_WhenTheCategoryDoesNotExist_FailsWithCategoryRequired,
//        Handle_WhenTheBrandDoesNotExist_FailsWithBrandRequired
// AC-27 (duplicate SKU refused): Handle_WithADuplicateSku_FailsWithSkuAlreadyExists
// AC-32 (a failed save preserves everything, never partly saves): Handle_WhenPersistenceFails_DeletesEveryImageUploadedThisRequest,
//        Handle_WhenAnUnexpectedExceptionIsThrown_FailsWithCreateFailed
// RULE-18 (an unused image is discarded, never left stored): Handle_WhenPersistenceFails_DeletesEveryImageUploadedThisRequest
// product-description AC-1 (create with specification rows, saved in given order): Handle_WithSpecificationRows_PersistsThemInGivenOrder
// product-description AC-8 (duplicate names rejected, nothing saved): Handle_WithDuplicateSpecificationNames_FailsWithSpecificationNameDuplicated
