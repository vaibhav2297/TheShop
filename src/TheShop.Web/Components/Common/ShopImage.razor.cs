using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Displays one image inside a frame whose geometry is fixed by <see cref="Preset"/> before the image
/// arrives. The frame width follows the caller's allocation and its height follows the preset ratio;
/// <see cref="ShopImagePreset.BrandLogo"/> instead fills the width and height the caller reserves.
/// A missing, blank, or failed source is replaced by <see cref="PlaceholderLabel"/> inside the same
/// frame. <see cref="ShopImagePreset.Hero"/> and <see cref="ShopImagePreset.CategoryBanner"/> switch to a
/// 4:5 frame below 600 CSS pixels, showing <see cref="MobileSrc"/> when supplied and otherwise the
/// desktop source cropped from the center. The component adds no keyboard stops; surrounding actions
/// stay with the caller. Inherits from <see cref="MudComponentBase"/> so <c>Class</c>, <c>Style</c>,
/// and arbitrary attributes reach the root frame.
/// </summary>
public partial class ShopImage : MudComponentBase, IAsyncDisposable
{
    private const string DesktopBranch = "desktop";
    private const string MobileBranch = "mobile";

    #region Parameters

    /// <summary>
    /// The image URL. A <c>null</c> or blank value shows <see cref="PlaceholderLabel"/> immediately.
    /// </summary>
    [Parameter] public string? Src { get; set; }

    /// <summary>
    /// Dedicated mobile artwork for <see cref="ShopImagePreset.Hero"/> and
    /// <see cref="ShopImagePreset.CategoryBanner"/>. Ignored by other presets. When absent, mobile
    /// presentation reuses <see cref="Src"/> with a centered 4:5 crop.
    /// </summary>
    [Parameter] public string? MobileSrc { get; set; }

    /// <summary>
    /// The treatment that decides frame ratio and fit. Defaults to <see cref="ShopImagePreset.ProductCard"/>.
    /// </summary>
    [Parameter] public ShopImagePreset Preset { get; set; } = ShopImagePreset.ProductCard;

    /// <summary>
    /// Localized description of a meaningful image. Leave empty for a decorative image; assistive
    /// technology then ignores both the image and its placeholder.
    /// </summary>
    [Parameter] public string Alt { get; set; } = string.Empty;

    /// <summary>
    /// Name or localized label shown when the image is missing or cannot be displayed.
    /// </summary>
    [Parameter, EditorRequired] public string PlaceholderLabel { get; set; } = string.Empty;

    #endregion

    #region State

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Stable per instance so the JS bridge can find this frame without an element reference
    // (the root is a MudStack, which exposes none).
    private readonly string _frameId = $"shop-image-{Guid.NewGuid():N}";

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<ShopImage>? _dotNetRef;

    // Each branch tracks the source that failed rather than a flag, so a replacement source is
    // shown again without an explicit reset and a late error for an earlier source is ignored.
    private string? _failedDesktopSrc;
    private string? _failedMobileSrc;

    private bool HasMobileBranch => Preset is ShopImagePreset.Hero or ShopImagePreset.CategoryBanner;

    private string? DesktopSrc => string.IsNullOrWhiteSpace(Src) ? null : Src;

    private string? MobileBranchSrc => string.IsNullOrWhiteSpace(MobileSrc) ? DesktopSrc : MobileSrc;

    private ObjectFit Fit => Preset switch
    {
        ShopImagePreset.ProductCard or
        ShopImagePreset.ProductDetail or
        ShopImagePreset.Thumbnail or
        ShopImagePreset.BrandLogo or
        ShopImagePreset.SocialSharing => ObjectFit.Contain,
        _ => ObjectFit.Cover,
    };

    private IReadOnlyList<ImageBranch> Branches =>
        HasMobileBranch
            ? [CreateBranch(DesktopBranch, "shop-image__branch--desktop", DesktopSrc, _failedDesktopSrc),
               CreateBranch(MobileBranch, "shop-image__branch--mobile", MobileBranchSrc, _failedMobileSrc)]
            : [CreateBranch(DesktopBranch, "shop-image__branch--desktop", DesktopSrc, _failedDesktopSrc)];

