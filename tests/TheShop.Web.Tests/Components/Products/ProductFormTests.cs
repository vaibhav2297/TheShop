using TheShop.Web.Common.Notifications;
using Bunit;
using Bunit.TestDoubles;
using TheShop.Web.Common.Dialogs;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.CreateProduct;
using TheShop.Application.Features.Products.Commands.UpdateProduct;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Exceptions;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

/// <summary>
/// Tests for <see cref="ProductForm"/> — the create/edit product form's cross-field behaviour:
/// offering a just-picked image to the variant pin picker before anything is saved (FR-16, AC-10a),
/// refusing to publish while a variant has no price (RULE-14, AC-20), and dropping the product's own
/// price from the command once it has variants, because customers then pay the variant's price
/// (FR-19, RULE-19).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class ProductFormTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    private readonly CategoryLookupDto _category = new(Guid.NewGuid(), "Disposables");
    private readonly BrandLookupDto _brand = new(Guid.NewGuid(), "Elf Bar");

    public ProductFormTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);
        Services.AddSingleton(Substitute.For<IShopDialogService>());


        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
    }

    private IRenderedComponent<ProductForm> RenderCreateForm() =>
        Render<ProductForm>(parameters => parameters
            .Add(component => component.Mode, ProductFormMode.Create)
            .Add(component => component.Categories, [_category])
            .Add(component => component.Brands, [_brand]));

    private IRenderedComponent<ProductForm> RenderEditForm(AdminProductDto product) =>
        Render<ProductForm>(parameters => parameters
            .Add(component => component.Mode, ProductFormMode.Edit)
            .Add(component => component.InitialData, product)
            .Add(component => component.Categories, [_category])
            .Add(component => component.Brands, [_brand]));

    private static ShopUploadedImage NewlyPickedImage(string fileName = "mango.png") =>
        new([1, 2, 3], fileName, "image/png", $"data:image/png;base64,AQID");

    private Task PickImagesAsync(IRenderedComponent<ProductForm> cut, params ShopUploadedImage[] images) =>
        cut.InvokeAsync(() => cut.FindComponent<ShopImageUpload>().Instance.FilesChanged.InvokeAsync(images));

    /// <summary>
    /// Raises the variants card's state the way the card itself would, so the form is exercised
    /// through the same contract without driving the virtualized variant table.
    /// </summary>
    private Task SetVariantsAsync(IRenderedComponent<ProductForm> cut, params VariantInput[] variants)
    {
        var optionValueIds = variants.SelectMany(v => v.OptionValueIds).Distinct().ToList();
        var optionTypes = new List<OptionTypeInput>
        {
            new(Guid.NewGuid(), "Flavour", [.. optionValueIds.Select((id, index) => new OptionValueInput(id, $"Value {index}"))]),
        };
        var labels = variants.ToDictionary(v => v.Id!.Value, v => $"Variant {v.Sku}");

        return cut.InvokeAsync(() => cut.FindComponent<ProductVariantsCard>().Instance.StateChanged
            .InvokeAsync(new ProductVariantsState(optionTypes, variants, labels)));
    }

    private static VariantInput Variant(string sku, decimal? price) =>
        new(Guid.NewGuid(), [Guid.NewGuid()], sku, price, null, true, null);

    private static Task SetDescriptionAsync(IRenderedComponent<ProductForm> cut, string html, int textLength) =>
        cut.InvokeAsync(() => cut.FindComponent<ShopRichTextEditor>().Instance.OnTextChanged(html, textLength));

    /// <summary>
    /// Raises the content card's state the way the card itself would, so the form is exercised
    /// through the same contract without driving its row markup directly.
    /// </summary>
    private static Task SetSpecificationsAsync(IRenderedComponent<ProductForm> cut, params SpecificationInput[] specifications) =>
        cut.InvokeAsync(() => cut.FindComponent<ProductContentCard>().Instance.StateChanged
            .InvokeAsync(new ProductSpecificationsState(specifications)));

    // =========================================================================
    // The pin picker sees images that have not been saved yet (FR-16, AC-10a)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task PickAnImage_BeforeAnythingIsSaved_OffersItToTheVariantPinPicker()
    {
        var cut = RenderCreateForm();

        await PickImagesAsync(cut, NewlyPickedImage());

        cut.FindComponent<ProductVariantsCard>().Instance.GalleryImages
            .Should().ContainSingle("a just-picked image is pinnable straight away — nothing has to be saved first");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task PickAnImage_BeforeAnythingIsSaved_IdentifiesItByItsClientIdAndPreviewsIt()
    {
        var cut = RenderCreateForm();
        var picked = NewlyPickedImage();

        await PickImagesAsync(cut, picked);

        var offered = cut.FindComponent<ProductVariantsCard>().Instance.GalleryImages.Single();
        offered.Id.Should().Be(picked.ClientId);
        offered.Url.Should().Be(picked.PreviewUrl, "the data: URL is what the dialog can render before an upload exists");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task PickImages_WithOneFailingItsGuard_OffersOnlyTheValidOne()
    {
        var cut = RenderCreateForm();
        var valid = NewlyPickedImage();
        var rejected = NewlyPickedImage("huge.png") with { Error = "Too large" };

        await PickImagesAsync(cut, valid, rejected);

        cut.FindComponent<ProductVariantsCard>().Instance.GalleryImages
            .Should().ContainSingle().Which.Id.Should().Be(valid.ClientId);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task RemoveAPickedImage_ClearsItFromThePinPicker()
    {
        var cut = RenderCreateForm();
        var first = NewlyPickedImage("first.png");
        var second = NewlyPickedImage("second.png");
        await PickImagesAsync(cut, first, second);

        await PickImagesAsync(cut, first);

        cut.FindComponent<ProductVariantsCard>().Instance.GalleryImages
            .Should().ContainSingle().Which.Id.Should().Be(first.ClientId);
    }

    // =========================================================================
    // Publishing needs every variant priced (RULE-14, AC-20)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Publish_WithAVariantMissingItsPrice_ShowsWhichRowsAreIncomplete()
    {
        var cut = RenderCreateForm();
        await ConfigureVariantAsync(cut);

        await SetStatusAsync(cut, published: true);

        cut.Find(".shop-product-variant-table").TextContent.Should().Contain(Strings.Product_PriceRequired);
        cut.Markup.Should().NotContain(string.Format(Strings.ProductVariants_MissingPrice, 1, 1));
        cut.FindAll("form > .shop-product-error").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Publish_WithAVariantMissingItsPrice_FlagsThatRowOnTheVariantsCard()
    {
        var cut = RenderCreateForm();
        var unpriced = Variant("SKU-MINT", null);
        await SetVariantsAsync(cut, Variant("SKU-MANGO", 24.99m), unpriced);

        await SetStatusAsync(cut, published: true);

        cut.FindComponent<ProductVariantsCard>().Instance.FlaggedVariantIds
            .Should().ContainSingle().Which.Should().Be(unpriced.Id!.Value);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Publish_WithEveryVariantPriced_ShowsNoIncompleteRowsNotice()
    {
        var cut = RenderCreateForm();
        await SetVariantsAsync(cut, Variant("SKU-MANGO", 24.99m), Variant("SKU-MINT", 26.99m));

        await SetStatusAsync(cut, published: true);

        cut.FindComponent<ProductVariantsCard>().Instance.FlaggedVariantIds.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Draft_WithAVariantMissingItsPrice_IsNotFlagged()
    {
        var cut = RenderCreateForm();
        await SetVariantsAsync(cut, Variant("SKU-MANGO", 24.99m), Variant("SKU-MINT", null));

        cut.FindComponent<ProductVariantsCard>().Instance.FlaggedVariantIds
            .Should().BeEmpty("an unpublished product may be saved at any level of completeness (RULE-15)");
        cut.Markup.Should().NotContain(string.Format(Strings.ProductVariants_MissingPrice, 1, 2));
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Publish_WithAVariantMissingItsPrice_PreservesServerRefusalAndFlags()
    {
        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await ConfigureVariantAsync(cut);
        await SetStatusAsync(cut, published: true);
        cut.Find(".shop-product-variant-table input").GetAttribute("aria-required").Should().Be("true");

        var variantId = cut.FindComponent<ProductVariantsCard>().Instance.FlaggedVariantIds.Single();
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<AdminProductDto>(ProductNotPublishableException.MessageResourceKey,
                [$"{ProductNotPublishableException.VariantPriceTokenPrefix}{variantId}"]));
        await cut.Find("form").SubmitAsync();
        cut.FindComponent<ProductVariantsCard>().Instance.FlaggedVariantIds.Should().NotBeEmpty();
        _notifications.DidNotReceive().Show(Arg.Any<string>(), ShopNotificationKind.Error);
        await _mediator.Received(1).Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>());
        Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain(Routes.Admin.ManageProducts);
    }

    // =========================================================================
    // A variant product carries no price of its own (FR-19, RULE-19)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Save_OnceTheProductHasVariants_SendsNoProductLevelPrice()
    {
        CreateProductCommand? dispatched = null;
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                dispatched = call.Arg<CreateProductCommand>();
                return Result.Ok(ExampleSavedProduct());
            });

        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 19.99m);
        await SetVariantsAsync(cut, Variant("SKU-MANGO", 24.99m));

        await cut.Find("form").SubmitAsync();

        dispatched.Should().NotBeNull();
        dispatched!.OriginalPrice.Should().BeNull(
            "the field is hidden once variants exist, so persisting its value would leave a price nobody can see");
        dispatched.SalePrice.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Save_WithNoVariants_SendsTheProductsOwnPrice()
    {
        CreateProductCommand? dispatched = null;
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                dispatched = call.Arg<CreateProductCommand>();
                return Result.Ok(ExampleSavedProduct());
            });

        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 19.99m);

        await cut.Find("form").SubmitAsync();

        dispatched.Should().NotBeNull();
        dispatched!.OriginalPrice.Should().Be(19.99m);
    }

    // =========================================================================
    // Description and specifications ride the existing save action (product-description AC-1, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Save_WithDescriptionAndSpecifications_DispatchesThemInTheCommand()
    {
        CreateProductCommand? dispatched = null;
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                dispatched = call.Arg<CreateProductCommand>();
                return Result.Ok(ExampleSavedProduct());
            });

        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);
        await SetDescriptionAsync(cut, "<p>Long-lasting.</p>", 13);
        await SetSpecificationsAsync(cut, new SpecificationInput(null, "Material", "Stainless steel", 0));

        await cut.Find("form").SubmitAsync();

        dispatched.Should().NotBeNull();
        dispatched!.Description.Should().Be("<p>Long-lasting.</p>");
        dispatched.Specifications.Should().ContainSingle(s => s.Name == "Material" && s.Value == "Stainless steel");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Save_EditDescriptionForVariantProduct_PreservesLoadedVariantsInUpdateCommand()
    {
        UpdateProductCommand? dispatched = null;
        _mediator.Send(Arg.Any<UpdateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                dispatched = call.Arg<UpdateProductCommand>();
                return Result.Ok(ExampleSavedProduct());
            });

        var optionValueId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var product = new AdminProductDto(
            Guid.NewGuid(), "Elf Bar BC5000", null, "ELF-BC5000",
            _category.Id, _category.Name, _brand.Id, _brand.Name,
            null, null, false, [],
            [new ProductOptionTypeDto(Guid.NewGuid(), "Flavour", 0, [new ProductOptionValueDto(optionValueId, "Mango", 0)])],
            [], [new ProductVariantDto(variantId, "ELF-BC5000-MANGO", 24.99m, null, true, null, [optionValueId], "Mango")],
            "row-version-1");
        var cut = RenderEditForm(product);

        await SetDescriptionAsync(cut, "<p>Updated description.</p>", 20);
        await cut.Find("form").SubmitAsync();

        dispatched.Should().NotBeNull();
        dispatched!.OptionTypes.Should().ContainSingle();
        dispatched.Variants.Should().ContainSingle().Which.Id.Should().Be(variantId);
        dispatched.Description.Should().Be("<p>Updated description.</p>");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Save_WithWhitespaceOnlyDescription_SendsNullDescription()
    {
        CreateProductCommand? dispatched = null;
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                dispatched = call.Arg<CreateProductCommand>();
                return Result.Ok(ExampleSavedProduct());
            });

        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);
        await SetDescriptionAsync(cut, "   ", 0);

        await cut.Find("form").SubmitAsync();

        dispatched.Should().NotBeNull();
        dispatched!.Description.Should().BeNull("description is optional; whitespace-only content is not entered (RULE-2)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Save_WhenTheServerFlagsADuplicateSpecificationName_FlagsThoseRowsOnTheContentCard()
    {
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<AdminProductDto>(ProductErrorKeys.SpecificationNameDuplicated, ["0", "1"]));

        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);
        await SetSpecificationsAsync(
            cut,
            new SpecificationInput(null, "Material", "Steel", 0),
            new SpecificationInput(null, "material", "Aluminum", 1));

        await cut.Find("form").SubmitAsync();

        cut.FindComponent<ProductContentCard>().Instance.FlaggedRowPositions.Should().BeEquivalentTo([0, 1]);
        _notifications.DidNotReceive().Show(Arg.Any<string>(), ShopNotificationKind.Error);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Save_WhenTheSaveFails_PreservesTheEnteredDescriptionAndSpecificationRows()
    {
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<AdminProductDto>("Product_CreateFailed"));

        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);
        await SetDescriptionAsync(cut, "<p>Do not lose this.</p>", 16);
        await SetSpecificationsAsync(cut, new SpecificationInput(null, "Material", "Stainless steel", 0));

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Stainless steel",
            "a failed save must not silently overwrite or drop entered content (AC-15)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_BeforeAnyEdit_DoesNotConfirmExternalNavigation()
    {
        var cut = RenderCreateForm();

        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse(
            "the content card's initial seeding callback (empty rows, no description) is not a staff edit");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task EditingTheDescription_ConfirmsExternalNavigation()
    {
        var cut = RenderCreateForm();

        await SetDescriptionAsync(cut, "<p>Edited.</p>", 7);

        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue(
            "an unsaved description edit must warn before leaving (AC-14)");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InternalNavigation_WithUnsavedChanges_UsesNativeConfirmationResult(bool confirmed)
    {
        var dialogs = Services.GetRequiredService<IShopDialogService>();
        dialogs.ConfirmAsync(Arg.Any<ShopConfirmationOptions>(), Arg.Any<CancellationToken>()).Returns(confirmed);
        var cut = RenderCreateForm();
        await SetDescriptionAsync(cut, "<p>Edited.</p>", 7);
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        await cut.InvokeAsync(() => navigation.NavigateTo(Routes.Admin.ManageProducts));

        cut.WaitForAssertion(() => navigation.History.Should().ContainSingle());
        (navigation.History.Single().State == NavigationState.Prevented).Should().Be(!confirmed);
        await dialogs.Received(1).ConfirmAsync(Arg.Is<ShopConfirmationOptions>(options =>
            options.Title == Strings.ProductForm_UnsavedChangesTitle && options.Destructive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task EditingASpecificationRow_ConfirmsExternalNavigation()
    {
        var cut = RenderCreateForm();

        await SetSpecificationsAsync(cut, new SpecificationInput(null, "Material", "Steel", 0));

        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue(
            "an unsaved specification edit must warn before leaving (AC-14)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Save_WhenTheServerReturnsAVariantPublishFailure_DoesNotFlagAnySpecificationRow()
    {
        // Result.ErrorArgs is shared between the variant-publish and specification flag paths —
        // a variant-shaped token must not be misread as a specification row position.
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<AdminProductDto>(
                ProductNotPublishableException.MessageResourceKey,
                [$"{ProductNotPublishableException.VariantPriceTokenPrefix}{Guid.NewGuid()}"]));

        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);
        await SetSpecificationsAsync(cut, new SpecificationInput(null, "Material", "Steel", 0));

        await cut.Find("form").SubmitAsync();

        cut.FindComponent<ProductContentCard>().Instance.FlaggedRowPositions.Should().BeEmpty();
        _notifications.Received(1).Show(Arg.Any<string>(), ShopNotificationKind.Error);
    }

    // =========================================================================
    // Duplicate name refused — error surfaced, input preserved, no navigation (AC-22)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Save_WhenTheProductNameAlreadyExists_ShowsAnErrorSnackbarAndDoesNotNavigate()
    {
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<AdminProductDto>("Product_NameAlreadyExists"));
        var cut = RenderCreateForm();
        var navManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);

        await cut.Find("form").SubmitAsync();

        _notifications.Received(1).Show(Arg.Any<string>(), ShopNotificationKind.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageProducts);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Save_WhenTheProductNameAlreadyExists_PreservesTheEnteredName()
    {
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<AdminProductDto>("Product_NameAlreadyExists"));
        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);

        await cut.Find("form").SubmitAsync();

        cut.Find("input[data-testid='product-name']").GetAttribute("value").Should().Be("Elf Bar BC5000",
            "everything already entered must be preserved so the staff member can correct and resubmit (AC-23 / FR-23)");
    }

    // =========================================================================
    // Technical failure — a clear message, everything preserved, no navigation (AC-32)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Save_WhenTheSaveFailsForATechnicalReason_ShowsAnErrorSnackbarAndDoesNotNavigate()
    {
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<AdminProductDto>("Product_CreateFailed"));
        var cut = RenderCreateForm();
        var navManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);

        await cut.Find("form").SubmitAsync();

        _notifications.Received(1).Show(Arg.Any<string>(), ShopNotificationKind.Error);
        navManager.Uri.Should().NotContain(Routes.Admin.ManageProducts);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    [Theory]
    [InlineData("bad")]
    [InlineData("-1")]
    public async Task Save_InvalidNativePrice_DoesNotDispatchOldAmount(string draft)
    {
        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await SetPriceAsync(cut, 24.99m);
        var input = cut.FindComponents<ShopMoneyField>()[0].Find("input");
        await input.InputAsync(new ChangeEventArgs { Value = draft });
        await cut.Find("form").SubmitAsync();
        await _mediator.DidNotReceive().Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>());
        input.GetAttribute("value").Should().Be(draft);
        input.GetAttribute("aria-invalid").Should().Be("true");
    }

    [Fact]
    public async Task Save_PendingNativePriceAndClearedSalePrice_CommitsBeforeDispatch()
    {
        CreateProductCommand? saved = null;
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = call.Arg<CreateProductCommand>();
            return Result.Ok(ExampleSavedProduct());
        });
        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        var fields = cut.FindComponents<ShopMoneyField>();
        await fields[1].Find("input").InputAsync(new ChangeEventArgs { Value = "19.99" });
        await fields[1].Find("input").BlurAsync(new());
        await fields[1].Find("input").InputAsync(new ChangeEventArgs { Value = "" });
        await fields[0].Find("input").InputAsync(new ChangeEventArgs { Value = "35.125" });
        await cut.Find("form").SubmitAsync();
        saved.Should().NotBeNull();
        saved!.OriginalPrice.Should().Be(35.125m);
        saved.SalePrice.Should().BeNull();
    }

    private async Task FillRequiredDetailsAsync(IRenderedComponent<ProductForm> cut)
    {
        await cut.Find("input[data-testid='product-name']").InputAsync(new ChangeEventArgs { Value = "Elf Bar BC5000" });
        var selects = cut.FindComponents<ShopSelect<Guid?>>();
        await cut.InvokeAsync(() => selects.Single(c => c.Instance.Label == Strings.AddProduct_CategoryLabel).Instance.ValueChanged.InvokeAsync(_category.Id));
        await cut.InvokeAsync(() => selects.Single(c => c.Instance.Label == Strings.AddProduct_BrandLabel).Instance.ValueChanged.InvokeAsync(_brand.Id));
    }

    private static async Task SetPriceAsync(IRenderedComponent<ProductForm> cut, decimal price)
    {
        var input = cut.FindComponents<ShopMoneyField>()[0].Find("input");
        await input.InputAsync(new ChangeEventArgs { Value = price.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        await input.BlurAsync(new());
    }

    private static Task SetStatusAsync(IRenderedComponent<ProductForm> cut, bool published) =>
        cut.InvokeAsync(() => cut.FindComponents<ShopChip>()[published ? 0 : 1].Instance.SelectedChanged.InvokeAsync(true));

    // =========================================================================
    // Description (FR-1, FR-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_ShowsTheDescriptionLabelAndHeading()
    {
        var cut = RenderCreateForm();

        cut.Markup.Should().Contain(Strings.ProductForm_GeneralInfoHeading);
        cut.Markup.Should().Contain(Strings.AddProduct_DescriptionLabel);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task EditingTheDescription_RaisesDescriptionChanged()
    {
        var cut = RenderCreateForm();

        await cut.InvokeAsync(() => cut.FindComponent<ShopRichTextEditor>().Instance.OnTextChanged("<p>New</p>", 3));

        cut.FindComponent<ShopRichTextEditor>().Instance.Value.Should().Be("<p>New</p>");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task TextLengthChanged_UpdatesTheCounter()
    {
        var cut = RenderCreateForm();

        await cut.InvokeAsync(() => cut.FindComponent<ShopRichTextEditor>().Instance.OnTextChanged("<p>Hello</p>", 5));

        cut.Markup.Should().Contain(string.Format(Strings.AddProduct_DescriptionCounter, 5, 20_000));
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task UnsupportedPaste_ShowsTheFormattingRemovedNotice()
    {
        var cut = RenderCreateForm();

        await cut.InvokeAsync(() => cut.FindComponent<ShopRichTextEditor>().Instance.OnPasteUnsupported());

        cut.Markup.Should().Contain(Strings.AddProduct_DescriptionFormattingRemoved);
    }

    private static AdminProductDto ExampleSavedProduct() =>
        new(Guid.NewGuid(), "Elf Bar BC5000", "A long-lasting disposable vape.", "ELF-BC5000",
            Guid.NewGuid(), "Disposables", Guid.NewGuid(), "Elf Bar",
            null, null, false, [], [], [], [], "row-version-1");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PickerUpload_AppendsToGallery_AndOnlySaveChangesThePin(bool cancelPin)
    {
        JSInterop.SetupModule("./js/shopImageUpload.js").Setup<string>("createObjectUrl", _ => true).SetResult("blob:picker-owned");
        CreateProductCommand? saved = null;
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = call.Arg<CreateProductCommand>();
            return Result.Fail<AdminProductDto>(ProductErrorKeys.CreateFailed);
        });
        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await ConfigureVariantAsync(cut);
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == Strings.ProductVariants_PinImage).ClickAsync(new());
        var picker = cut.FindComponent<VariantImageDialog>();
        picker.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "from-picker.png", contentType: "image/png"));
        cut.WaitForAssertion(() => cut.FindComponent<ShopImageUpload>().Instance.Files.Should().ContainSingle());
        var image = cut.FindComponent<ShopImageUpload>().Instance.Files.Single();
        picker.Find("[data-image-id] .shop-image-tile-surface").GetAttribute("aria-pressed").Should().Be("true");
        await picker.Find(cancelPin ? "[data-testid='variant-image-cancel']" : "[data-testid='variant-image-save']").ClickAsync(new());
        cut.FindComponent<ShopImageUpload>().Instance.Files.Single().Should().BeSameAs(image);
        await cut.Find("form").SubmitAsync();
        saved.Should().NotBeNull();
        var entry = saved!.Gallery.Should().ContainSingle().Subject;
        entry.ClientId.Should().Be(image.ClientId);
        entry.Upload!.Content.Should().Equal(1, 2, 3);
        entry.IsPrimary.Should().BeTrue();
        saved.Variants.Single().PinnedImageId.Should().Be(cancelPin ? null : image.ClientId);
        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
        await cut.Find(".shop-image-upload .shop-image-tile-remove").ClickAsync(new());
        cut.FindComponent<ProductVariantsCard>().Instance.GalleryImages.Should().BeEmpty();
        await cut.Find("form").SubmitAsync();
        saved.Gallery.Should().BeEmpty();
        saved.Variants.Single().PinnedImageId.Should().BeNull();
        JSInterop.Invocations.Should().ContainSingle(call => call.Identifier == "revokeObjectUrl");
    }

    [Theory]
    [InlineData("text/plain", 3)]
    [InlineData("image/png", 3000000)]
    public async Task PickerUpload_RejectedFile_ShowsErrorWithoutReadingOrOfferingAPin(string type, long size)
    {
        var cut = RenderCreateForm();
        await ConfigureVariantAsync(cut);
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == Strings.ProductVariants_PinImage).ClickAsync(new());
        var file = Substitute.For<IBrowserFile>();
        file.Name.Returns("rejected.png"); file.ContentType.Returns(type); file.Size.Returns(size);
        var picker = cut.FindComponent<VariantImageDialog>();
        await cut.InvokeAsync(() => picker.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { file }));
        file.DidNotReceiveWithAnyArgs().OpenReadStream(default, Xunit.TestContext.Current.CancellationToken);
        picker.FindAll("[data-image-id]").Should().BeEmpty();
        picker.Find("[role=alert]").TextContent.Should().NotBeNullOrWhiteSpace();
        cut.FindComponent<ShopImageUpload>().Instance.Files.Single().Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_PendingFileAndPrice_CommitsBothAndSuppressesDuplicateSubmission()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var file = Substitute.For<IBrowserFile>();
        file.Name.Returns("pending.png"); file.ContentType.Returns("image/png"); file.Size.Returns(2);
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(new GatedStream(release.Task));
        CreateProductCommand? saved = null;
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = call.Arg<CreateProductCommand>();
            return Result.Fail<AdminProductDto>(ProductErrorKeys.CreateFailed);
        });
        var cut = RenderCreateForm();
        await FillRequiredDetailsAsync(cut);
        await cut.FindComponents<ShopMoneyField>()[0].Find("input").InputAsync(new ChangeEventArgs { Value = "42.75" });
        var pending = cut.InvokeAsync(() => cut.FindComponent<ShopImageUpload>().Instance.AddFilesAsync(new[] { file }));
        cut.WaitForAssertion(() => cut.FindAll(".shop-image-tile-loader").Should().ContainSingle());
        var save = cut.Find("form").SubmitAsync();
        await cut.Find("form").SubmitAsync();
        saved.Should().BeNull();
        release.SetResult();
        await pending;
        await save;
        saved!.OriginalPrice.Should().Be(42.75m);
        saved.Gallery.Should().ContainSingle();
        await _mediator.Received(1).Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("OriginalPrice", false)]
    [InlineData("OriginalPrice.Value", false)]
    [InlineData("OriginalPrice", true)]
    [InlineData("OriginalPrice.Value", true)]
    public async Task Save_CommandValidation_MapsPriceAndName_AndEditingClearsErrors(string pricePath, bool edit)
    {
        var failure = Result.Fail<AdminProductDto>(ProductErrorKeys.NameRequired, [],
            [new("Name", ProductErrorKeys.NameRequired), new(pricePath, ProductErrorKeys.PriceRequired)]);
        _mediator.Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>())
            .Returns(failure);
        _mediator.Send(Arg.Any<UpdateProductCommand>(), Arg.Any<CancellationToken>()).Returns(failure);
        var cut = edit ? RenderEditForm(ExampleSavedProduct()) : RenderCreateForm();
        await cut.Find("form").SubmitAsync();
        cut.Find("[data-testid=product-name]").GetAttribute("aria-invalid").Should().Be("true");
        cut.FindComponents<ShopMoneyField>()[0].Find("input").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find(".shop-field-error").TextContent.Should().Contain(ProductErrorKeys.NameRequired);
        cut.FindAll(".validation-summary-errors").Should().BeEmpty();
        _notifications.DidNotReceive().Show(Arg.Any<string>(), ShopNotificationKind.Error);
        await cut.Find("[data-testid=product-name]").InputAsync(new ChangeEventArgs { Value = "Corrected" });
        cut.Find("[data-testid=product-name]").GetAttribute("aria-invalid").Should().NotBe("true");
        await cut.Find("form").SubmitAsync();
        if (edit)
            await _mediator.Received(2).Send(Arg.Any<UpdateProductCommand>(), Arg.Any<CancellationToken>());
        else
            await _mediator.Received(2).Send(Arg.Any<CreateProductCommand>(), Arg.Any<CancellationToken>());
    }

    private static async Task ConfigureVariantAsync(IRenderedComponent<ProductForm> cut)
    {
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == Strings.ProductVariants_AddVariant).ClickAsync(new());
        var fields = cut.FindAll(".shop-product-option input");
        await fields[0].InputAsync(new ChangeEventArgs { Value = "Flavor" });
        await cut.FindAll(".shop-product-option input")[1].InputAsync(new ChangeEventArgs { Value = "Mango" });
    }

    private sealed class GatedStream(Task release) : MemoryStream([1, 2])
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await release.WaitAsync(cancellationToken);
            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10a (pin a gallery image to a variant): PickAnImage_BeforeAnythingIsSaved_OffersItToTheVariantPinPicker,
