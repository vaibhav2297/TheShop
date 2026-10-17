using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.Dialogs;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Common.UI;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.CreateProduct;
using TheShop.Application.Features.Products.Commands.UpdateProduct;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Products;

/// <summary>
/// Shared create/edit editor for product fields, rich text, gallery, specifications and variants.
/// Owns command mapping, save dispatch, field feedback and unsaved-navigation recovery.
/// </summary>
public partial class ProductForm : ShopComponentBase
{
    private static readonly IReadOnlyDictionary<string, string> CommandFieldMap = new Dictionary<string, string>
    {
        ["OriginalPrice.Value"] = nameof(ProductFormModel.OriginalPrice),
        ["SalePrice.Value"] = nameof(ProductFormModel.SalePrice),
    };

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private IShopDialogService DialogService { get; set; } = default!;

    /// <summary>Whether this form creates a new product or edits an existing one.</summary>
    [Parameter, EditorRequired]
    public ProductFormMode Mode { get; set; }

    /// <summary>Saved product state, required in Edit mode and ignored in Create mode.</summary>
    [Parameter]
    public AdminProductDto? InitialData { get; set; }

    /// <summary>The Active categories offered by the category select (AC-23).</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<CategoryLookupDto> Categories { get; set; } = [];

    /// <summary>The Active brands offered by the brand select (AC-23).</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<BrandLookupDto> Brands { get; set; } = [];

    private ShopImageUpload? _imageUpload;
    private readonly ProductFormModel _model = new();
    private ShopCommandValidation _validation = default!;
    private readonly string _descriptionLabelId = $"product-description-{Guid.NewGuid():N}";
    private string DescriptionErrorId => $"{_descriptionLabelId}-error";
    private int _textLength;
    private bool _showPasteNotice;
    private ShopMoneyField? _originalPriceField, _salePriceField;
    private ProductVariantsCard _variantsCard = default!;
    private bool _dirty;
    private bool _initialized;

    private string? _rowVersion;

    private IReadOnlyList<ShopUploadedImage> _galleryImages = [];
    private IReadOnlyList<ProductGalleryEntry> _gallery = [];
    private IReadOnlyList<ProductImageDto> _galleryPreview = [];
    private IReadOnlyList<OptionTypeInput> _optionTypes = [];
    private IReadOnlyList<VariantInput> _variants = [];
    private IReadOnlyDictionary<Guid, string> _variantLabels = new Dictionary<Guid, string>();
    private IReadOnlyList<Guid> _variantsFlaggedByServer = [];
    private bool _variantsSeeded;
    private IReadOnlyList<SpecificationInput> _specifications = [];
    private IReadOnlyList<int> _specificationsFlaggedByServer = [];
    private bool _specificationsSeeded;

    private bool HasVariants => _optionTypes.Count > 0;

    /// <summary>
    /// The lowest price a customer could pay across the variants, or <c>null</c> while none is
    /// priced — the sale-aware effective price, matching what <c>Product.MinVariantPrice</c>
    /// reports server-side.
    /// </summary>
    private decimal? LowestVariantPrice => _variants
        .Select(v => v.SalePrice ?? v.OriginalPrice)
        .Where(price => price is not null)
        .Min();

    /// <summary>
    /// The variants still missing a price. The product has no price of its own for them to fall
    /// back on — RULE-14 requires each variant to carry its own — so an omission has to be
    /// corrected rather than defaulted.
    /// </summary>
    private IReadOnlyList<Guid> UnpricedVariantIds =>
        [.. _variants.Where(v => v.OriginalPrice is null && v.Id is not null).Select(v => v.Id!.Value)];

    private IReadOnlyList<Guid> FlaggedVariantIds =>
        [.. (_model.IsPublished ? UnpricedVariantIds : []).Concat(_variantsFlaggedByServer).Distinct()];

    private IReadOnlyList<CategoryLookupDto> DisplayCategories =>
        _model.CategoryId is { } id && InitialData is not null && Categories.All(c => c.Id != id)
            ? [.. Categories, new CategoryLookupDto(id, InitialData.CategoryName)]
            : Categories;