    #endregion

    #region CSS Forwarding

    protected string Classname => new CssBuilder("shop-image")
        .AddClass(PresetClass)
        .AddClass(Class)
        .Build();

    protected string Stylename => new StyleBuilder()
        .AddStyle(Style)
        .Build();

    private string PresetClass => Preset switch
    {
        ShopImagePreset.ProductCard => "shop-image--product-card",
        ShopImagePreset.ProductDetail => "shop-image--product-detail",
        ShopImagePreset.Thumbnail => "shop-image--thumbnail",
        ShopImagePreset.CategoryTile => "shop-image--category-tile",
        ShopImagePreset.CategoryBanner => "shop-image--category-banner",
        ShopImagePreset.Hero => "shop-image--hero",
        ShopImagePreset.MobileBanner => "shop-image--mobile-banner",
        ShopImagePreset.Editorial => "shop-image--editorial",
        ShopImagePreset.BrandLogo => "shop-image--brand-logo",
        ShopImagePreset.SocialSharing => "shop-image--social-sharing",
        _ => throw new ArgumentOutOfRangeException(nameof(Preset), Preset, null),
    };

    private Dictionary<string, object> RootAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(UserAttributes) { ["data-shop-image"] = _frameId };

            // MudStack defaults to role="group", which stops a surrounding link from taking its
            // accessible name from the image's alt text. The frame is layout only.
            attributes.TryAdd("role", "none");
            return attributes;
        }
    }

    private Dictionary<string, object> PlaceholderAttributes =>
        string.IsNullOrWhiteSpace(Alt)
            ? new() { ["aria-hidden"] = "true" }
            : new() { ["role"] = "img", ["aria-label"] = Alt };

    private static ImageBranch CreateBranch(string key, string branchClass, string? src, string? failedSrc) =>
        new(
            key,
            src,
            src is not null && src != failedSrc,
            new CssBuilder("shop-image__media")
                .AddClass(branchClass)
                .Build(),
            new CssBuilder("shop-image__placeholder")
                .AddClass("mud-tertiary")
                .AddClass("pa-2")
                .AddClass(branchClass)
                .Build());

    #endregion

    #region Lifecycle

    protected override void OnParametersSet()
    {
        // A changed source gets a fresh attempt, even one that failed earlier and has come back.
        if (_failedDesktopSrc != DesktopSrc) _failedDesktopSrc = null;
        if (_failedMobileSrc != MobileBranchSrc) _failedMobileSrc = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _dotNetRef = DotNetObjectReference.Create(this);
        _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/shopImage.js");

        // Loose bUnit JS interop hands back no module; the frame still renders its images.
        if (_jsModule is not null)
            await _jsModule.InvokeVoidAsync("observe", _frameId, _dotNetRef);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called by the JS bridge when an image in <paramref name="branch"/> fails to load
    /// <paramref name="src"/>. Reports for a source that is no longer current are ignored.
    /// </summary>
    /// <param name="branch">Either <c>desktop</c> or <c>mobile</c>.</param>
    /// <param name="src">The source the failed image element was loading.</param>
    [JSInvokable]
    public Task OnImageFailed(string branch, string src)
    {
        switch (branch)
        {
            case DesktopBranch when src == DesktopSrc && _failedDesktopSrc != src:
                _failedDesktopSrc = src;
                break;
            case MobileBranch when HasMobileBranch && src == MobileBranchSrc && _failedMobileSrc != src:
                _failedMobileSrc = src;
                break;
            default:
                return Task.CompletedTask;
        }

        StateHasChanged();
        return Task.CompletedTask;
    }

    #endregion

    #region Disposal

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("unobserve", _frameId);
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The runtime is already gone; the listener went with the page.
            }
        }

        _dotNetRef?.Dispose();
    }

    #endregion

    private sealed record ImageBranch(
        string Key,
        string? Src,
        bool ShowsImage,
        string MediaClassname,
        string PlaceholderClassname);
}
