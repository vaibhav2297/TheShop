using MediatR;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductDetails;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Products;

/// <summary>
/// The public product-details page. Loads one published product through
/// <see cref="GetProductDetailsQuery"/> and lets the customer choose a variant, browse the shared
/// gallery, and expand the description and specifications. Variant, pricing, and image selection
/// all come from <see cref="ProductDetailsSelection"/>; the page only holds the current result.
/// Add to Bag and Favourite are enabled but deliberately do nothing in this feature.
/// </summary>
[Route(Routes.ProductDetailsPattern)]
public partial class ProductDetails : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    /// <summary>The product id from the route.</summary>
    [Parameter] public Guid Id { get; set; }

    private CancellationTokenSource? _loadCts;
    private Guid? _requestedId;

    private ProductDetailsDto? _product;
    private ProductDetailsSelectionDto? _selection;
    private bool _notFound;
    private bool _loadFailed;
    private bool _descriptionExpanded;
    private bool _specificationsExpanded;

    private bool HasDescription => !string.IsNullOrWhiteSpace(_product?.DescriptionText);

    private bool HasSpecifications => _product?.Specifications.Count > 0;

    private string? PriceText => _selection?.EffectivePrice is { } price
        ? CurrencyFormatter.Format(price, _selection.Currency)
        : null;

    private string? OriginalPriceText => _selection is { IsDiscounted: true, OriginalPrice: { } original }
        ? CurrencyFormatter.Format(original, _selection.Currency)
        : null;

    private string SectionsTestId => _descriptionExpanded && _specificationsExpanded
        ? "product-details-expanded"
        : "product-details-sections";

    /// <inheritdoc/>
    protected override Task OnParametersSetAsync() =>
        _requestedId == Id ? Task.CompletedTask : LoadAsync();

    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = _loadCts = new CancellationTokenSource();
        var id = Id;

        _requestedId = id;
        _product = null;
        _selection = null;
        _notFound = false;
        _loadFailed = false;
        _descriptionExpanded = false;
        _specificationsExpanded = false;
        Breadcrumbs.Set(BreadcrumbTrail.Storefront().Add(Strings.Nav_Products, Routes.Products).Build());

        try
        {
            var result = await BusyState.RunAsync(
                BusyKeys.Products.Details, () => Mediator.Send(new GetProductDetailsQuery(id), cts.Token));

            if (cts.IsCancellationRequested)
                return;

            if (result.IsSuccess)
            {
                _product = result.Value;
                _selection = ProductDetailsSelection.Resolve(_product);
                Breadcrumbs.Set(BreadcrumbTrail.Storefront().Add(Strings.Nav_Products, Routes.Products).Current(_product.Name));
            }
            else if (result.Error == ProductErrorKeys.NotFound)
            {
                _notFound = true;
            }
            else
            {
                _loadFailed = true;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // A newer route or disposal superseded this load; its result is discarded.
        }
    }

    private Task OnRetryAsync() => LoadAsync();

    private void OnOptionValueSelected(Guid optionValueId)
    {
        if (_product is not null && _selection is not null)
            _selection = ProductDetailsSelection.SelectOptionValue(_product, _selection, optionValueId);
    }

    private void OnImageSelected(Guid? imageId)
    {
        if (_product is not null && _selection is not null)
            _selection = ProductDetailsSelection.SelectImage(_product, _selection, imageId);
    }

    private bool IsOptionValueSelected(Guid optionValueId) =>
        _product is not null && _selection is not null &&
        ProductDetailsSelection.IsOptionValueSelected(_product, _selection, optionValueId);

    private bool IsOptionValueSelectable(Guid optionValueId) =>
        _product is not null && ProductDetailsSelection.IsOptionValueSelectable(_product, optionValueId);

    private static string OptionClassname(bool isSelected) =>
        new CssBuilder("shop-product-details__option")
            .AddClass("shop-product-details__option--selected", isSelected)
            .Build();

    /// <inheritdoc/>
    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
