using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Product"/> aggregate — discount projection (<see cref="Product.IsDiscounted"/>,
/// <see cref="Product.EffectivePrice"/>) and sellability (<see cref="Product.IsInStock"/>), which
/// back FR-3 (AC-2). Stock is not tracked (RULE-16 revision): a no-variant product is always
/// sellable.
///
/// Also carries the create-product feature's coverage of every aggregate invariant introduced for
/// managed product CRUD: detail/SKU/pricing/publish mutation, the gallery's exactly-one-primary
/// rule and pin unpinning (RULE-7, RULE-13), option-type uniqueness (RULE-9), the variant set as
/// the cartesian product of option values with surviving-row preservation (RULE-10, RULE-12),
/// variant image pinning ownership (RULE-13), and publish completeness (RULE-14).
/// <see href=".specs/product-catalogue/spec.md"/>
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class ProductTests
{
    private static Category ExampleCategory() =>
        Category.Rehydrate(Guid.NewGuid(), "Disposables");

    private static Brand ExampleBrand() =>
        Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar");

    private static Product BuildProduct(
        ProductPricing? pricing = null,
        bool isPublished = true) =>
        Product.Create(
            "Elf Bar BC5000",
            ProductDescription.Create("A long-lasting disposable vape."),
            "https://example.com/photo.webp",
            Sku.Create("ELF-BAR-BC5000"),
            pricing ?? ProductPricing.Create(Money.Create(24.99m)),
            isPublished,
            ExampleCategory(),
            ExampleBrand());

    // =========================================================================
    // Create — happy path (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithValidData_ReturnsProductWithSuppliedProperties()
    {
        var product = BuildProduct();

        product.Name.Should().Be("Elf Bar BC5000");
        product.ImageUrl.Should().Be("https://example.com/photo.webp");
        product.Sku.Value.Should().Be("ELF-BAR-BC5000");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_CalledTwice_AssignsDifferentIds()
    {
        var first = BuildProduct();
        var second = BuildProduct();

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithoutExplicitCreatedAt_AssignsUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var product = BuildProduct();
        var after = DateTimeOffset.UtcNow;

        product.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithExplicitCreatedAt_UsesSuppliedTimestamp()
    {
        var fixedTime = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var product = Product.Create(
            "Elf Bar BC5000", ProductDescription.Create("desc"), null,
            Sku.Create("ELF-BAR-BC5000"),
            ProductPricing.Create(Money.Create(24.99m)),
            true, ExampleCategory(), ExampleBrand(), fixedTime);

        product.CreatedAt.Should().Be(fixedTime);
    }

    // =========================================================================
    // IsDiscounted / EffectivePrice — projected from ProductPricing (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void IsDiscounted_WhenPricingHasSalePrice_ReturnsTrue()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m), Money.Create(19.99m));
        var product = BuildProduct(pricing);

        product.IsDiscounted.Should().BeTrue();
        product.EffectivePrice.Should().Be(Money.Create(19.99m));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void IsDiscounted_WhenPricingHasNoSalePrice_ReturnsFalse()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m));
        var product = BuildProduct(pricing);

        product.IsDiscounted.Should().BeFalse();
        product.EffectivePrice.Should().Be(Money.Create(24.99m));
    }

    // =========================================================================
    // IsInStock — sellability, no stock tracking (RULE-16 revision)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void IsInStock_WithNoVariants_IsAlwaysTrue()
    {
        var product = BuildProduct();

        product.IsInStock.Should().BeTrue();
    }

    // =========================================================================
    // Rehydrate — reconstruction from persisted data without re-running invariants
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Rehydrate_PreservesSuppliedId()
    {
        var id = Guid.NewGuid();

        var product = Product.Rehydrate(
            id, "Elf Bar BC5000", ProductDescription.Rehydrate("desc"), null,
            Sku.Create("ELF-BAR-BC5000"),
            ProductPricing.Create(Money.Create(24.99m)),
            true, ExampleCategory(), ExampleBrand(),
            DateTimeOffset.UtcNow);

        product.Id.Should().Be(id);
    }

    // =========================================================================
    // create-product — UpdateDetails (RULE-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void UpdateDetails_WithValidData_UpdatesNameDescriptionCategoryAndBrand()
    {
        var product = BuildProduct();
        var newCategory = Category.Rehydrate(Guid.NewGuid(), "Pod Systems");
        var newBrand = Brand.Rehydrate(Guid.NewGuid(), "Vaporesso", "vaporesso");

        product.UpdateDetails("New Name", ProductDescription.Create("New description."), newCategory, newBrand);

        product.Name.Should().Be("New Name");
        product.Description.Html.Should().Be("New description.");
        product.Category.Should().Be(newCategory);
        product.Brand.Should().Be(newBrand);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void UpdateDetails_WithBlankName_ThrowsProductNameRequiredException()
    {
        var product = BuildProduct();

        var act = () => product.UpdateDetails("   ", ProductDescription.Create("desc"), ExampleCategory(), ExampleBrand());

        act.Should().Throw<ProductNameRequiredException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void UpdateDetails_WithNameOver150Characters_ThrowsProductNameTooLongException()
    {
        var product = BuildProduct();
        var tooLong = new string('a', 151);

        var act = () => product.UpdateDetails(tooLong, ProductDescription.Create("desc"), ExampleCategory(), ExampleBrand());

        act.Should().Throw<ProductNameTooLongException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void UpdateDetails_WithNameOfExactly150Characters_Succeeds()
    {
        var product = BuildProduct();
        var boundary = new string('a', 150);

        var act = () => product.UpdateDetails(boundary, ProductDescription.Create("desc"), ExampleCategory(), ExampleBrand());

        act.Should().NotThrow();
        product.Name.Should().Be(boundary);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void UpdateDetails_WithDescriptionOver20000Characters_ThrowsProductDescriptionTooLongException()
    {
        // RULE-2 (product-description) supersedes create-product RULE-1's 2,000-character bound
        // with a 20,000 plain-text-character bound, enforced by ProductDescription.Create before
        // the value ever reaches UpdateDetails.
        var product = BuildProduct();
        var tooLong = new string('a', 20_001);

        var act = () => product.UpdateDetails("Name", ProductDescription.Create(tooLong), ExampleCategory(), ExampleBrand());

        act.Should().Throw<ProductDescriptionTooLongException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithBlankDescription_TreatsDescriptionAsEmpty()
    {
        var product = Product.Create(
            "Elf Bar BC5000", ProductDescription.Create("   "), null, Sku.Create("ELF-BC5000"),
            ProductPricing.Create(Money.Create(24.99m)), true, ExampleCategory(), ExampleBrand());

        product.Description.IsEmpty.Should().BeTrue("an optional description with only whitespace is not entered (RULE-1)");
    }

    // =========================================================================
    // create-product — SetSku, SetPricing, SetPublished (RULE-8, RULE-15)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetSku_WithANewSku_ReplacesTheProductsSku()
    {
        var product = BuildProduct();

        product.SetSku(Sku.Create("NEW-SKU"));

        product.Sku.Value.Should().Be("NEW-SKU");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetPricing_WithNull_LeavesTheProductADraftWithNoPrice()
    {
        var product = BuildProduct();

        product.SetPricing(null);

        product.Pricing.Should().BeNull("an Unpublished product may be saved with no price yet (RULE-15)");
        product.EffectivePrice.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetPublished_TogglesTheStatus()
    {
        var product = BuildProduct(isPublished: false);

        product.SetPublished(true);

        product.IsPublished.Should().BeTrue();
    }

    // =========================================================================
    // create-product — SetGallery / SetPrimaryImage / RemoveImage (RULE-7, RULE-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetGallery_WithNoImageMarkedPrimary_DefaultsTheFirstToPrimary()
    {
        var product = BuildProduct();

        product.SetGallery([
            new GalleryImageInput(null, "img-1.png", false),
            new GalleryImageInput(null, "img-2.png", false),
        ]);

        product.Images.Should().ContainSingle(i => i.IsPrimary).Which.ObjectKey.Should().Be("img-1.png");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetGallery_WithMoreThanOneImageMarkedPrimary_KeepsExactlyOnePrimary()
    {
        var product = BuildProduct();

        product.SetGallery([
            new GalleryImageInput(null, "img-1.png", true),
            new GalleryImageInput(null, "img-2.png", true),
        ]);

        product.Images.Should().ContainSingle(i => i.IsPrimary);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetGallery_WithNoImagesAtAll_LeavesTheGalleryEmpty()
    {
        var product = BuildProduct();

        product.SetGallery([]);

        product.Images.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetPrimaryImage_WithAnImageNotInTheGallery_ThrowsProductImageNotOwnedException()
    {
        var product = BuildProduct();
        product.SetGallery([new GalleryImageInput(null, "img-1.png", true)]);

        var act = () => product.SetPrimaryImage(Guid.NewGuid());

        act.Should().Throw<ProductImageNotOwnedException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetPrimaryImage_WithAGalleryImage_MakesItPrimaryAndDemotesTheOthers()
    {
        var product = BuildProduct();
        product.SetGallery([
            new GalleryImageInput(null, "img-1.png", true),
            new GalleryImageInput(null, "img-2.png", false),
        ]);
        var second = product.Images[1];

        product.SetPrimaryImage(second.Id);

        product.Images[0].IsPrimary.Should().BeFalse();
        second.IsPrimary.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void RemoveImage_WhenTheRemovedImageWasPrimary_PromotesTheNextImage()
    {
        var product = BuildProduct();
        product.SetGallery([
            new GalleryImageInput(null, "img-1.png", true),
            new GalleryImageInput(null, "img-2.png", false),
        ]);
        var primaryId = product.Images[0].Id;

        product.RemoveImage(primaryId);

        product.Images.Should().ContainSingle(i => i.IsPrimary).Which.ObjectKey.Should().Be("img-2.png");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void RemoveImage_WhenItWasThePinOfAVariant_UnpinsThatVariant()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out _);
        product.SetGallery([new GalleryImageInput(null, "img-1.png", true)]);
        var imageId = product.Images[0].Id;
        product.ApplyVariantConfiguration([]);
        var variantId = product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId)).Id;
        product.PinVariantImage(variantId, imageId);

        product.RemoveImage(imageId);

        product.Variants.Single(v => v.Id == variantId).PinnedImageId.Should().BeNull(
            "RULE-13: removing a pinned gallery image leaves that variant unpinned");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void RemoveImage_WithAnUnknownId_DoesNothing()
    {
        var product = BuildProduct();
        product.SetGallery([new GalleryImageInput(null, "img-1.png", true)]);

        var act = () => product.RemoveImage(Guid.NewGuid());

        act.Should().NotThrow();
        product.Images.Should().ContainSingle();
    }

    // =========================================================================
    // create-product — SetOptionTypes (RULE-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetOptionTypes_WithDuplicateNamesIgnoringCaseAndSpaces_ThrowsDuplicateOptionNameException()
    {
        var product = BuildProduct();

        var act = () => product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Flavour", [new ProductOptionValueInput(null, "Mango")]),
            new ProductOptionTypeInput(null, "  flavour  ", [new ProductOptionValueInput(null, "Mint")]),
        ]);

        act.Should().Throw<DuplicateOptionNameException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetOptionTypes_WithAnEmptyName_ThrowsOptionTypeNameRequiredException()
    {
        var product = BuildProduct();

        var act = () => product.SetOptionTypes([
            new ProductOptionTypeInput(null, "  ", [new ProductOptionValueInput(null, "Mango")]),
        ]);

        act.Should().Throw<OptionTypeNameRequiredException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetOptionTypes_WithNoValues_ThrowsOptionTypeValueRequiredException()
    {
        var product = BuildProduct();

        var act = () => product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Flavour", []),
        ]);

        act.Should().Throw<OptionTypeValueRequiredException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetOptionTypes_WithDuplicateValuesIgnoringCaseAndSpaces_ThrowsDuplicateOptionValueException()
    {
        var product = BuildProduct();

        var act = () => product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Flavour", [
                new ProductOptionValueInput(null, "Mango"),
                new ProductOptionValueInput(null, "  MANGO  "),
            ]),
        ]);

        act.Should().Throw<DuplicateOptionValueException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SetOptionTypes_WithValidTypes_ReplacesTheProductsOptionTypesWholesale()
    {
        var product = BuildProduct();
        product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Flavour", [new ProductOptionValueInput(null, "Mango")]),
        ]);

        product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Nicotine", [new ProductOptionValueInput(null, "20mg")]),
        ]);

        product.OptionTypes.Should().ContainSingle().Which.Name.Should().Be("Nicotine");
    }

    // =========================================================================
    // product-description — SetSpecifications (RULE-3, RULE-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void SetSpecifications_WithNoRows_LeavesTheSpecificationsEmpty()
    {
        var product = BuildProduct();

        product.SetSpecifications([]);

        product.Specifications.Should().BeEmpty("specifications are optional (RULE-3)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void SetSpecifications_WithValidRows_AssignsPositionByListOrder()
    {
        var product = BuildProduct();

        product.SetSpecifications([
            new ProductSpecificationInput(null, "Material", "Stainless steel"),
            new ProductSpecificationInput(null, "Capacity", "750 ml"),
        ]);

        product.Specifications.Should().SatisfyRespectively(
            first =>
            {
                first.Name.Should().Be("Material");
                first.Position.Should().Be(0);
            },
            second =>
            {
                second.Name.Should().Be("Capacity");
                second.Position.Should().Be(1);
            });
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void SetSpecifications_WithAnEmptyName_ThrowsSpecificationNameRequiredException()
    {
        var product = BuildProduct();

        var act = () => product.SetSpecifications([
            new ProductSpecificationInput(null, "   ", "Stainless steel"),
        ]);

        act.Should().Throw<SpecificationNameRequiredException>();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void SetSpecifications_WithAnEmptyValue_ThrowsSpecificationValueRequiredException()
    {
        var product = BuildProduct();

        var act = () => product.SetSpecifications([
            new ProductSpecificationInput(null, "Material", "   "),
        ]);

        act.Should().Throw<SpecificationValueRequiredException>();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void SetSpecifications_WithDuplicateNamesIgnoringCaseAndSpaces_ThrowsDuplicateSpecificationNameException()
    {
        var product = BuildProduct();

        var act = () => product.SetSpecifications([
            new ProductSpecificationInput(null, "Material", "Stainless steel"),
            new ProductSpecificationInput(null, "  material  ", "Aluminum"),
        ]);

        act.Should().Throw<DuplicateSpecificationNameException>();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void SetSpecifications_CalledTwice_ReplacesThePriorSetWholesale()
    {
        var product = BuildProduct();
        product.SetSpecifications([new ProductSpecificationInput(null, "Material", "Stainless steel")]);

        product.SetSpecifications([new ProductSpecificationInput(null, "Capacity", "750 ml")]);

        product.Specifications.Should().ContainSingle().Which.Name.Should().Be("Capacity");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void SetSpecifications_KeepingAnExistingRowById_PreservesItsId()
    {
        var product = BuildProduct();
        product.SetSpecifications([new ProductSpecificationInput(null, "Material", "Stainless steel")]);
        var existingId = product.Specifications[0].Id;

        product.SetSpecifications([new ProductSpecificationInput(existingId, "Material", "Aluminum")]);

        product.Specifications.Should().ContainSingle().Which.Id.Should().Be(existingId);
    }

    // =========================================================================
    // create-product — ApplyVariantConfiguration (RULE-10, RULE-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void ApplyVariantConfiguration_WithTwoOptionTypesOfTwoValuesEach_GeneratesFourVariants()
    {
        var product = BuildProduct();
        product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Flavour", [
                new ProductOptionValueInput(null, "Mango"), new ProductOptionValueInput(null, "Mint"),
            ]),
            new ProductOptionTypeInput(null, "Nicotine", [
                new ProductOptionValueInput(null, "20mg"), new ProductOptionValueInput(null, "50mg"),
            ]),
        ]);

        product.ApplyVariantConfiguration([]);

        product.Variants.Should().HaveCount(4);
        product.HasVariants.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ApplyVariantConfiguration_WithNoOptionTypes_ProducesNoVariants()
    {
        var product = BuildProduct();

        product.ApplyVariantConfiguration([]);

        product.Variants.Should().BeEmpty();
        product.HasVariants.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ApplyVariantConfiguration_WhenAValueIsAdded_KeepsEveryExistingVariantsConfiguration()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out var mintValueId);
        var mangoVariant = product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId));
        product.ApplyVariantConfiguration([
            new ProductVariantInput(mangoVariant.Id, mangoVariant.OptionValueIds, Sku.Create("SKU-MANGO"),
                ProductPricing.Create(Money.Create(24.99m)), true, null),
        ]);

        // Add a third value to the same option type — Berry.
        var flavourType = product.OptionTypes.Single();
        product.SetOptionTypes([
            new ProductOptionTypeInput(flavourType.Id, flavourType.Name, [
                .. flavourType.Values.Select(v => new ProductOptionValueInput(v.Id, v.Value)),
                new ProductOptionValueInput(null, "Berry"),
            ]),
        ]);
        product.ApplyVariantConfiguration([
            new ProductVariantInput(mangoVariant.Id, mangoVariant.OptionValueIds, Sku.Create("SKU-MANGO"),
                ProductPricing.Create(Money.Create(24.99m)), true, null),
        ]);

        product.Variants.Should().HaveCount(3);
        product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId)).Pricing!.OriginalPrice.Amount
            .Should().Be(24.99m, "RULE-12: adding a value never disturbs an existing variant's configuration");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ApplyVariantConfiguration_WhenAValueIsRemoved_DropsOnlyTheVariantsBuiltFromIt()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out var mintValueId);
        var flavourType = product.OptionTypes.Single();

        product.SetOptionTypes([
            new ProductOptionTypeInput(flavourType.Id, flavourType.Name, [
                new ProductOptionValueInput(flavourType.Values.Single(v => v.Id == mangoValueId).Id, "Mango"),
            ]),
        ]);
        product.ApplyVariantConfiguration([]);

        product.Variants.Should().ContainSingle().Which.OptionValueIds.Should().Contain(mangoValueId);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ApplyVariantConfiguration_WithARequestedRowNoLongerAValidCombination_DropsIt()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out _);

        product.ApplyVariantConfiguration([
            new ProductVariantInput(null, new HashSet<Guid> { Guid.NewGuid() }, Sku.Create("STALE"), null, true, null),
        ]);

        product.Variants.Should().HaveCount(2, "the request named a combination outside the current option types and must be dropped (RULE-10)");
        product.Variants.Should().NotContain(v => v.Sku.Value == "STALE");
    }

    // =========================================================================
    // create-product — PinVariantImage (RULE-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void PinVariantImage_WithAnImageNotInTheGallery_ThrowsProductImageNotOwnedException()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out _);
        product.ApplyVariantConfiguration([]);
        var variantId = product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId)).Id;

        var act = () => product.PinVariantImage(variantId, Guid.NewGuid());

        act.Should().Throw<ProductImageNotOwnedException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void PinVariantImage_WithAGalleryImage_PinsTheVariant()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out _);
        product.SetGallery([new GalleryImageInput(null, "img-1.png", true)]);
        var imageId = product.Images[0].Id;
        product.ApplyVariantConfiguration([]);
        var variantId = product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId)).Id;

        product.PinVariantImage(variantId, imageId);

        product.Variants.Single(v => v.Id == variantId).PinnedImageId.Should().Be(imageId);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void PinVariantImage_WithNull_UnpinsTheVariant()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out _);
        product.SetGallery([new GalleryImageInput(null, "img-1.png", true)]);
        var imageId = product.Images[0].Id;
        product.ApplyVariantConfiguration([]);
        var variantId = product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId)).Id;
        product.PinVariantImage(variantId, imageId);

        product.PinVariantImage(variantId, null);

        product.Variants.Single(v => v.Id == variantId).PinnedImageId.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void PinVariantImage_WithAnUnknownVariantId_DoesNothing()
    {
        var product = BuildProductWithOneFlavourOption(out _, out _);
        product.SetGallery([new GalleryImageInput(null, "img-1.png", true)]);
        var imageId = product.Images[0].Id;
        product.ApplyVariantConfiguration([]);

        var act = () => product.PinVariantImage(Guid.NewGuid(), imageId);

        act.Should().NotThrow();
    }

    // =========================================================================
    // create-product — EnsurePublishable (RULE-14, RULE-15)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void EnsurePublishable_WithNoVariantsAndNoPrice_ThrowsProductNotPublishableExceptionNamingPrice()
    {
        var product = Product.Create(
            "Elf Bar BC5000", ProductDescription.Create("desc"), null, Sku.Create("ELF-BC5000"),
            null, false, ExampleCategory(), ExampleBrand());

        var act = () => product.EnsurePublishable();

        act.Should().Throw<ProductNotPublishableException>()
            .Which.Missing.Should().Contain(ProductNotPublishableException.PriceToken);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void EnsurePublishable_WithNoVariantsAndAPrice_Succeeds()
    {
        var product = BuildProduct();

        var act = () => product.EnsurePublishable();

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void EnsurePublishable_WithAnUnpricedVariant_ThrowsNamingThatVariantsToken()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out var mintValueId);
        var mango = product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId));
        var mint = product.Variants.Single(v => v.OptionValueIds.Contains(mintValueId));
        product.ApplyVariantConfiguration([
            new ProductVariantInput(mango.Id, mango.OptionValueIds, mango.Sku, ProductPricing.Create(Money.Create(24.99m)), true, null),
        ]);

        var act = () => product.EnsurePublishable();

        act.Should().Throw<ProductNotPublishableException>()
            .Which.Missing.Should().ContainSingle(
                token => token == $"{ProductNotPublishableException.VariantPriceTokenPrefix}{mint.Id}");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void EnsurePublishable_WithEveryVariantPriced_Succeeds()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out var mintValueId);
        product.ApplyVariantConfiguration([.. product.Variants.Select(v =>
            new ProductVariantInput(v.Id, v.OptionValueIds, v.Sku, ProductPricing.Create(Money.Create(24.99m)), true, null))]);

        var act = () => product.EnsurePublishable();

        act.Should().NotThrow();
    }

    // =========================================================================
    // create-product — MinVariantPrice / HasSellableVariant (AC-17, RULE-16)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void MinVariantPrice_WithVariantsAtDifferentPrices_ReturnsTheLowest()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out var mintValueId);
        product.ApplyVariantConfiguration([
            new ProductVariantInput(
                product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId)).Id,
                new HashSet<Guid> { mangoValueId }, Sku.Create("SKU-MANGO"),
                ProductPricing.Create(Money.Create(29.99m)), true, null),
            new ProductVariantInput(
                product.Variants.Single(v => v.OptionValueIds.Contains(mintValueId)).Id,
                new HashSet<Guid> { mintValueId }, Sku.Create("SKU-MINT"),
                ProductPricing.Create(Money.Create(24.99m)), true, null),
        ]);

        product.MinVariantPrice.Should().Be(Money.Create(24.99m));
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void MinVariantPrice_WithNoVariantsPricedYet_ReturnsNull()
    {
        var product = BuildProductWithOneFlavourOption(out _, out _);

        product.MinVariantPrice.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void HasSellableVariant_WithEveryVariantUnavailable_ReturnsFalse()
    {
        var product = BuildProductWithOneFlavourOption(out _, out _);
        product.ApplyVariantConfiguration([.. product.Variants.Select(v =>
            new ProductVariantInput(v.Id, v.OptionValueIds, v.Sku, null, false, null))]);

        product.HasSellableVariant.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void HasSellableVariant_WithAtLeastOneAvailableVariant_ReturnsTrue()
    {
        var product = BuildProductWithOneFlavourOption(out var mangoValueId, out _);
        var mango = product.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId));
        product.ApplyVariantConfiguration([.. product.Variants.Select(v =>
            new ProductVariantInput(v.Id, v.OptionValueIds, v.Sku, null, v.Id == mango.Id, null))]);

        product.HasSellableVariant.Should().BeTrue();
    }

    // =========================================================================
    // create-product — retired flavour/nicotine fields (RULE-19, AC-18)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Product_CarriesNoDedicatedFlavourOrNicotineProperty()
    {
        var members = typeof(Product).GetProperties().Select(p => p.Name);

        members.Should().NotContain(["Flavour", "NicotineStrengthMg"],
            "RULE-19: flavour and nicotine strength are expressed only as option types");
    }

    // =========================================================================
    // create-product — helpers
    // =========================================================================

    /// <summary>
    /// Builds a product with one "Flavour" option type (Mango, Mint) and the two variants that
    /// combination generates, both still unpriced.
    /// </summary>
    private static Product BuildProductWithOneFlavourOption(out Guid mangoValueId, out Guid mintValueId)
    {
        var product = BuildProduct(pricing: null, isPublished: false);
        product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Flavour", [
                new ProductOptionValueInput(null, "Mango"),
                new ProductOptionValueInput(null, "Mint"),
            ]),
        ]);
        product.ApplyVariantConfiguration([]);

        var flavourType = product.OptionTypes.Single();
        mangoValueId = flavourType.Values.Single(v => v.Value == "Mango").Id;
        mintValueId = flavourType.Values.Single(v => v.Value == "Mint").Id;
        return product;
    }
}

