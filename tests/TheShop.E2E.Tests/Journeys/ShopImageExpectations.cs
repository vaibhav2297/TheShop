using FluentAssertions;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Components.Common;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Shared layout expectations for .specs/shop-image journeys, stated in the spec's terms: frame
/// ratio (FR-1), allocation and viewport containment (FR-2, RULE-1), whole-image fit (RULE-2), and
/// centered photographic crop (FR-3, RULE-3).
/// </summary>
internal static class ShopImageExpectations
{
    /// <summary>Phone, tablet, and desktop widths from plan TASK-005.</summary>
    public static readonly int[] Widths = [375, 768, 1440];

    /// <summary>Every preset whose frame follows a ratio (all but BrandLogo).</summary>
    public static readonly ShopImagePreset[] RatioPresets =
    [
        ShopImagePreset.ProductCard, ShopImagePreset.ProductDetail, ShopImagePreset.Thumbnail,
        ShopImagePreset.CategoryTile, ShopImagePreset.CategoryBanner, ShopImagePreset.Hero,
        ShopImagePreset.MobileBanner, ShopImagePreset.Editorial, ShopImagePreset.SocialSharing,
    ];

    /// <summary>The spec's FR-1/FR-4 frame ratio (width, height) for <paramref name="preset"/> at a viewport width.</summary>
    public static (int Width, int Height) ExpectedRatio(ShopImagePreset preset, int viewportWidth) => preset switch
    {
        ShopImagePreset.CategoryBanner when viewportWidth < 600 => (4, 5),
        ShopImagePreset.Hero when viewportWidth < 600 => (4, 5),
        ShopImagePreset.CategoryBanner => (16, 5),
        ShopImagePreset.Hero => (16, 9),
        ShopImagePreset.MobileBanner => (4, 5),
        ShopImagePreset.Editorial => (4, 3),
        ShopImagePreset.SocialSharing => (40, 21),
        _ => (1, 1),
    };

    /// <summary>Whether the preset shows the whole source (contain) rather than a centered crop.</summary>
    public static bool IsWholeImage(ShopImagePreset preset) => preset is ShopImagePreset.ProductCard
        or ShopImagePreset.ProductDetail or ShopImagePreset.Thumbnail or ShopImagePreset.BrandLogo
        or ShopImagePreset.SocialSharing;

    public static void ShouldHaveRatio(this ShopImageMetrics m, (int Width, int Height) ratio, string subject) =>
        m.Height.Should().BeApproximately(m.Width * ratio.Height / ratio.Width, 1.0,
            $"{subject} must be a {ratio.Width}:{ratio.Height} frame (measured {m.Width:0.#}×{m.Height:0.#})");

    public static void ShouldStayInsideItsAllocation(this ShopImageMetrics m, string subject)
    {
        m.IsInsideAllocation.Should().BeTrue(
            $"{subject} must stay inside its allocated space (frame {m.Left:0.#},{m.Top:0.#} {m.Width:0.#}×{m.Height:0.#}; " +
            $"allocation {m.AllocationLeft:0.#},{m.AllocationTop:0.#} {m.AllocationWidth:0.#}×{m.AllocationHeight:0.#})");
        m.FitsViewportWidth.Should().BeTrue($"{subject} must not overflow the {m.ViewportWidth:0}px viewport");
    }

    public static void ShouldShowTheWholeImage(this ShopImageMetrics m, string subject)
    {
        m.IsImage.Should().BeTrue($"{subject} must display its image");
        m.ObjectFit.Should().Be("contain", $"{subject} must keep the whole source visible, never cropped or stretched");
        m.MediaFillsFrame.Should().BeTrue($"{subject}'s image box must be the frame box, so containment is judged against the frame");
    }

    public static void ShouldFillWithACenteredCrop(this ShopImageMetrics m, string subject)
    {
        m.IsImage.Should().BeTrue($"{subject} must display its image");
        m.ObjectFit.Should().Be("cover", $"{subject} photography must fill the frame proportionally, never stretched");
        m.ObjectPosition.Should().Be("50% 50%", $"{subject} must trim excess edges evenly from the center");
        m.MediaFillsFrame.Should().BeTrue($"{subject}'s image must fill the whole frame");
    }
}
