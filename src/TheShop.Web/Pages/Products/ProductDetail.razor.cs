using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductDetail;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;
using TheShop.Web.State;
using TheShop.Web.Theme;

namespace TheShop.Web.Pages.Products;

/// <summary>Public product detail with explicit option selection and a responsive shared image gallery.</summary>
[Route(Routes.ProductDetailPattern)]
[Route(Routes.ProductBySlugPattern)]
[Route(Routes.ProductVariantPattern)]
public partial class ProductDetail : IAsyncDisposable
{
    /// <summary>Product to load; missing and unpublished IDs share the unavailable state.</summary>
    [Parameter] public Guid Id { get; set; }
    [Parameter] public string? ProductSlug { get; set; }
    [Parameter] public string? VariantSlug { get; set; }
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private ProductDetailDto? _product;
    private ProductDetailVariantDto? _variant;
    private readonly Dictionary<Guid, Guid> _choices = [];
    private CancellationTokenSource? _load;
    private int _generation, _galleryGeneration, _activeIndex;
    private bool _desktop, _expanded, _attention, _invalidCombination, _descriptionExpanded, _specificationsExpanded;
    private bool _disposed, _pageReady, _carouselReady, _focusExpanded, _clearAttention;
    private ElementReference _root;
    private IJSObjectReference? _module, _controller;
    private DotNetObjectReference<ProductDetail>? _reference;
    private string PageClass => ShopCssClass.Join("shop-product-detail", _expanded ? "shop-product-detail-expanded" : null);
    private string OptionsClass => ShopCssClass.Join("shop-product-detail-options", _attention ? "shop-product-detail-attention" : null);
    private IReadOnlyList<ProductImageDto> VisibleImages => _product is null ? [] :
        !_product.HasVariants ? _product.Images :
        _variant is null ? [] : _variant.ImageIds
            .Select(id => _product.Images.FirstOrDefault(image => image.Id == id))
            .OfType<ProductImageDto>().ToArray();
    private string? ReadyTestId => _pageReady && (_carouselReady || VisibleImages.Count == 0) ? "product-detail-ready" : null;
    private static string OptionTestId(Guid id) => $"product-detail-option-{id}";
    private static string FormatPosition(int index, int count) => string.Format(Strings.ProductDetail_ImagePosition, index, count);
    private bool IsSelected(Guid group, Guid value) => _choices.GetValueOrDefault(group) == value;
    private string GalleryToggleLabel => _expanded ? Strings.ProductDetail_ShowLess : Strings.ProductDetail_ShowMore;
    private string GalleryToggleIcon => _expanded ? ShopIcons.Outlined.Chevron_Up : ShopIcons.Outlined.Chevron_Down;
    private string GalleryToggleTestId => _expanded ? "product-detail-show-less" : "product-detail-show-more";

    private string PriceLabel
    {
        get
        {
            if (_product is null) return Strings.NotAvailable;
            if (_product.HasVariants && _variant is null)
            {
                if (_product.MinVariantPrice is not { } min || _product.MaxVariantPrice is not { } max)
                    return Strings.NotAvailable;
                var low = CurrencyFormatter.Format(min, _product.Currency);
                return min == max ? low : string.Format(Strings.ProductDetail_PriceRange, low, CurrencyFormatter.Format(max, _product.Currency));
            }
            var price = _product.HasVariants ? _variant?.CurrentPrice : _product.CurrentPrice;
            return price is { } amount ? CurrencyFormatter.Format(amount, _product.Currency) : Strings.NotAvailable;
        }
    }

