using Bunit;
using TheShop.Web.Common.Dialogs;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

public class ProductVariantsCardTests : TestContext
{
    public ProductVariantsCardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.AddSingleton(Substitute.For<IShopDialogService>());
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PinImage_Save_UpdatesOnlyTheChosenScope(bool shared)
    {
        var (cut, changes, imageId) = RenderPinCard();
        await OpenFirstPinAsync(cut);
        var picker = cut.FindComponent<VariantImageDialog>();
        picker.Instance.SharedScopeCount.Should().Be(2);
        if (shared)
            picker.Find("[data-testid='variant-image-scope-all']").Change(true);
        await picker.Find("[data-image-id]").ClickAsync(new());
        await picker.Find("[data-testid='variant-image-save']").ClickAsync(new());
        changes.Should().ContainSingle();
        changes[0].Variants[0].PinnedImageId.Should().Be(imageId);
        changes[0].Variants[1].PinnedImageId.Should().Be(shared ? imageId : null);
        changes[0].Variants[2].PinnedImageId.Should().BeNull();
        cut.FindComponents<VariantImageDialog>().Should().BeEmpty();
    }

    [Fact]
    public async Task PinImage_CancelThenStaleCallback_DoesNotAffectReopenedPicker()
    {
        var (cut, changes, imageId) = RenderPinCard();
        await OpenFirstPinAsync(cut);
        var oldCallback = cut.FindComponent<VariantImageDialog>().Instance.OnCompleted;
        await cut.Find("[data-testid='variant-image-cancel']").ClickAsync(new());
        changes.Should().BeEmpty();
        await OpenFirstPinAsync(cut);
        await cut.InvokeAsync(() => oldCallback.InvokeAsync(new VariantImagePinResult(imageId, true)));
        changes.Should().BeEmpty();
        cut.FindComponents<VariantImageDialog>().Should().ContainSingle();
        await cut.Find("[data-testid='variant-image-save']").ClickAsync(new());
        changes.Should().ContainSingle();
        changes[0].Variants.Should().OnlyContain(v => v.PinnedImageId == null);
    }

    [Fact]
    public async Task PinImage_DisabledWhileOpen_RemovesPickerAndIgnoresLateSave()
    {
        var (cut, changes, imageId) = RenderPinCard();
        await OpenFirstPinAsync(cut);
        var callback = cut.FindComponent<VariantImageDialog>().Instance.OnCompleted;
        cut.Render(p => p.Add(c => c.Disabled, true));
        cut.FindComponents<VariantImageDialog>().Should().BeEmpty();
        await cut.InvokeAsync(() => callback.InvokeAsync(new VariantImagePinResult(imageId, false)));
        changes.Should().BeEmpty();
    }

    [Fact]
    public async Task PinImage_RemovedGalleryImage_RejectsStaleSavedId()
    {
        var (cut, changes, imageId) = RenderPinCard();
        await OpenFirstPinAsync(cut);
        var callback = cut.FindComponent<VariantImageDialog>().Instance.OnCompleted;
        cut.Render(p => p.Add(c => c.GalleryImages, []));
        await cut.InvokeAsync(() => callback.InvokeAsync(new VariantImagePinResult(imageId, false)));
        changes.Should().BeEmpty();
        cut.FindComponents<VariantImageDialog>().Should().BeEmpty();
    }

    [Fact]
    public async Task PinImage_ClearExistingPin_RequiresSaveAndDoesNotClearOnCancel()
    {
        var (cut, changes, _) = RenderPinCard(initiallyPinned: true);
        await OpenFirstPinAsync(cut);
        await cut.Find("[data-image-id]").ClickAsync(new());
        await cut.Find("[data-testid='variant-image-cancel']").ClickAsync(new());
        changes.Should().BeEmpty();
        await OpenFirstPinAsync(cut);
        cut.Find("[data-image-id]").GetAttribute("aria-pressed").Should().Be("true");
        await cut.Find("[data-image-id]").ClickAsync(new());
        await cut.Find("[data-testid='variant-image-save']").ClickAsync(new());
        changes.Should().ContainSingle();
        changes[0].Variants[0].PinnedImageId.Should().BeNull();
    }

