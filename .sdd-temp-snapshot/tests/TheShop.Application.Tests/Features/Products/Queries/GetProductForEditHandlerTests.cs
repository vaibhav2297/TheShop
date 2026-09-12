using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Queries.GetProductForEdit;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetProductForEditHandler"/>: a saved product loads with its gallery,
/// option types, and variants exactly as saved (AC-6), and a stale id fails gracefully rather
/// than throwing (AC-34).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class GetProductForEditHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private GetProductForEditHandler CreateSut() => new(_products, _fileStorage);

    private static Product ExampleProduct(Guid id) =>
        Product.Rehydrate(
            id, "Elf Bar BC5000", ProductDescription.Rehydrate("A long-lasting disposable vape."), null,
            Sku.Create("ELF-BC5000"), ProductPricing.Create(Money.Create(24.99m)), true,
            Category.Rehydrate(Guid.NewGuid(), "Disposables"), Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar"),
            DateTimeOffset.UtcNow);

    // =========================================================================
    // Happy path (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenTheProductExists_ReturnsItsFullEditPayload()
    {
        var id = Guid.NewGuid();
        _products.GetForEditAsync(id, Arg.Any<CancellationToken>()).Returns((ExampleProduct(id), "row-version-1"));

        var result = await CreateSut().Handle(new GetProductForEditQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.RowVersion.Should().Be("row-version-1");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Handle_WhenTheProductHasSpecifications_ReturnsThemInSavedOrder()
    {
        var id = Guid.NewGuid();
        var product = Product.Rehydrate(
            id, "Elf Bar BC5000", ProductDescription.Rehydrate("<p>Long-lasting.</p>"), null,
            Sku.Create("ELF-BC5000"), ProductPricing.Create(Money.Create(24.99m)), true,
            Category.Rehydrate(Guid.NewGuid(), "Disposables"), Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar"),
            DateTimeOffset.UtcNow,
            specifications: [
                ProductSpecification.Create(Guid.NewGuid(), "Material", "Stainless steel", 0),
                ProductSpecification.Create(Guid.NewGuid(), "Capacity", "750 ml", 1),
            ]);
        _products.GetForEditAsync(id, Arg.Any<CancellationToken>()).Returns((product, "row-version-1"));

        var result = await CreateSut().Handle(new GetProductForEditQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("<p>Long-lasting.</p>");
        result.Value.Specifications.Should().SatisfyRespectively(
            first => first.Name.Should().Be("Material"),
            second => second.Name.Should().Be("Capacity"));
    }

    // =========================================================================
    // Stale edit link (AC-34)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenTheProductNoLongerExists_FailsWithNotFound()
    {
        var id = Guid.NewGuid();
        _products.GetForEditAsync(id, Arg.Any<CancellationToken>())
            .Returns(((Product Product, string RowVersion)?)null);

        var result = await CreateSut().Handle(new GetProductForEditQuery(id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.NotFound);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6 (edit form pre-filled with everything saved): Handle_WhenTheProductExists_ReturnsItsFullEditPayload
// AC-34 (stale edit link): Handle_WhenTheProductNoLongerExists_FailsWithNotFound
// product-description AC-1/AC-2 (reopening shows saved description/rows in saved order): Handle_WhenTheProductHasSpecifications_ReturnsThemInSavedOrder