    private IReadOnlyList<BrandLookupDto> DisplayBrands =>
        _model.BrandId is { } id && InitialData is not null && Brands.All(b => b.Id != id)
            ? [.. Brands, new BrandLookupDto(id, InitialData.BrandName)]
            : Brands;

    private IReadOnlyList<ShopSelectOption<Guid?>> CategoryOptions =>
        [.. DisplayCategories.Select(category => new ShopSelectOption<Guid?>(category.Id, category.Name))];
    private IReadOnlyList<ShopSelectOption<Guid?>> BrandOptions =>
        [.. DisplayBrands.Select(brand => new ShopSelectOption<Guid?>(brand.Id, brand.Name))];
    protected string Classname => ShopCssClass.Join("shop-native", "shop-product-form", Class);

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (_initialized)
            return;

        _initialized = true;

        if (Mode != ProductFormMode.Edit || InitialData is not { } data)
        {
            _variantsSeeded = true;
            return;
        }

        _model.Name = data.Name;
        _model.Description = data.Description;
        _model.Sku = data.Sku;
        _model.CategoryId = data.CategoryId;
        _model.BrandId = data.BrandId;
        _model.OriginalPrice = data.OriginalPrice;
        _model.SalePrice = data.SalePrice;
        _model.IsPublished = data.IsPublished;
        _rowVersion = data.RowVersion;

        // The command carries the gallery's whole desired state, so the saved images have to be
        // in it from the start — otherwise saving an untouched form would clear them.
        ApplyGallery([.. data.Images.OrderBy(image => image.Position)
            .Select(image => ShopUploadedImage.Existing(image.Id, image.Url))]);