    private (IRenderedComponent<ProductVariantsCard> Cut, List<ProductVariantsState> Changes, Guid ImageId) RenderPinCard(bool initiallyPinned = false)
    {
        var red = Guid.NewGuid();
        var blue = Guid.NewGuid();
        var small = Guid.NewGuid();
        var large = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var changes = new List<ProductVariantsState>();
        var cut = Render<ProductVariantsCard>(p => p
            .Add(c => c.ProductSku, "SKU")
            .Add(c => c.GalleryImages, [new ProductImageDto(imageId, "/pin.webp", 0, true)])
            .Add(c => c.InitialOptionTypes,
            [
                new ProductOptionTypeDto(Guid.NewGuid(), "Color", 0,
                    [new ProductOptionValueDto(red, "Red", 0), new ProductOptionValueDto(blue, "Blue", 1)]),
                new ProductOptionTypeDto(Guid.NewGuid(), "Size", 1,
                    [new ProductOptionValueDto(small, "Small", 0), new ProductOptionValueDto(large, "Large", 1)])
            ])
            .Add(c => c.InitialVariants,
            [
                new ProductVariantDto(Guid.NewGuid(), "SKU-RED-S", null, null, true, initiallyPinned ? imageId : null, [red, small], "Red / Small"),
                new ProductVariantDto(Guid.NewGuid(), "SKU-RED-L", null, null, true, null, [red, large], "Red / Large"),
                new ProductVariantDto(Guid.NewGuid(), "SKU-BLUE-S", null, null, true, null, [blue, small], "Blue / Small")
            ])
            .Add(c => c.StateChanged, state => changes.Add(state)));
        cut.FindComponent<MudTableBase>().Render(p => p.Add(c => c.Virtualize, false));
        changes.Clear();
        return (cut, changes, imageId);
    }

    private static Task OpenFirstPinAsync(IRenderedComponent<ProductVariantsCard> cut)
    {
        cut.FindComponent<MudTableBase>().Render(p => p.Add(c => c.Virtualize, false));
        return cut.FindAll("button").First(button => button.TextContent.Trim() == Strings.ProductVariants_PinImage ||
            button.TextContent.Trim() == Strings.ProductVariants_ChangeImage)
            .ClickAsync(new MouseEventArgs());
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task RemoveOptionValue_WhenItIsTheOnlyValue_RemovesOptionTypeAndVariant()
    {
        var optionTypeId = Guid.NewGuid();
        var optionValueId = Guid.NewGuid();
        var optionType = new ProductOptionTypeDto(
            optionTypeId,
            "Flavor",
            0,
            [new ProductOptionValueDto(optionValueId, "Mango", 0)]);
        var variant = new ProductVariantDto(
            Guid.NewGuid(),
            "SKU-MANGO",
            null,
            null,
            true,
            null,
            [optionValueId],
            "Mango");
        ProductVariantsState? raised = null;

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, [variant])
            .Add(component => component.StateChanged, state => raised = state));

        await cut.Find($"[aria-label='{Strings.ProductVariants_RemoveOptionValue}']")
            .ClickAsync(new MouseEventArgs());