//         PickAnImage_BeforeAnythingIsSaved_IdentifiesItByItsClientIdAndPreviewsIt,
//         PickImages_WithOneFailingItsGuard_OffersOnlyTheValidOne
// RULE-13 (removing an image unpins it): RemoveAPickedImage_ClearsItFromThePinPicker
// AC-20 / RULE-14 (publish refused, flagged per row): Publish_WithAVariantMissingItsPrice_ShowsWhichRowsAreIncomplete,
//         Publish_WithAVariantMissingItsPrice_FlagsThatRowOnTheVariantsCard,
//         Publish_WithAVariantMissingItsPrice_PreservesServerRefusalAndFlags,
//         Publish_WithEveryVariantPriced_ShowsNoIncompleteRowsNotice
// RULE-15 (a draft may be incomplete): Draft_WithAVariantMissingItsPrice_IsNotFlagged
// FR-19 / RULE-19 (a variant product has no price of its own): Save_OnceTheProductHasVariants_SendsNoProductLevelPrice,
//         Save_WithNoVariants_SendsTheProductsOwnPrice
// AC-22 (duplicate product name refused, input preserved): Save_WhenTheProductNameAlreadyExists_ShowsAnErrorSnackbarAndDoesNotNavigate,
//         Save_WhenTheProductNameAlreadyExists_PreservesTheEnteredName
// AC-32 (a failed save preserves everything, never partly saves): Save_WhenTheSaveFailsForATechnicalReason_ShowsAnErrorSnackbarAndDoesNotNavigate
// product-description AC-1/AC-5 (description/specifications ride the existing save action): Save_WithDescriptionAndSpecifications_DispatchesThemInTheCommand,
//         Save_WithWhitespaceOnlyDescription_SendsNullDescription
// product-description AC-8 (duplicate names flagged on the content card): Save_WhenTheServerFlagsADuplicateSpecificationName_FlagsThoseRowsOnTheContentCard,
//         Save_WhenTheServerReturnsAVariantPublishFailure_DoesNotFlagAnySpecificationRow
// product-description AC-14 (unsaved description/specification changes warn before leaving): Render_BeforeAnyEdit_DoesNotConfirmExternalNavigation,
//         EditingTheDescription_ConfirmsExternalNavigation, EditingASpecificationRow_ConfirmsExternalNavigation
// product-description AC-15 (failed save preserves local content, no silent overwrite): Save_WhenTheSaveFails_PreservesTheEnteredDescriptionAndSpecificationRows