        _optionTypes = [.. data.OptionTypes.OrderBy(type => type.Position).Select(type => new OptionTypeInput(
            type.Id,
            type.Name,
            [.. type.Values.OrderBy(value => value.Position)
                .Select(value => new OptionValueInput(value.Id, value.Value))]))];
        _variants = [.. data.Variants.Select(variant => new VariantInput(
            variant.Id,
            variant.OptionValueIds,
            variant.Sku,
            variant.OriginalPrice,
            variant.SalePrice,
            variant.IsAvailable,
            variant.PinnedImageId))];
        _variantLabels = data.Variants.ToDictionary(variant => variant.Id, variant => variant.Label);
        _variantsSeeded = true;
    }

    private void OnNameChanged(string? value)
    {
        _dirty = true;
        _validation?.Clear();
        _model.Name = value ?? string.Empty;
        _model.Sku = string.IsNullOrWhiteSpace(value) ? string.Empty : Sku.Suggest(value).Value;
    }

    private void OnDescriptionChanged(string? value)
    {
        _dirty = true;
        _validation?.Clear();
        _model.Description = value;
    }

    private void OnCategoryChanged(Guid? value)
    {
        _dirty = true;
        _validation?.Clear();
        _model.CategoryId = value;
    }

    private void OnBrandChanged(Guid? value)
    {
        _dirty = true;
        _validation?.Clear();
        _model.BrandId = value;
    }

    private void OnOriginalPriceChanged(decimal? value)
    {
        _dirty = true;
        _validation?.Clear();
        _model.OriginalPrice = value;
    }

    private void OnSalePriceChanged(decimal? value)
    {
        _dirty = true;
        _validation?.Clear();
        _model.SalePrice = value;
    }

    private Task OnPublishedChangedAsync(bool value)
    {
        _dirty = true;
        _validation?.Clear();
        _model.IsPublished = value;

        return Task.CompletedTask;
    }

    private void OnGalleryImagesChanged(IReadOnlyList<ShopUploadedImage> images)
    {
        _dirty = true;
        _validation?.Clear();
        ApplyGallery(images);
    }

    /// <summary>
    /// Projects the gallery control's ordered selection onto the two shapes the rest of the form
    /// needs: the command's desired gallery state and the variant pin picker's image list.
    /// </summary>
    private void ApplyGallery(IReadOnlyList<ShopUploadedImage> images)
    {
        _galleryImages = images;

        // A row still showing a validation error (wrong type / too large) never leaves the client,
        // and never counts toward position or the primary slot (FR-11).
        var validImages = images.Where(image => image.Error is null).ToList();

        _gallery = [.. validImages.Select((image, position) => new ProductGalleryEntry(
            image.ExistingId,
            image.IsExisting ? null : new ProductImageUpload(image.Bytes, image.FileName, image.ContentType),
            position,
            position == 0,
            image.ClientId))];

        // Every valid image is offered to the pin picker, including one picked moments ago: it is
        // identified by its ClientId and previewed from its data: URL, and the handler resolves that
        // id to the real one once the upload lands (Decision 9 keeps uploads on save).
        _galleryPreview = [.. validImages.Select((image, position) => new ProductImageDto(
            image.ClientId,
            image.PreviewUrl,
            position,
            position == 0))];
    }

    private void OnVariantsStateChanged(ProductVariantsState state)
    {
        _optionTypes = state.OptionTypes;
        _variants = state.Variants;
        _variantLabels = state.LabelsByVariantId;

        // The card's first callback seeds the save payload from InitialData. It is not a staff
        // edit, so it must not mark the form dirty.
        if (!_variantsSeeded)
        {
            _variantsSeeded = true;
            return;
        }

        _dirty = true;
        _validation?.Clear();

        // Whatever the server flagged last time describes a save that has since been edited.
        _variantsFlaggedByServer = [];
    }

    private void OnSpecificationsStateChanged(ProductSpecificationsState state)
    {
        _specifications = state.Specifications;

        // The card's first callback only seeds the save payload from InitialData (Edit mode) or
        // starts empty (Create mode) — neither is a staff edit, so it must not mark the form dirty.
        if (!_specificationsSeeded)
        {
            _specificationsSeeded = true;
            return;
        }

        _dirty = true;
        _validation?.Clear();

        // Whatever the server flagged last time describes a save that has since been edited.
        _specificationsFlaggedByServer = [];
    }

    private IReadOnlyList<string> GalleryErrors => [.. _galleryImages.Where(image => image.Error is not null).Select(image => image.Error!)];

    private Task AddGalleryFilesAsync(IReadOnlyList<IBrowserFile> files) =>
        _imageUpload?.AddFilesAsync(files) ?? Task.CompletedTask;

    private async Task SaveAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Products.SaveProduct)) return;
        await BusyState.RunAsync(BusyKeys.Products.SaveProduct, async () =>
        {
            _validation.Clear();
            if (_imageUpload is not null) await _imageUpload.WaitForPendingFilesAsync();
            var pricesValid = true;
            if (!HasVariants)
            {
                if (_originalPriceField is not null) pricesValid &= await _originalPriceField.ValidateAsync(includeDisabled: true);
                if (_salePriceField is not null) pricesValid &= await _salePriceField.ValidateAsync(includeDisabled: true);
            }
            pricesValid &= await _variantsCard.ValidatePricesAsync(includeDisabled: true);
            if (!pricesValid) return;

            // Products with variants persist only the row prices. Keep the standalone drafts
            // in the form so removing the last option restores the staff member's edits.
            var originalPrice = HasVariants ? null : _model.OriginalPrice;
            var salePrice = HasVariants ? null : _model.SalePrice;
            var result = Mode == ProductFormMode.Create
                ? await Mediator.Send(new CreateProductCommand(
                    _model.Name.Trim(),
                    NormalizedDescription(),
                    _model.Sku.Trim(),
                    _model.CategoryId.GetValueOrDefault(),
                    _model.BrandId.GetValueOrDefault(),
                    originalPrice,
                    salePrice,
                    _model.IsPublished,
                    _gallery,
                    _optionTypes,
                    _specifications,
                    _variants))
                : await Mediator.Send(new UpdateProductCommand(
                    InitialData!.Id,
                    _rowVersion!,
                    _model.Name.Trim(),
                    NormalizedDescription(),
                    _model.Sku.Trim(),
                    _model.CategoryId.GetValueOrDefault(),
                    _model.BrandId.GetValueOrDefault(),
                    originalPrice,
                    salePrice,
                    _model.IsPublished,
                    _gallery,
                    _optionTypes,
                    _specifications,
                    _variants));

            if (result.IsSuccess)
            {
                _dirty = false;
                var message = Mode == ProductFormMode.Create
                    ? _model.IsPublished
                        ? string.Format(Strings.Product_CreatedPublished, result.Value.Name)
                        : string.Format(Strings.Product_Created, result.Value.Name)
                    : string.Format(Strings.Product_Updated, result.Value.Name);
                Notifications.Show(message, ShopNotificationKind.Success);
                Nav.NavigateTo(Routes.Admin.ManageProducts);
            }
            else
            {
                _validation.ShowErrors(result.ValidationErrors);
                FlagVariantsReportedByServer(result);
                FlagSpecificationsReportedByServer(result);
                if (result.ValidationErrors.Count == 0
                    && !_variantsFlaggedByServer.Any(id => _variants.Any(variant => variant.Id == id))
                    && !_specificationsFlaggedByServer.Any(position => position >= 0 && position < _specifications.Count))
                    Notifications.Show(DescribeFailure(result), ShopNotificationKind.Error);
            }
        });
    }

    /// <summary>
    /// Marks the variant rows a publish refusal named, so the omission shows up against the row the
    /// staff member has to fix and not only in the message (AC-20). The identifiers match the rows'
    /// own, because the aggregate keeps the identifier each variant row was submitted with.
    /// </summary>
    private void FlagVariantsReportedByServer(Result result) =>
        _variantsFlaggedByServer =
        [
            .. result.ErrorArgs
                .Where(token => token.StartsWith(ProductNotPublishableException.VariantPriceTokenPrefix, StringComparison.Ordinal))
                .Select(token => token[ProductNotPublishableException.VariantPriceTokenPrefix.Length..])
                .Select(id => Guid.TryParse(id, out var variantId) ? variantId : (Guid?)null)
                .Where(id => id is not null)
                .Select(id => id!.Value),
        ];

    /// <summary>
    /// Marks the specification rows a validation failure named — a blank name/value or a
    /// duplicate name (RULE-3, RULE-4) — so the omission shows up against the row the staff
    /// member has to fix (AC-7, AC-8). <see cref="Result.ErrorArgs"/> carries each offending
    /// row's position as a plain integer string for these three keys.
    /// </summary>
    private void FlagSpecificationsReportedByServer(Result result)
    {
        var isSpecificationError = result.Error is ProductErrorKeys.SpecificationNameRequired
            or ProductErrorKeys.SpecificationValueRequired
            or ProductErrorKeys.SpecificationNameDuplicated;

        _specificationsFlaggedByServer = isSpecificationError
            ? [.. result.ErrorArgs
                .Select(token => int.TryParse(token, out var position) ? position : (int?)null)
                .Where(position => position is not null)
                .Select(position => position!.Value)]
            : [];
    }

    /// <summary>
    /// Turns a failed save into the message shown to the staff member. A publish refusal is the one
    /// failure that carries detail beyond its key — it lists every value that is missing, which the
    /// message's placeholder expects.
    /// </summary>
    private string DescribeFailure(Result result)
    {
        if (result.Error != ProductNotPublishableException.MessageResourceKey || result.ErrorArgs.Count == 0)
            return Localizer[result.Error ?? nameof(Strings.Product_CreateFailed)];

        var missing = result.ErrorArgs.Select(DescribeMissingValue);
        return string.Format(Strings.Product_NotPublishable, string.Join(", ", missing));
    }

    private string DescribeMissingValue(string token)
    {
        if (token == ProductNotPublishableException.PriceToken)
            return Strings.Product_NotPublishable_Price;

        var id = token[ProductNotPublishableException.VariantPriceTokenPrefix.Length..];
        var label = Guid.TryParse(id, out var variantId) && _variantLabels.TryGetValue(variantId, out var variantLabel)
            ? variantLabel
            : id;

        return string.Format(Strings.Product_NotPublishable_VariantPrice, label);
    }

    private string? NormalizedDescription() =>
        string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim();

    private async Task OnBeforeInternalNavigationAsync(LocationChangingContext context)
    {
        if (!_dirty)
            return;

        if (!await ConfirmLeaveAsync())
            context.PreventNavigation();
    }

    private Task<bool> ConfirmLeaveAsync() =>
        DialogService.ConfirmAsync(new(Strings.ProductForm_UnsavedChangesTitle,
            Strings.ProductForm_UnsavedChangesBody, Strings.ProductForm_LeaveAnyway, true));
}
