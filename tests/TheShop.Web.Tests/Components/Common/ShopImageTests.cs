using Bunit;
using FluentAssertions;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopImage"/>: preset frame classes and fit (FR-1, FR-3), the Hero and
/// Banner desktop/mobile branches (FR-4), named placeholders for missing or failed sources
/// inside an unchanged frame (FR-5), description semantics (FR-6), no added keyboard stops (FR-7),
/// Class/Style/attribute forwarding, and the JS failure bridge lifecycle. Actual rendered geometry
/// (ratios, crops, overflow across widths) is a browser concern.
/// <see href=".specs/shop-image/spec.md"/>
/// </summary>
public class ShopImageTests : TestContext
{
    private const string DesktopUrl = "https://example.com/desktop.webp";
    private const string MobileUrl = "https://example.com/mobile.webp";
    private const string Label = "Elf Bar BC5000";

    private readonly BunitJSModuleInterop _module;

    public ShopImageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./js/shopImage.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<ShopImage> RenderImage(
        ShopImagePreset preset = ShopImagePreset.SquareContain,
        string? src = DesktopUrl,
        string? mobileSrc = null,
        string alt = "Image of Elf Bar BC5000") =>
        Render<ShopImage>(p => p
            .Add(c => c.Preset, preset)
            .Add(c => c.Src, src)
            .Add(c => c.MobileSrc, mobileSrc)
            .Add(c => c.Alt, alt)
            .Add(c => c.PlaceholderLabel, Label));

    // =========================================================================
    // Preset frames and fit (FR-1, FR-3; AC-1–AC-4, AC-7–AC-9)
    // =========================================================================

    [Theory]
    [InlineData(ShopImagePreset.SquareContain, "shop-image-square-contain")]
    [InlineData(ShopImagePreset.SquareCover, "shop-image-square-cover")]
    [InlineData(ShopImagePreset.Banner, "shop-image-banner")]
    [InlineData(ShopImagePreset.Hero, "shop-image-hero")]
    [InlineData(ShopImagePreset.PortraitCover, "shop-image-portrait-cover")]
    [InlineData(ShopImagePreset.Editorial, "shop-image-editorial")]
    [InlineData(ShopImagePreset.BrandLogo, "shop-image-brand-logo")]
    [InlineData(ShopImagePreset.SocialSharing, "shop-image-social-sharing")]
    [Trait("Feature", "shop-image")]
    public void Render_WithEachPreset_TagsTheFrameWithThatPresetsGeometryClass(ShopImagePreset preset, string expectedClass)
    {
        var cut = RenderImage(preset);

        var frame = cut.Find("[data-shop-image]");
        frame.ClassList.Should().Contain("shop-image").And.Contain(expectedClass);
    }

    [Theory]
    [InlineData(ShopImagePreset.SquareContain)]
    [InlineData(ShopImagePreset.BrandLogo)]
    [InlineData(ShopImagePreset.SocialSharing)]
    [Trait("Feature", "shop-image")]
    public void Render_WithAWholeImagePreset_ContainsTheImageWithoutCropping(ShopImagePreset preset)
    {
        var cut = RenderImage(preset);

        cut.Find("img").ClassList.Should().Contain("shop-image-media").And.Contain("shop-image-contain")
            .And.NotContain("shop-image-cover");
        cut.FindAll("img").Should().ContainSingle();
    }

