using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Displays one image inside a frame whose geometry is fixed by <see cref="Preset"/> before the image
/// arrives. The frame width follows the caller's allocation and its height follows the preset ratio;
/// <see cref="ShopImagePreset.BrandLogo"/> instead fills the width and height the caller reserves.
/// A missing, blank, or failed source is replaced by <see cref="PlaceholderLabel"/> inside the same
/// frame. <see cref="ShopImagePreset.Hero"/> and <see cref="ShopImagePreset.Banner"/> switch to a
/// 4:5 frame below 600 CSS pixels, showing <see cref="MobileSrc"/> when supplied and otherwise the
/// desktop source cropped from the center. The component adds no keyboard stops; surrounding actions
/// stay with the caller. Inherits from <see cref="ShopComponentBase"/> so <c>Class</c>, <c>Style</c>,
/// and arbitrary attributes reach the root frame.
/// </summary>
public partial class ShopImage : ShopComponentBase, IAsyncDisposable
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
    /// <see cref="ShopImagePreset.Banner"/>. Ignored by other presets. When absent, mobile
    /// presentation reuses <see cref="Src"/> with a centered 4:5 crop.
    /// </summary>
    [Parameter] public string? MobileSrc { get; set; }

    /// <summary>
    /// The treatment that decides frame ratio and fit. Defaults to <see cref="ShopImagePreset.SquareContain"/>.
    /// </summary>
    [Parameter] public ShopImagePreset Preset { get; set; } = ShopImagePreset.SquareContain;

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

    // Stable per instance so the JS bridge can find this frame across source changes.
    private readonly string _frameId = $"shop-image-{Guid.NewGuid():N}";

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<ShopImage>? _dotNetRef;

    // Each branch tracks the source that failed rather than a flag, so a replacement source is
    // shown again without an explicit reset and a late error for an earlier source is ignored.
    private string? _failedDesktopSrc;
    private string? _failedMobileSrc;

    private bool HasMobileBranch => Preset is ShopImagePreset.Hero or ShopImagePreset.Banner;

    private string? DesktopSrc => string.IsNullOrWhiteSpace(Src) ? null : Src;

    private string? MobileBranchSrc => string.IsNullOrWhiteSpace(MobileSrc) ? DesktopSrc : MobileSrc;

    private string FitClass => Preset switch
    {
        ShopImagePreset.SquareContain or
        ShopImagePreset.BrandLogo or
        ShopImagePreset.SocialSharing => "shop-image-contain",
        _ => "shop-image-cover",
    };

    private IReadOnlyList<ImageBranch> Branches =>
        HasMobileBranch
            ? [CreateBranch(DesktopBranch, "shop-image-branch-desktop", DesktopSrc, _failedDesktopSrc),
               CreateBranch(MobileBranch, "shop-image-branch-mobile", MobileBranchSrc, _failedMobileSrc)]
            : [CreateBranch(DesktopBranch, "shop-image-branch-desktop", DesktopSrc, _failedDesktopSrc)];

    #endregion

    #region CSS Forwarding

    private string Classname => ShopCssClass.Join("shop-image", PresetClass, Class);

    private string PresetClass => Preset switch
    {
        ShopImagePreset.SquareContain => "shop-image-square-contain",
        ShopImagePreset.SquareCover => "shop-image-square-cover",
        ShopImagePreset.Banner => "shop-image-banner",
        ShopImagePreset.Hero => "shop-image-hero",
        ShopImagePreset.PortraitCover => "shop-image-portrait-cover",
        ShopImagePreset.Editorial => "shop-image-editorial",
        ShopImagePreset.BrandLogo => "shop-image-brand-logo",
        ShopImagePreset.SocialSharing => "shop-image-social-sharing",
        _ => throw new ArgumentOutOfRangeException(nameof(Preset), Preset, null),
    };

    private Dictionary<string, object> RootAttributes
    {
        get
        {
            var attributes = AdditionalAttributes is null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(AdditionalAttributes, StringComparer.OrdinalIgnoreCase);
            attributes["data-shop-image"] = _frameId;
            attributes.TryAdd("role", "none");
            return attributes;
        }
    }

    private Dictionary<string, object> PlaceholderAttributes =>
        string.IsNullOrWhiteSpace(Alt)
            ? new() { ["aria-hidden"] = "true" }
            : new() { ["role"] = "img", ["aria-label"] = Alt };

    private ImageBranch CreateBranch(string key, string branchClass, string? src, string? failedSrc) =>
        new(
            key,
            src,
            src is not null && src != failedSrc,
            ShopCssClass.Join("shop-image-media", FitClass, branchClass),
            ShopCssClass.Join("shop-image-placeholder", branchClass));

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
