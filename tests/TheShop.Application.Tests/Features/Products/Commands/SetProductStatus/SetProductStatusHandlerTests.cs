using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.SetProductStatus;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.SetProductStatus;

/// <summary>
/// Tests for <see cref="SetProductStatusHandler"/>. Deactivation is unconditional (AC-10).
/// Activation loads each candidate with its variants and calls
/// <c>Product.EnsurePublishable()</c>; a product missing a price is skipped and reported in
/// <see cref="Application.Features.Products.DTOs.ProductStatusChangeDto.NotPublishable"/> rather
/// than failing the whole batch, and left Inactive (plan §5 Decision 3, AC-10, AC-23).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class SetProductStatusHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private SetProductStatusHandler CreateSut() => new(_products);

    private static Category ExampleCategory() => Category.Rehydrate(Guid.NewGuid(), "Disposables");
    private static Brand ExampleBrand() => Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar");

    private static Product BuildPricedProduct() =>
        Product.Create(
            "Elf Bar BC5000", "desc", null, Sku.Create("ELF-BC5000"),
            ProductPricing.Create(Money.Create(24.99m)), false, ExampleCategory(), ExampleBrand());

    private static Product BuildUnpricedProduct() =>
        Product.Create(
            "Lost Mary OS5000", "desc", null, Sku.Create("LM-OS5000"),
            null, false, ExampleCategory(), ExampleBrand());

    // =========================================================================
    // Deactivation is unconditional — no EnsurePublishable check (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenDeactivating_CallsSetPublishedAsyncWithFalseAndReturnsTheChangedCount()
    {
        var id = Guid.NewGuid();
        _products.SetPublishedAsync(Arg.Is<IReadOnlyList<Guid>>(ids => ids.Contains(id)), false, Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await CreateSut().Handle(new SetProductStatusCommand([id], false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChangedCount.Should().Be(1);
        result.Value.NotPublishable.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenDeactivating_NeverLoadsVariants()
    {
        _products.SetPublishedAsync(Arg.Any<IReadOnlyList<Guid>>(), false, Arg.Any<CancellationToken>()).Returns(1);

        await CreateSut().Handle(new SetProductStatusCommand([Guid.NewGuid()], false), CancellationToken.None);

        await _products.DidNotReceive().GetManyWithVariantsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Activation — eligible product is published (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenActivatingAPricedProduct_ReturnsChangedCountOfOneAndNoSkips()
    {
        var product = BuildPricedProduct();
        _products.GetManyWithVariantsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([product]);
        _products.SetPublishedAsync(Arg.Is<IReadOnlyList<Guid>>(ids => ids.Single() == product.Id), true, Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await CreateSut().Handle(new SetProductStatusCommand([product.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChangedCount.Should().Be(1);
        result.Value.NotPublishable.Should().BeEmpty();
    }

    // =========================================================================
    // Activation — a product missing a price is skipped, not failed (plan §5 Decision 3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenActivatingAnUnpricedProduct_SkipsItAndReportsItAsNotPublishable()
    {
        var product = BuildUnpricedProduct();
        _products.GetManyWithVariantsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([product]);

        var result = await CreateSut().Handle(new SetProductStatusCommand([product.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChangedCount.Should().Be(0);
        result.Value.NotPublishable.Should().ContainSingle(p => p.Id == product.Id && p.Name == product.Name);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenEveryCandidateIsUnpriced_NeverCallsSetPublishedAsync()
    {
        var product = BuildUnpricedProduct();
        _products.GetManyWithVariantsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([product]);

        await CreateSut().Handle(new SetProductStatusCommand([product.Id], true), CancellationToken.None);

        await _products.DidNotReceive().SetPublishedAsync(Arg.Any<IReadOnlyList<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Activation — a mixed batch activates only the eligible products (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WithAMixOfPricedAndUnpricedProducts_ActivatesOnlyThePricedOnesAndReportsTheRest()
    {
        var priced = BuildPricedProduct();
        var unpriced = BuildUnpricedProduct();
        _products.GetManyWithVariantsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([priced, unpriced]);
        _products.SetPublishedAsync(Arg.Is<IReadOnlyList<Guid>>(ids => ids.Single() == priced.Id), true, Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await CreateSut().Handle(
            new SetProductStatusCommand([priced.Id, unpriced.Id], true), CancellationToken.None);

        result.Value.ChangedCount.Should().Be(1);
        result.Value.NotPublishable.Should().ContainSingle(p => p.Id == unpriced.Id);
    }

    // =========================================================================
    // Failure translation (RULE-8 access denial, and unexpected technical failure)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheRepositoryThrowsAnAccessDeniedException_ReturnsRbacAccessDeniedFailure()
    {
        _products.SetPublishedAsync(Arg.Any<IReadOnlyList<Guid>>(), false, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("access denied"));

        var result = await CreateSut().Handle(new SetProductStatusCommand([Guid.NewGuid()], false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheRepositoryThrowsUnexpectedly_ReturnsStatusChangeFailedFailure()
    {
        _products.SetPublishedAsync(Arg.Any<IReadOnlyList<Guid>>(), false, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(new SetProductStatusCommand([Guid.NewGuid()], false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ProductErrorKeys.StatusChangeFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10 (activate/deactivate without/with confirmation, changed count): Handle_WhenDeactivating_CallsSetPublishedAsyncWithFalseAndReturnsTheChangedCount,
//        Handle_WhenActivatingAPricedProduct_ReturnsChangedCountOfOneAndNoSkips
// AC-23 (skipped-unpublishable reporting, mixed batch): Handle_WhenActivatingAnUnpricedProduct_SkipsItAndReportsItAsNotPublishable,
//        Handle_WithAMixOfPricedAndUnpricedProducts_ActivatesOnlyThePricedOnesAndReportsTheRest
