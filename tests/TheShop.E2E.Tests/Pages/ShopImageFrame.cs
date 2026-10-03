using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Pages;

/// <summary>
/// Page-object wrapper for one rendered <c>ShopImage</c> frame (.specs/shop-image/spec.md). Locates
/// frames through the component's own <c>data-shop-image</c> hook and measures real browser layout:
/// the frame, the caller allocation it sits in (its parent element), and whichever image branch or
/// named placeholder is currently displayed.
/// </summary>
public sealed class ShopImageFrame(ILocator root)
{
    /// <summary>Selector for every ShopImage frame root; the attribute is owned by the component.</summary>
    public const string FrameSelector = "[data-shop-image]";

    private const string MeasureScript = """
        frame => {
            const box = el => { const r = el.getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; };
            const shown = [...frame.children].filter(c => getComputedStyle(c).display !== 'none');
            const media = shown[0];
            const isImage = media instanceof HTMLImageElement;
            const style = getComputedStyle(media);
            const focusable = frame.querySelectorAll('a, button, input, select, textarea, [tabindex]');
            const [left, top, width, height] = box(frame);
            const [allocationLeft, allocationTop, allocationWidth, allocationHeight] = box(frame.parentElement);
            const [mediaLeft, mediaTop, mediaWidth, mediaHeight] = box(media);
            return {
                Left: left, Top: top, Width: width, Height: height,
                AllocationLeft: allocationLeft, AllocationTop: allocationTop,
                AllocationWidth: allocationWidth, AllocationHeight: allocationHeight,
                MediaLeft: mediaLeft, MediaTop: mediaTop, MediaWidth: mediaWidth, MediaHeight: mediaHeight,
                ShownCount: shown.length,
                IsImage: isImage,
                Branch: isImage ? (media.dataset.shopImageBranch ?? null) : null,
                Src: isImage ? media.getAttribute('src') : null,
                Alt: isImage ? media.getAttribute('alt') : null,
                NaturalWidth: isImage ? media.naturalWidth : 0,
                NaturalHeight: isImage ? media.naturalHeight : 0,
                ObjectFit: style.objectFit,
                ObjectPosition: style.objectPosition,
                PlaceholderText: isImage ? null : media.innerText.trim(),
                FocusableCount: focusable.length + (frame.hasAttribute('tabindex') ? 1 : 0),
                ViewportWidth: document.documentElement.clientWidth,
            };
        }
        """;

    /// <summary>The frame root.</summary>
    public ILocator Root { get; } = root;

    /// <summary>The frame whose subtree contains <paramref name="content"/> (e.g. an image found by its accessible name).</summary>
    public static ShopImageFrame Containing(IPage page, ILocator content) =>
        new(page.Locator(FrameSelector).Filter(new() { Has = content }));

    /// <summary>The named placeholder currently shown inside this frame.</summary>
    public ILocator Placeholder => Root.Locator(".shop-image-placeholder:visible");

    /// <summary>Measures the frame, its caller allocation, and the displayed branch in the live layout.</summary>
    public Task<ShopImageMetrics> MeasureAsync() => Root.EvaluateAsync<ShopImageMetrics>(MeasureScript);

    /// <summary>Waits until the displayed branch is an image that finished decoding.</summary>
    public async Task WaitForImageAsync()
    {
        var handle = await Root.ElementHandleAsync();
        await Root.Page.WaitForFunctionAsync("""
            frame => {
                const img = [...frame.children].find(c => getComputedStyle(c).display !== 'none');
                return img instanceof HTMLImageElement && img.complete && img.naturalWidth > 0;
            }
            """, handle, new() { Timeout = 15_000 });
    }

    /// <summary>Waits until the displayed branch is the named placeholder.</summary>
    public Task WaitForPlaceholderAsync() => Placeholder.WaitForAsync(new() { Timeout = 15_000 });
}

/// <summary>Live layout measurements of one <see cref="ShopImageFrame"/>, in CSS pixels.</summary>
public sealed class ShopImageMetrics
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double AllocationLeft { get; set; }
    public double AllocationTop { get; set; }
    public double AllocationWidth { get; set; }
    public double AllocationHeight { get; set; }
    public double MediaLeft { get; set; }
    public double MediaTop { get; set; }
    public double MediaWidth { get; set; }
    public double MediaHeight { get; set; }
    public int ShownCount { get; set; }
    public bool IsImage { get; set; }
    public string? Branch { get; set; }
    public string? Src { get; set; }
    public string? Alt { get; set; }
    public int NaturalWidth { get; set; }
    public int NaturalHeight { get; set; }
    public string ObjectFit { get; set; } = "";
    public string ObjectPosition { get; set; } = "";
    public string? PlaceholderText { get; set; }
    public int FocusableCount { get; set; }
    public double ViewportWidth { get; set; }

    /// <summary>Displayed frame width ÷ height.</summary>
    public double Ratio => Width / Height;

    /// <summary>Whether the frame lies inside its caller allocation (half-pixel rounding allowed).</summary>
    public bool IsInsideAllocation =>
        Left >= AllocationLeft - 0.5 && Top >= AllocationTop - 0.5
        && Left + Width <= AllocationLeft + AllocationWidth + 0.5
        && Top + Height <= AllocationTop + AllocationHeight + 0.5;

    /// <summary>Whether the frame causes no horizontal overflow of the viewport.</summary>
    public bool FitsViewportWidth => Left >= -0.5 && Left + Width <= ViewportWidth + 0.5;

    /// <summary>Whether the displayed image or placeholder covers exactly the frame box.</summary>
    public bool MediaFillsFrame =>
        Math.Abs(MediaLeft - Left) <= 0.5 && Math.Abs(MediaTop - Top) <= 0.5
        && Math.Abs(MediaWidth - Width) <= 0.5 && Math.Abs(MediaHeight - Height) <= 0.5;

    /// <summary>Whether the source's own width ÷ height differs from the frame's (so fit decides the outcome).</summary>
    public bool SourceRatioDiffersFromFrame =>
        NaturalHeight > 0 && Math.Abs((double)NaturalWidth / NaturalHeight - Ratio) > 0.05;
}