    [Theory]
    [InlineData(ShopImagePreset.SquareCover)]
    [InlineData(ShopImagePreset.Banner)]
    [InlineData(ShopImagePreset.Hero)]
    [InlineData(ShopImagePreset.PortraitCover)]
    [InlineData(ShopImagePreset.Editorial)]
    [Trait("Feature", "shop-image")]
    public void Render_WithAPhotographyPreset_FillsTheFrameWithACenteredCrop(ShopImagePreset preset)
    {
        var cut = RenderImage(preset);

        var images = cut.FindAll("img");
        images.Should().NotBeEmpty();
        foreach (var image in images)
        {
            image.ClassList.Should().Contain("shop-image-media").And.Contain("shop-image-cover")
                .And.NotContain("shop-image-contain");
        }
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_Always_LeavesTheSourceUrlUnchanged()
    {
        var cut = RenderImage(ShopImagePreset.SquareCover, src: DesktopUrl);

        cut.Find("img").GetAttribute("src").Should().Be(DesktopUrl);
    }

    // =========================================================================
    // Desktop and mobile branches (FR-4; AC-4, AC-5, AC-6)
    // =========================================================================

    [Theory]
    [InlineData(ShopImagePreset.Hero)]
    [InlineData(ShopImagePreset.Banner)]
    [Trait("Feature", "shop-image")]
    public void Render_WithDedicatedMobileArtwork_ShowsItInTheMobileBranchAndKeepsDesktopArtworkForDesktop(ShopImagePreset preset)
    {
        var cut = RenderImage(preset, src: DesktopUrl, mobileSrc: MobileUrl);

        cut.Find("img[data-shop-image-branch='desktop']").GetAttribute("src").Should().Be(DesktopUrl);
        cut.Find("img[data-shop-image-branch='mobile']").GetAttribute("src").Should().Be(MobileUrl);
        cut.Find("img[data-shop-image-branch='mobile']").ClassList.Should().Contain("shop-image-branch-mobile");
    }

    [Theory]
    [InlineData(ShopImagePreset.Hero, null)]
    [InlineData(ShopImagePreset.Hero, "   ")]
    [InlineData(ShopImagePreset.Banner, null)]
    [Trait("Feature", "shop-image")]
    public void Render_WithoutMobileArtwork_ReusesTheDesktopArtworkInTheMobileBranch(ShopImagePreset preset, string? mobileSrc)
    {
        var cut = RenderImage(preset, src: DesktopUrl, mobileSrc: mobileSrc);

        cut.Find("img[data-shop-image-branch='mobile']").GetAttribute("src").Should().Be(DesktopUrl);
    }

    [Theory]
    [InlineData(ShopImagePreset.SquareContain)]
    [InlineData(ShopImagePreset.PortraitCover)]
    [InlineData(ShopImagePreset.Editorial)]
    [Trait("Feature", "shop-image")]
    public void Render_WithANonResponsivePreset_IgnoresMobileArtworkAndRendersOneImage(ShopImagePreset preset)
    {
        var cut = RenderImage(preset, src: DesktopUrl, mobileSrc: MobileUrl);

        cut.FindAll("img").Should().ContainSingle().Which.GetAttribute("src").Should().Be(DesktopUrl);
    }

    // =========================================================================
    // Missing and failed sources (FR-5; AC-11)
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "shop-image")]
    public void Render_WithoutASource_ShowsTheNamedPlaceholderInsteadOfAnImage(string? src)
    {
        var cut = RenderImage(src: src);

        cut.FindAll("img").Should().BeEmpty();
        cut.Find(".shop-image-placeholder").TextContent.Trim().Should().Be(Label);
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public async Task ImageFails_ForTheCurrentSource_ReplacesItWithTheNamedPlaceholderInsideTheSameFrame()
    {
        var cut = Render<ShopImage>(p => p
            .Add(c => c.Src, DesktopUrl)
            .Add(c => c.Preset, ShopImagePreset.SquareContain)
            .Add(c => c.PlaceholderLabel, Label)
            .Add(c => c.Style, "width: 240px;"));
        var frameBefore = cut.Find("[data-shop-image]");
        var frameClass = frameBefore.GetAttribute("class");
        var frameStyle = frameBefore.GetAttribute("style");

        await cut.InvokeAsync(() => cut.Instance.OnImageFailed("desktop", DesktopUrl));

        cut.FindAll("img").Should().BeEmpty();
        cut.Find(".shop-image-placeholder").TextContent.Trim().Should().Be(Label);
        var frameAfter = cut.Find("[data-shop-image]");
        frameAfter.GetAttribute("class").Should().Be(frameClass);
        frameAfter.GetAttribute("style").Should().Be(frameStyle);
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public async Task ImageFails_ForASourceThatHasSinceBeenReplaced_IsIgnored()
    {
        var cut = RenderImage(src: "https://example.com/old.webp");
        cut.Render(p => p.Add(c => c.Src, DesktopUrl));

        await cut.InvokeAsync(() => cut.Instance.OnImageFailed("desktop", "https://example.com/old.webp"));

        cut.Find("img").GetAttribute("src").Should().Be(DesktopUrl);
        cut.FindAll(".shop-image-placeholder").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public async Task ReplaceSource_AfterAFailure_ShowsTheNewImageAgain()
    {
        var cut = RenderImage(src: "https://example.com/broken.webp");
        await cut.InvokeAsync(() => cut.Instance.OnImageFailed("desktop", "https://example.com/broken.webp"));

        cut.Render(p => p.Add(c => c.Src, DesktopUrl));

        cut.Find("img").GetAttribute("src").Should().Be(DesktopUrl);
        cut.FindAll(".shop-image-placeholder").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public async Task ImageFails_InTheMobileBranch_LeavesTheDesktopBranchShowingItsImage()
    {
        var cut = RenderImage(ShopImagePreset.Hero, src: DesktopUrl, mobileSrc: MobileUrl);

        await cut.InvokeAsync(() => cut.Instance.OnImageFailed("mobile", MobileUrl));

        cut.Find("img[data-shop-image-branch='desktop']").GetAttribute("src").Should().Be(DesktopUrl);
        cut.FindAll("img[data-shop-image-branch='mobile']").Should().BeEmpty();
        cut.Find(".shop-image-placeholder.shop-image-branch-mobile").TextContent.Trim().Should().Be(Label);
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public async Task ImageFails_InTheDesktopBranch_LeavesDedicatedMobileArtworkShowing()
    {
        var cut = RenderImage(ShopImagePreset.Banner, src: DesktopUrl, mobileSrc: MobileUrl);

        await cut.InvokeAsync(() => cut.Instance.OnImageFailed("desktop", DesktopUrl));

        cut.Find("img[data-shop-image-branch='mobile']").GetAttribute("src").Should().Be(MobileUrl);
        cut.Find(".shop-image-placeholder.shop-image-branch-desktop").TextContent.Trim().Should().Be(Label);
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public async Task ImageFails_ForAMobileReportOnANonResponsivePreset_IsIgnored()
    {
        var cut = RenderImage(ShopImagePreset.SquareContain, src: DesktopUrl);

        await cut.InvokeAsync(() => cut.Instance.OnImageFailed("mobile", DesktopUrl));

        cut.Find("img").GetAttribute("src").Should().Be(DesktopUrl);
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_WithABrandLogoPlaceholder_KeepsTheCallersReservedSpace()
    {
        var cut = Render<ShopImage>(p => p
            .Add(c => c.Src, null)
            .Add(c => c.Preset, ShopImagePreset.BrandLogo)
            .Add(c => c.PlaceholderLabel, "Elf Bar")
            .Add(c => c.Style, "width: 52px; height: 52px;"));

        var frame = cut.Find("[data-shop-image]");
        frame.GetAttribute("style").Should().Contain("width: 52px").And.Contain("height: 52px");
        frame.QuerySelector(".shop-image-placeholder")!.TextContent.Trim().Should().Be("Elf Bar");
    }

    // =========================================================================
    // Descriptions and decorative semantics (FR-6; AC-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_WithAMeaningfulDescription_UsesItAsTheImagesAltText()
    {
        var cut = RenderImage(alt: "Image of Elf Bar BC5000");

        cut.Find("img").GetAttribute("alt").Should().Be("Image of Elf Bar BC5000");
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_WithADecorativeImage_RendersAnEmptyAltText()
    {
        var cut = RenderImage(alt: string.Empty);

        cut.Find("img").GetAttribute("alt").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_WithAMissingMeaningfulImage_ExposesThePlaceholderAsAnImageWithThatDescription()
    {
        var cut = RenderImage(src: null, alt: "Image of Elf Bar BC5000");

        var placeholder = cut.Find(".shop-image-placeholder");
        placeholder.GetAttribute("role").Should().Be("img");
        placeholder.GetAttribute("aria-label").Should().Be("Image of Elf Bar BC5000");
        placeholder.QuerySelector(".shop-image-label")!.GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_WithAMissingDecorativeImage_HidesThePlaceholderFromAssistiveTechnology()
    {
        var cut = RenderImage(src: null, alt: string.Empty);

        var placeholder = cut.Find(".shop-image-placeholder");
        placeholder.GetAttribute("aria-hidden").Should().Be("true");
        placeholder.GetAttribute("role").Should().NotBe("img");
        placeholder.HasAttribute("aria-label").Should().BeFalse();
        placeholder.TextContent.Trim().Should().Be(Label, "the name stays visible even when hidden from screen readers");
    }

    // =========================================================================
    // No keyboard stops (FR-7; AC-13)
    // =========================================================================

    [Theory]
    [InlineData(DesktopUrl)]
    [InlineData(null)]
    [Trait("Feature", "shop-image")]
    public void Render_Always_AddsNoFocusableElements(string? src)
    {
        var cut = RenderImage(ShopImagePreset.Hero, src: src);

        cut.FindAll("a, button, input, [tabindex]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_Always_ExposesNoGroupRoleThatWouldHideTheAltTextFromASurroundingLink()
    {
        var cut = RenderImage(ShopImagePreset.BrandLogo, src: DesktopUrl);

        cut.Find("[data-shop-image]").GetAttribute("role").Should().Be("none",
            "a group role on the frame would empty the accessible name of a link wrapping the image");
    }

    // =========================================================================
    // Class, Style, and attribute forwarding (Rules 23, 24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_WithConsumerClassStyleAndAttributes_ForwardsThemToTheFrame()
    {
        var cut = Render<ShopImage>(p => p
            .Add(c => c.Src, DesktopUrl)
            .Add(c => c.PlaceholderLabel, Label)
            .Add(c => c.Class, "media-image")
            .Add(c => c.Style, "width: 120px;")
            .AddUnmatched("data-testid", "product-image"));

        var frame = cut.Find("[data-shop-image]");
        frame.ClassList.Should().Contain("media-image");
        frame.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Should().EndWith("media-image");
        frame.GetAttribute("style").Should().Contain("width: 120px");
        frame.GetAttribute("data-testid").Should().Be("product-image");
    }

    // =========================================================================
    // JS failure bridge lifecycle
    // =========================================================================

    [Fact]
    [Trait("Feature", "shop-image")]
    public void Render_AfterFirstRender_StartsObservingItsOwnFrameForImageFailures()
    {
        var cut = RenderImage();

        var frameId = cut.Find("[data-shop-image]").GetAttribute("data-shop-image");
        _module.VerifyInvoke("observe").Arguments[0].Should().Be(frameId);
    }

    [Fact]
    [Trait("Feature", "shop-image")]
    public async Task Dispose_WhenTheComponentIsRemoved_StopsObservingItsFrame()
    {
        var cut = RenderImage();
        var frameId = cut.Find("[data-shop-image]").GetAttribute("data-shop-image");

        await DisposeComponentsAsync();

        _module.VerifyInvoke("unobserve").Arguments[0].Should().Be(frameId);
    }
}
