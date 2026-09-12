using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Utilities;
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
/// The create/edit product form (Figma nodes <c>2687:14713</c> empty / <c>2687:15441</c>
/// populated): General Info, the inline <see cref="ProductVariantsCard"/>, and Create/Cancel
/// actions, shared by <c>AddProduct</c> and <c>EditProduct</c> (constitution Rule 25 — the
/// second real call site). Owns its own MediatR dispatch, branching on <see cref="Mode"/>,
/// since both pages need the identical field set, validation, and save mechanics.
/// </summary>
public partial class ProductForm : MudComponentBase
{
    private const int MaxNameLength = 150;

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    /// <summary>Whether this form creates a new product or edits an existing one.</summary>
    [Parameter, EditorRequired]
    public ProductFormMode Mode { get; set; }

    /// <summary>The product's current saved state. Required, and ignored, for <see cref="ProductFormMode.Create"/>.</summary>
    [Parameter]
    public AdminProductDto? InitialData { get; set; }

    /// <summary>The Active categories offered by the category select (AC-23).</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<CategoryLookupDto> Categories { get; set; } = [];

    /// <summary>The Active brands offered by the brand select (AC-23).</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<BrandLookupDto> Brands { get; set; } = [];

    private MudForm _form = default!;
    private bool _isFormValid;
    private bool _dirty;
    private bool _initialized;

    private string _name = string.Empty;
    private string? _description;
    private string _sku = string.Empty;
    private Guid? _categoryId;
    private Guid? _brandId;
    private decimal? _originalPrice;
    private decimal? _salePrice;
    private bool _isPublished;
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

    /// <summary>
    /// Whether publishing is currently impossible because some variant has no price (RULE-14). A
    /// draft is free to be incomplete (RULE-15), so this only bites while the status is Active.
    /// </summary>
    private bool PublishBlockedByVariantPrices =>
        _isPublished && HasVariants && UnpricedVariantIds.Count > 0;

    private IReadOnlyList<Guid> FlaggedVariantIds =>
        [.. (_isPublished ? UnpricedVariantIds : []).Concat(_variantsFlaggedByServer).Distinct()];

    private IReadOnlyList<CategoryLookupDto> DisplayCategories =>
        _categoryId is { } id && InitialData is not null && Categories.All(c => c.Id != id)
            ? [.. Categories, new CategoryLookupDto(id, InitialData.CategoryName)]
            : Categories;

    private IReadOnlyList<BrandLookupDto> DisplayBrands =>
        _brandId is { } id && InitialData is not null && Brands.All(b => b.Id != id)
            ? [.. Brands, new BrandLookupDto(id, InitialData.BrandName)]
            : Brands;

    private readonly Func<string, string?> _nameValidation = name =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length > MaxNameLength
            ? Strings.Product_NameTooLong
            : null;

    protected string Classname => new CssBuilder("product-form").AddClass(Class).Build();

    protected string Stylename => new StyleBuilder().AddStyle(Style).Build();

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (_initialized || Mode != ProductFormMode.Edit || InitialData is not { } data)
            return;

        _initialized = true;
        _name = data.Name;
        _description = data.Description;
        _sku = data.Sku;
        _categoryId = data.CategoryId;
        _brandId = data.BrandId;
        _originalPrice = data.OriginalPrice;
        _salePrice = data.SalePrice;
        _isPublished = data.IsPublished;
        _rowVersion = data.RowVersion;