        raised.Should().NotBeNull();
        raised!.OptionTypes.Should().BeEmpty();
        raised.Variants.Should().BeEmpty();
        cut.Markup.Should().Contain(Strings.ProductVariants_EmptyStateTitle);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WithAvailableAndUnavailableVariants_ShowsActiveAndInactiveStatuses()
    {
        var optionTypeId = Guid.NewGuid();
        var activeValueId = Guid.NewGuid();
        var inactiveValueId = Guid.NewGuid();
        var optionType = new ProductOptionTypeDto(
            optionTypeId,
            "Flavor",
            0,
            [
                new ProductOptionValueDto(activeValueId, "Mango", 0),
                new ProductOptionValueDto(inactiveValueId, "Berry", 1),
            ]);
        ProductVariantDto[] variants =
        [
            new ProductVariantDto(Guid.NewGuid(), "SKU-MANGO", null, null, true, null, [activeValueId], "Mango"),
            new ProductVariantDto(Guid.NewGuid(), "SKU-BERRY", null, null, false, null, [inactiveValueId], "Berry"),
        ];

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, variants));

        cut.FindComponent<MudTableBase>()
            .Render(parameters => parameters.Add(table => table.Virtualize, false));

        cut.Markup.Should().Contain(Strings.AddProduct_StatusActive);
        cut.Markup.Should().Contain(Strings.AddProduct_StatusInactive);
        cut.Markup.Should().NotContain(Strings.ProductVariants_StatusAvailable);
        cut.Markup.Should().NotContain(Strings.ProductVariants_StatusUnavailable);
    }

    // =========================================================================
    // Copying the first variant row's price/sale price to every other row (RULE-14 — there is no
    // product-level fallback)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task CopyPriceToAll_WithAPricedFirstRow_CopiesItToTheOtherVariants()
    {
        var (optionType, variants) = OnePricedAndOneUnpricedVariant();
        ProductVariantsState? raised = null;

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, variants)
            .Add(component => component.StateChanged, state => raised = state));

        await cut.FindAll("thead button")[0].ClickAsync(new MouseEventArgs());

        raised.Should().NotBeNull();
        raised!.Variants.Should().OnlyContain(variant => variant.OriginalPrice == 24.99m);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void CopyPriceToAll_WithNoPriceOnTheFirstRow_IsDisabled()
    {
        var (optionType, variants) = TwoUnpricedVariants();

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, variants));

        cut.FindAll("thead button")[0]
            .HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task CopySalePriceToAll_WithASalePriceOnTheFirstRow_CopiesItToTheOtherVariants()
    {
        var (optionType, variants) = OnePricedAndOneUnpricedVariant();
        ProductVariantsState? raised = null;

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, variants)
            .Add(component => component.StateChanged, state => raised = state));

        // The Sale Price column's header button is the second "Copy to all" in document order —
        // the first belongs to the Price column.
        await cut.FindAll("thead button")[1].ClickAsync(new MouseEventArgs());

        raised.Should().NotBeNull();
        raised!.Variants.Should().OnlyContain(variant => variant.SalePrice == 19.99m);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void CopyToAll_WithOnlyOneVariant_IsDisabledForBothColumns()
    {
        var optionValueId = Guid.NewGuid();
        var optionType = new ProductOptionTypeDto(
            Guid.NewGuid(), "Flavor", 0, [new ProductOptionValueDto(optionValueId, "Mango", 0)]);
        var variant = new ProductVariantDto(
            Guid.NewGuid(), "SKU-MANGO", 24.99m, 19.99m, true, null, [optionValueId], "Mango");

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, [variant]));

        cut.FindAll("thead button")
            .Should().OnlyContain(button => button.HasAttribute("disabled"));
    }

    // =========================================================================
    // Pins follow the gallery (RULE-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void GalleryImagesChanged_WhenAPinnedImageIsRemoved_UnpinsThatVariant()
    {
        var optionValueId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var optionType = new ProductOptionTypeDto(
            Guid.NewGuid(), "Flavor", 0, [new ProductOptionValueDto(optionValueId, "Mango", 0)]);
        var variant = new ProductVariantDto(
            Guid.NewGuid(), "SKU-MANGO", 24.99m, null, true, imageId, [optionValueId], "Mango");
        ProductVariantsState? raised = null;

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [new ProductImageDto(imageId, "data:image/png;base64,AA==", 0, true)])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, [variant])
            .Add(component => component.StateChanged, state => raised = state));

        raised.Should().BeNull("nothing has changed while the pinned image is still in the gallery");

        cut.Render(parameters => parameters
            .Add(component => component.GalleryImages, []));

        raised.Should().NotBeNull();
        raised!.Variants.Should().OnlyContain(v => v.PinnedImageId == null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveConfiguredOption_UsesNativeConfirmation_PreservesCancellation(bool confirmed)
    {
        var dialogs = Services.GetRequiredService<IShopDialogService>();
        dialogs.ConfirmAsync(Arg.Any<ShopConfirmationOptions>(), Arg.Any<CancellationToken>()).Returns(confirmed);
        var (optionType, variants) = OnePricedAndOneUnpricedVariant();
        ProductVariantsState? raised = null;
        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, variants)
            .Add(component => component.StateChanged, state => raised = state));

        await cut.Find($"button[aria-label='{Strings.ProductVariants_RemoveOptionType}']").ClickAsync(new());

        await dialogs.Received(1).ConfirmAsync(Arg.Is<ShopConfirmationOptions>(options =>
            options.Title == Strings.ProductVariants_RemoveOptionConfirmTitle && options.Destructive), Arg.Any<CancellationToken>());
        if (confirmed)
            raised!.Variants.Should().BeEmpty();
        else
            raised.Should().BeNull("cancelling must preserve the configured variants");
    }

    private static (ProductOptionTypeDto OptionType, ProductVariantDto[] Variants) TwoUnpricedVariants()
    {
        var mangoValueId = Guid.NewGuid();
        var berryValueId = Guid.NewGuid();
        var optionType = new ProductOptionTypeDto(
            Guid.NewGuid(),
            "Flavor",
            0,
            [
                new ProductOptionValueDto(mangoValueId, "Mango", 0),
                new ProductOptionValueDto(berryValueId, "Berry", 1),
            ]);

        ProductVariantDto[] variants =
        [
            new ProductVariantDto(Guid.NewGuid(), "SKU-MANGO", null, null, true, null, [mangoValueId], "Mango"),
            new ProductVariantDto(Guid.NewGuid(), "SKU-BERRY", null, null, true, null, [berryValueId], "Berry"),
        ];

        return (optionType, variants);
    }

    /// <summary>
    /// The first variant already carries a price and sale price; the second has neither — the
    /// fixture for the copy-to-all header buttons, which read the first row and write the rest.
    /// </summary>
    private static (ProductOptionTypeDto OptionType, ProductVariantDto[] Variants) OnePricedAndOneUnpricedVariant()
    {
        var mangoValueId = Guid.NewGuid();
        var berryValueId = Guid.NewGuid();
        var optionType = new ProductOptionTypeDto(
            Guid.NewGuid(),
            "Flavor",
            0,
            [
                new ProductOptionValueDto(mangoValueId, "Mango", 0),
                new ProductOptionValueDto(berryValueId, "Berry", 1),
            ]);

        ProductVariantDto[] variants =
        [
            new ProductVariantDto(Guid.NewGuid(), "SKU-MANGO", 24.99m, 19.99m, true, null, [mangoValueId], "Mango"),
            new ProductVariantDto(Guid.NewGuid(), "SKU-BERRY", null, null, true, null, [berryValueId], "Berry"),
        ];

        return (optionType, variants);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WithOnlyABlankOptionValue_HidesVariantTable()
    {
        var optionType = new ProductOptionTypeDto(
            Guid.NewGuid(),
            string.Empty,
            0,
            [new ProductOptionValueDto(Guid.NewGuid(), string.Empty, 0)]);

        var cut = Render<ProductVariantsCard>(parameters => parameters
            .Add(component => component.ProductSku, "SKU")
            .Add(component => component.GalleryImages, [])
            .Add(component => component.InitialOptionTypes, [optionType])
            .Add(component => component.InitialVariants, []));

        cut.FindAll("table").Should().BeEmpty();
    }
}