    private decimal? DiscountOriginal
    {
        get
        {
            var original = _product?.HasVariants == true ? _variant?.OriginalPrice : _product?.OriginalPrice;
            var sale = _product?.HasVariants == true ? _variant?.SalePrice : _product?.SalePrice;
            return original is { } amount && sale is { } discounted && discounted < amount ? amount : null;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        // Route parameters are reused by Blazor. Read the active path so stale values from a
        // previous route shape cannot silently turn a slug request into a GUID request.
        var segments = Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', 2)[0]
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = segments.Length == 2 && Guid.TryParse(segments[1], out var parsed) ? parsed : (Guid?)null;
        var productSlug = id is null && segments.Length >= 2 ? Uri.UnescapeDataString(segments[1]) : null;
        var variantSlug = id is null && segments.Length == 3 ? Uri.UnescapeDataString(segments[2]) : null;
        if (_product is { } current && productSlug == current.ProductSlug)
        {
            var selected = variantSlug is null
                ? ProductDetailVariantResolver.ResolveInitial(current, null)
                : current.Variants.FirstOrDefault(v => v.Slug == variantSlug);
            if ((current.HasVariants || variantSlug is not null) && selected is null)
            {
                _product = null;
                return;
            }
            if (selected?.Id != _variant?.Id) CommitSelection(selected);
            Canonicalize(current, selected, id is not null || variantSlug is null);
            return;
        }
        _load?.Cancel();
        _load?.Dispose();
        _load = new CancellationTokenSource();
        var ct = _load.Token;
        var generation = ++_generation;
        await DisposeControllerAsync();
        _product = null; _variant = null; _choices.Clear(); _activeIndex = 0;
        _expanded = _attention = _invalidCombination = _descriptionExpanded = _specificationsExpanded = false;
        _pageReady = _carouselReady = _focusExpanded = false;
        Breadcrumbs.Set(BreadcrumbTrail.Storefront().Current(Strings.Nav_Products));
        try
        {
            var result = await BusyState.RunAsync(BusyKeys.Products.Detail,
                () => Mediator.Send(new GetProductDetailQuery(id, productSlug, variantSlug), ct));
            if (_disposed || generation != _generation || ct.IsCancellationRequested) return;
            if (result.IsSuccess)
            {
                _product = result.Value;
                Breadcrumbs.Set(BreadcrumbTrail.Storefront().Add(Strings.Nav_Products, Routes.Products).Current(_product!.Name));
                var selected = _product.Variants.FirstOrDefault(v => v.Id == _product.SelectedVariantId);
                CommitSelection(selected);
                Canonicalize(_product, selected, id is not null || variantSlug is null);
            }
            else if (result.Error != ProductErrorKeys.DetailUnavailable)
                Notifications.Show(Localizer[result.Error!], ShopNotificationKind.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _product is null || BusyState.IsBusy(BusyKeys.Products.Detail)) return;
        if (_module is null) _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/productDetail.js");
        if (_disposed) return;
        if (_controller is null)
        {
            _reference ??= DotNetObjectReference.Create(this);
            _controller = await _module.InvokeAsync<IJSObjectReference>("mount", _root, _reference, _generation);
            _desktop = await _controller.InvokeAsync<bool>("isDesktop");
            _pageReady = true;
            StateHasChanged();
        }
        if (_clearAttention)
        {
            _clearAttention = false;
            await _controller.InvokeVoidAsync("clearAttention");
        }
        if (_focusExpanded)
        {
            _focusExpanded = false;
            await _controller.InvokeVoidAsync("focusExpanded", string.Format(Strings.ProductDetail_AllImagesShown, VisibleImages.Count));
        }
    }

    private void OnCarouselReady(int generation)
    {
        if (generation == _galleryGeneration) _carouselReady = true;
    }

    private void OnImageChanged(int index, int generation)
    {
        if (generation == _galleryGeneration) _activeIndex = index;
    }
    private void SelectValue(Guid group, Guid value)
    {
        if (_product is null) return;
        var choices = new Dictionary<Guid, Guid>(_choices) { [group] = value };
        var selected = ProductDetailVariantResolver.Resolve(_product, choices.Values.ToArray());
        if (selected is null) { _invalidCombination = true; return; }
        CommitSelection(selected);
        Navigation.NavigateTo(Routes.ProductVariant(_product.ProductSlug, selected.Slug));
    }

    private void CommitSelection(ProductDetailVariantDto? selected)
    {
        _variant = selected;
        _choices.Clear();
        if (_product is not null && selected is not null)
            foreach (var group in _product.OptionTypes)
            {
                var value = group.Values.FirstOrDefault(v => selected.OptionValueIds.Contains(v.Id));
                if (value is not null) _choices[group.Id] = value.Id;
            }
        _activeIndex = 0;
        _expanded = _attention = _invalidCombination = _carouselReady = false;
        _galleryGeneration++;
        _clearAttention = _controller is not null;
    }

    private void Canonicalize(ProductDetailDto product, ProductDetailVariantDto? selected, bool replace)
    {
        var canonical = selected is null ? Routes.ProductBySlug(product.ProductSlug) :
            Routes.ProductVariant(product.ProductSlug, selected.Slug);
        if (Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', 2)[0].TrimEnd('/') != canonical.TrimStart('/'))
            Navigation.NavigateTo(canonical, replace: replace);
    }

    private void ToggleGallery()
    {
        _expanded = !_expanded;
        _focusExpanded = _expanded;
    }

    private async Task AddToBagAsync()
    {
        if (_product?.HasVariants != true || _variant is not null || _controller is null) return;
        _attention = true;
        var group = _product.OptionTypes.FirstOrDefault(g => !_choices.ContainsKey(g.Id));
        await _controller.InvokeVoidAsync("attention", group?.Id.ToString(), Strings.ProductDetail_SelectVariant);
    }

    /// <summary>Applies browser breakpoint changes without altering variant choices or expansion.</summary>
    [JSInvokable]
    public Task OnDetailBreakpointChanged(bool desktop, int generation)
    {
        if (_disposed || generation != _generation) return Task.CompletedTask;
        _desktop = desktop;
        if (!desktop) _activeIndex = 0;
        return InvokeAsync(StateHasChanged);
    }

    private async ValueTask DisposeControllerAsync()
    {
        if (_controller is null) return;
        try { await _controller.InvokeVoidAsync("dispose"); await _controller.DisposeAsync(); }
        catch (JSDisconnectedException) { }
        _controller = null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _disposed = true; _load?.Cancel(); _load?.Dispose();
        await DisposeControllerAsync();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        }
        _reference?.Dispose();
    }
}