        // The command carries the gallery's whole desired state, so the saved images have to be
        // in it from the start — otherwise saving an untouched form would clear them.
        ApplyGallery([.. data.Images.OrderBy(image => image.Position)
            .Select(image => ShopUploadedImage.Existing(image.Id, image.Url))]);
    }

    private void OnNameChanged(string value)
    {
        _dirty = true;
        _name = value;
        _sku = string.IsNullOrWhiteSpace(value) ? string.Empty : Sku.Suggest(value).Value;
    }

    private void OnDescriptionChanged(string? value)
    {
        _dirty = true;
        _description = value;
    }

    private void OnCategoryChanged(Guid? value)
    {
        _dirty = true;
        _categoryId = value;
    }

    private void OnBrandChanged(Guid? value)
    {
        _dirty = true;
        _brandId = value;
    }

    private void OnOriginalPriceChanged(decimal? value)
    {
        _dirty = true;
        _originalPrice = value;
    }

    private void OnSalePriceChanged(decimal? value)
    {
        _dirty = true;
        _salePrice = value;
    }

    private async Task OnPublishedChangedAsync(bool value)
    {
        _dirty = true;
        _isPublished = value;

        // Switching to Active is what makes every variant price required, so re-validate straight
        // away rather than waiting for the staff member to press Save.
        await _form.ValidateAsync();
    }

    private void OnGalleryImagesChanged(IReadOnlyList<ShopUploadedImage> images)
    {
        _dirty = true;
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

        // Whatever the server flagged last time describes a save that has since been edited.
        _specificationsFlaggedByServer = [];
    }

    private async Task SaveAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid || PublishBlockedByVariantPrices)
            return;

        // A product with variants has no price of its own — customers pay the variant's price
        // (RULE-19). The entered values stay in the form so they come back if the last option type
        // is removed, but they must not be persisted while variants exist, or a price nobody can see
        // would linger in the catalogue.
        var originalPrice = HasVariants ? null : _originalPrice;
        var salePrice = HasVariants ? null : _salePrice;

        await BusyState.RunAsync(BusyKeys.Products.SaveProduct, async () =>
        {
            var result = Mode == ProductFormMode.Create
                ? await Mediator.Send(new CreateProductCommand(
                    _name.Trim(),
                    NormalizedDescription(),
                    _sku.Trim(),
                    _categoryId!.Value,
                    _brandId!.Value,
                    originalPrice,
                    salePrice,
                    _isPublished,
                    _gallery,
                    _optionTypes,
                    _specifications,
                    _variants))
                : await Mediator.Send(new UpdateProductCommand(
                    InitialData!.Id,
                    _rowVersion!,
                    _name.Trim(),
                    NormalizedDescription(),
                    _sku.Trim(),
                    _categoryId!.Value,
                    _brandId!.Value,
                    originalPrice,
                    salePrice,
                    _isPublished,
                    _gallery,
                    _optionTypes,
                    _specifications,
                    _variants));

            if (result.IsSuccess)
            {
                _dirty = false;
                var message = Mode == ProductFormMode.Create
                    ? _isPublished
                        ? string.Format(Strings.Product_CreatedPublished, result.Value.Name)
                        : string.Format(Strings.Product_Created, result.Value.Name)
                    : string.Format(Strings.Product_Updated, result.Value.Name);
                Snackbar.Add(message, Severity.Success);
                Nav.NavigateTo(Routes.Admin.ManageProducts);
            }
            else
            {
                FlagVariantsReportedByServer(result);
                FlagSpecificationsReportedByServer(result);
                Snackbar.Add(DescribeFailure(result), Severity.Error);
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
        string.IsNullOrWhiteSpace(_description) ? null : _description.Trim();

    private async Task OnBeforeInternalNavigationAsync(LocationChangingContext context)
    {
        if (!_dirty)
            return;

        if (!await ConfirmLeaveAsync())
            context.PreventNavigation();
    }

    private async Task<bool> ConfirmLeaveAsync()
    {
        var parameters = new DialogParameters<ShopConfirmDialog>
        {
            { x => x.TitleText, Strings.ProductForm_UnsavedChangesTitle },
            { x => x.BodyText, Strings.ProductForm_UnsavedChangesBody },
            { x => x.ConfirmLabel, Strings.ProductForm_LeaveAnyway },
            { x => x.ConfirmColor, Color.Error },
        };

        var dialog = await DialogService.ShowAsync<ShopConfirmDialog>(Strings.ProductForm_UnsavedChangesTitle, parameters);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }
}
