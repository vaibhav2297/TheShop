using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Components.Products;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

public class VariantImageDialogTests : TestContext
{
    private readonly ProductImageDto _first = new(Guid.NewGuid(), "/first.webp", 0, true);
    private readonly ProductImageDto _second = new(Guid.NewGuid(), "/second.webp", 1, false);
    private readonly Guid _variantId = Guid.NewGuid();

    public VariantImageDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<BusyState>();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void Apply_ReorderedImages_UsesFirstAsCover()
    {
        VariantImageSelectionResult? result = null;
        var cut = Render<VariantImageDialog>(p => p
            .Add(c => c.CurrentVariantId, _variantId)
            .Add(c => c.VariantLabel, "Mango")
            .Add(c => c.GalleryImages, [_first, _second])
            .Add(c => c.CurrentImageIds, [_first.Id, _second.Id])
            .Add(c => c.OnCompleted, value => result = value));

        cut.Find($"[data-image-id='{_first.Id}'] .shop-image-tile-surface").Click();
        cut.Find($"[data-image-id='{_first.Id}'] .shop-image-tile-surface").Click();
        cut.FindAll(".shop-variant-image-gallery .shop-variant-image-option")
            .Select(tile => tile.GetAttribute("data-image-id"))
            .Should().Equal(_first.Id.ToString(), _second.Id.ToString());
        cut.Find($"[data-image-id='{_second.Id}'] .shop-image-tile-primary").Should().NotBeNull();
        cut.FindAll(".shop-variant-image-order").Should().BeEmpty();
        cut.Find("[data-testid='variant-image-save']").Click();

        result.Should().NotBeNull();
        result!.ImageIds.Should().Equal(_second.Id, _first.Id);
        result.TargetVariantIds.Should().Equal(_variantId);
        result.Append.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void Cancel_DiscardsSelection()
    {
        VariantImageSelectionResult? result = new([], [], false);
        var cut = Render<VariantImageDialog>(p => p
            .Add(c => c.CurrentVariantId, _variantId)
            .Add(c => c.GalleryImages, [_first])
            .Add(c => c.OnCompleted, value => result = value));

        cut.Find("[data-image-id] .shop-image-tile-surface").Click();
        cut.Find("[data-testid='variant-image-cancel']").Click();
        result.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public void Apply_BulkScope_ReturnsExplicitTargets()
    {
        var other = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        VariantImageSelectionResult? result = null;
        var cut = Render<VariantImageDialog>(p => p
            .Add(c => c.CurrentVariantId, _variantId)
            .Add(c => c.GalleryImages, [_first])
            .Add(c => c.SharedScopeLabel, "Flavour = Mango")
            .Add(c => c.SharedVariantIds, [_variantId, other])
            .Add(c => c.OnCompleted, value => result = value));

        cut.Markup.Should().Contain("All 2 variants with Flavour = Mango");
        cut.Find("[data-testid='variant-image-scope-all']").Change(true);
        cut.FindAll(".shop-variant-image-targets").Should().BeEmpty();
        cut.Find("[data-testid='variant-image-scope-all']").GetAttribute("type").Should().Be("checkbox");
        cut.Find(".shop-dialog-actions").TextContent.Should().NotContain("Save the product to publish these changes.");
        cut.Find("[data-image-id] .shop-image-tile-surface").Click();
        cut.Find("[data-testid='variant-image-save']").Click();

        result.Should().NotBeNull();
        result!.ImageIds.Should().Equal(_first.Id);
        result.TargetVariantIds.Should().BeEquivalentTo([_variantId, other]);
        result.TargetVariantIds.Should().NotContain(unrelated);
        result.Append.Should().BeTrue();
    }
}