// =============================================================================
// AC → Test mapping (product-catalogue)
// =============================================================================
// AC-1: Create_WithValidData_ReturnsProductWithSuppliedProperties
// AC-2: IsDiscounted_WhenPricingHasSalePrice_ReturnsTrue, IsDiscounted_WhenPricingHasNoSalePrice_ReturnsFalse
// AC-11: IsInStock_WithNoVariants_IsAlwaysTrue (stock is not tracked — RULE-16 revision)

// =============================================================================
// AC → Test mapping (create-product)
// =============================================================================
// AC-6 (edit form / aggregate state exactly as saved): UpdateDetails_WithValidData_UpdatesNameDescriptionCategoryAndBrand,
//        SetSku_WithANewSku_ReplacesTheProductsSku, SetPricing_WithNull_LeavesTheProductADraftWithNoPrice
// AC-7 (gallery reorder/primary/remove): SetGallery_WithNoImageMarkedPrimary_DefaultsTheFirstToPrimary,
//        SetGallery_WithMoreThanOneImageMarkedPrimary_KeepsExactlyOnePrimary, SetPrimaryImage_WithAGalleryImage_MakesItPrimaryAndDemotesTheOthers,
//        RemoveImage_WhenTheRemovedImageWasPrimary_PromotesTheNextImage, RemoveImage_WithAnUnknownId_DoesNothing
// AC-9 (option types produce the cartesian product of variants): ApplyVariantConfiguration_WithTwoOptionTypesOfTwoValuesEach_GeneratesFourVariants,
//        ApplyVariantConfiguration_WithNoOptionTypes_ProducesNoVariants
// AC-10a (variant image pin, ownership guard): PinVariantImage_WithAGalleryImage_PinsTheVariant, PinVariantImage_WithNull_UnpinsTheVariant,
//        PinVariantImage_WithAnImageNotInTheGallery_ThrowsProductImageNotOwnedException, PinVariantImage_WithAnUnknownVariantId_DoesNothing
// AC-12 (adding a value preserves existing variants): ApplyVariantConfiguration_WhenAValueIsAdded_KeepsEveryExistingVariantsConfiguration
// AC-13 (removing a value drops only its variants): ApplyVariantConfiguration_WhenAValueIsRemoved_DropsOnlyTheVariantsBuiltFromIt,
//        ApplyVariantConfiguration_WithARequestedRowNoLongerAValidCombination_DropsIt
// AC-14 (removing the last option type restores product-level price/stock fields — variant-count half): ApplyVariantConfiguration_WithNoOptionTypes_ProducesNoVariants
// AC-17 (customer sees a range from the lowest variant price): MinVariantPrice_WithVariantsAtDifferentPrices_ReturnsTheLowest,
//        MinVariantPrice_WithNoVariantsPricedYet_ReturnsNull
// AC-18 (no dedicated flavour/nicotine field): Product_CarriesNoDedicatedFlavourOrNicotineProperty
// AC-20 / RULE-14 (publish refused, missing parts named): EnsurePublishable_WithNoVariantsAndNoPrice_ThrowsProductNotPublishableExceptionNamingPrice,
//        EnsurePublishable_WithAnUnpricedVariant_ThrowsNamingThatVariantsToken
// AC-21 (name/description limits): UpdateDetails_WithBlankName_ThrowsProductNameRequiredException,
//        UpdateDetails_WithNameOver150Characters_ThrowsProductNameTooLongException, UpdateDetails_WithNameOfExactly150Characters_Succeeds,
//        UpdateDetails_WithDescriptionOver2000Characters_ThrowsProductDescriptionTooLongException, Create_WithBlankDescription_TreatsDescriptionAsEmpty
// AC-28 (duplicate/empty option names and values refused): SetOptionTypes_WithDuplicateNamesIgnoringCaseAndSpaces_ThrowsDuplicateOptionNameException,
//        SetOptionTypes_WithAnEmptyName_ThrowsOptionTypeNameRequiredException, SetOptionTypes_WithNoValues_ThrowsOptionTypeValueRequiredException,
//        SetOptionTypes_WithDuplicateValuesIgnoringCaseAndSpaces_ThrowsDuplicateOptionValueException, SetOptionTypes_WithValidTypes_ReplacesTheProductsOptionTypesWholesale
// AC-29 (all-unavailable published product shown out of stock rather than hidden — sellability half): HasSellableVariant_WithEveryVariantUnavailable_ReturnsFalse,
//        HasSellableVariant_WithAtLeastOneAvailableVariant_ReturnsTrue
// AC-30 (unpublish toggles status): SetPublished_TogglesTheStatus
// RULE-13 (removing a pinned image unpins its variant): RemoveImage_WhenItWasThePinOfAVariant_UnpinsThatVariant
// RULE-15 (a draft may be saved with no price): EnsurePublishable_WithNoVariantsAndAPrice_Succeeds, SetPricing_WithNull_LeavesTheProductADraftWithNoPrice
