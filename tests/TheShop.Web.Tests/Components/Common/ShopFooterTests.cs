using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for the <see cref="ShopFooter"/> component. Verifies the brand block (logo,
/// tagline, social controls), the four data-driven link sections, the accessible
/// <c>footer</c> landmark, that items without a destination render as inert text while
/// contact items render as <c>mailto:</c>/<c>tel:</c> links, and that consumer
/// <c>Class</c> is forwarded to the root.
/// <see href=".specs/footer/spec.md"/>
/// </summary>
public class ShopFooterTests : TestContext
{
    public ShopFooterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    // =========================================================================
    // Landmark + accessibility
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_Always_RootIsFooterLandmarkWithLocalizedAriaLabel()
    {
        var cut = Render<ShopFooter>();

        var footer = cut.Find("footer");
        footer.GetAttribute("aria-label").Should().Be(Strings.Footer_AriaLabel,
            "the footer landmark must carry the localized aria-label for screen readers");
    }

    // =========================================================================
    // Brand block
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_Always_ShowsBrandLogoAndTagline()
    {
        var cut = Render<ShopFooter>();

        cut.Find($"img[src='{ShopIcons.ImageAssets.LogoPrimary}']").Should().NotBeNull(
            "the brand column must render the primary logo");
        cut.Markup.Should().Contain(Strings.Footer_Tagline,
            "the brand column must render the tagline");
    }

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_Always_ShowsThreeSocialControlsWithAriaLabels()
    {
        var cut = Render<ShopFooter>();

        cut.Find($"[aria-label='{Strings.Footer_Social_Facebook}']").Should().NotBeNull();
        cut.Find($"[aria-label='{Strings.Footer_Social_WhatsApp}']").Should().NotBeNull();
        cut.Find($"[aria-label='{Strings.Footer_Social_Instagram}']").Should().NotBeNull();
    }

    // =========================================================================
    // Section headings
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_Always_ShowsAllFourSectionTitles()
    {
        var cut = Render<ShopFooter>();

        cut.Markup.Should().Contain(Strings.Footer_Shop_Title);
        cut.Markup.Should().Contain(Strings.Footer_Help_Title);
        cut.Markup.Should().Contain(Strings.Footer_Legal_Title);
        cut.Markup.Should().Contain(Strings.Footer_Contact_Title);
    }

    // =========================================================================
    // Items with no destination render as inert text (not links)
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_ItemWithoutHref_RendersAsInertText()
    {
        var cut = Render<ShopFooter>();

        var anchors = cut.FindAll("a").Select(a => a.TextContent.Trim());
        anchors.Should().NotContain(Strings.Footer_Shop_Disposables,
            "a footer item without a destination must render as plain text, not a link");
        cut.Markup.Should().Contain(Strings.Footer_Shop_Disposables,
            "the inert item label must still be visible");
    }

    // =========================================================================
    // Contact items are wired as mailto:/tel: links
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_ContactEmail_RendersAsMailtoLink()
    {
        var cut = Render<ShopFooter>();

        cut.Find($"a[href='mailto:{Strings.Footer_Contact_Email}']").Should().NotBeNull(
            "the contact email must be a mailto: link");
    }

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_ContactPhone_RendersAsTelLink()
    {
        var cut = Render<ShopFooter>();

        var telLinks = cut.FindAll("a")
            .Select(a => a.GetAttribute("href"))
            .Where(href => href is not null && href.StartsWith("tel:"));

        telLinks.Should().ContainSingle("the contact phone must be a tel: link");
    }

    // =========================================================================
    // Class forwarding (Rule 24) — consumer's Class reaches the root
    // =========================================================================

    [Fact]
    [Trait("Feature", "footer")]
    public void Render_WithConsumerClass_ForwardsClassToRoot()
    {
        var cut = Render<ShopFooter>(p => p.Add(c => c.Class, "mt-auto"));

        var footer = cut.Find("footer");
        footer.ClassList.Should().Contain("mt-auto",
            "the consumer Class must be forwarded to the footer root (Rule 24)");
        footer.ClassList.Should().Contain("shop-footer",
            "the component-scoped class must remain present alongside the consumer Class");
    }
}
