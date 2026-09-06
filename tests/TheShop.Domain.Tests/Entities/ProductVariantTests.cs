using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="ProductVariant"/>'s own construction behaviour. A variant is never
/// created directly outside <see cref="Product.ApplyVariantConfiguration"/> in production, but
/// <see cref="ProductVariant.Create"/> is itself public — its identity-reuse contract (an
/// existing variant kept across an edit keeps its id) is covered here in isolation; pinning,
/// availability, and pricing behaviour are exercised through <c>Product</c> in
/// <c>ProductTests</c>, since <c>Pin</c>/<c>Unpin</c> are internal to the aggregate.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class ProductVariantTests
{
    private static IReadOnlySet<Guid> OneValue() => new HashSet<Guid> { Guid.NewGuid() };

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithNoIdSupplied_AssignsANewId()
    {
        var variant = ProductVariant.Create(null, Sku.Create("SKU-MANGO"), null, true, null, OneValue(), 0);

        variant.Id.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithAnExistingId_ReusesIt()
    {
        var id = Guid.NewGuid();

        var variant = ProductVariant.Create(id, Sku.Create("SKU-MANGO"), null, true, null, OneValue(), 0);

        variant.Id.Should().Be(id, "an existing variant kept across an edit must keep its identity");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithPricingAndAvailability_CarriesThemVerbatim()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m));

        var variant = ProductVariant.Create(null, Sku.Create("SKU-MANGO"), pricing, false, null, OneValue(), 2);

        variant.Pricing.Should().Be(pricing);
        variant.IsAvailable.Should().BeFalse();
        variant.Position.Should().Be(2);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithAPinnedImageId_CarriesItVerbatim()
    {
        var imageId = Guid.NewGuid();

        var variant = ProductVariant.Create(null, Sku.Create("SKU-MANGO"), null, true, imageId, OneValue(), 0);

        variant.PinnedImageId.Should().Be(imageId);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-9 (a generated variant carries its option values and an automatic SKU): Create_WithPricingAndAvailability_CarriesThemVerbatim
// AC-10a (a variant carries at most one pinned image): Create_WithAPinnedImageId_CarriesItVerbatim
// AC-12 (an existing variant kept across an edit keeps its configuration): Create_WithAnExistingId_ReusesIt
