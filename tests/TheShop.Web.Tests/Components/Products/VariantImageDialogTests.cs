using Bunit;
using FluentAssertions;
using Microsoft.JSInterop;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Components.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

public class VariantImageDialogTests : TestContext
{
    private readonly ProductImageDto _first = new(Guid.NewGuid(), "/first.webp", 0, true);
    private readonly ProductImageDto _second = new(Guid.NewGuid(), "/second.webp", 1, false);

    public VariantImageDialogTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Initialization_BrowserInteropFailure_CancelsInsteadOfLeavingAnOpenRequest()
    {
        var results = new List<VariantImagePinResult?>();
        JSInterop.SetupModule("./js/shopDialog.js").SetupVoid("show", _ => true)
            .SetException(new JSException("Modal unavailable"));
        var cut = Render<VariantImageDialog>(p => p.Add(c => c.OnCompleted, value => results.Add(value)));
        cut.WaitForAssertion(() => results.Should().ContainSingle().Which.Should().BeNull());
    }

    [Fact]
    public async Task Selection_TogglesAndSavesEmpty_AsAnAcceptedResult()
    {
        var results = new List<VariantImagePinResult?>();
        var cut = Render<VariantImageDialog>(p => p
            .Add(c => c.VariantLabel, "Mango")
            .Add(c => c.GalleryImages, [_first, _second])
            .Add(c => c.CurrentImageId, _first.Id)
            .Add(c => c.OnCompleted, value => results.Add(value)));

        var selected = cut.Find($"[data-image-id='{_first.Id}']");
        selected.TagName.Should().Be("BUTTON");
        selected.GetAttribute("type").Should().Be("button");
        selected.GetAttribute("aria-pressed").Should().Be("true");
        selected.GetAttribute("aria-label").Should().Be(string.Format(Strings.VariantImage_ImageLabel, 1, "Mango"));
        selected.QuerySelector(".shop-variant-image-selected-icon")!.GetAttribute("aria-hidden").Should().Be("true");
        cut.FindComponents<ShopIcon>().Should().ContainSingle(c =>
            c.Instance.Class == "shop-variant-image-selected-icon" && c.Instance.Icon == TheShop.Web.Theme.ShopIcons.Outlined.Check);
        await cut.Find($"[data-image-id='{_second.Id}']").ClickAsync(new());
        cut.FindAll("[aria-pressed='true']").Should().ContainSingle();
        cut.Find($"[data-image-id='{_first.Id}']").QuerySelector(".shop-variant-image-selected-icon").Should().BeNull();
        cut.Find($"[data-image-id='{_second.Id}']").QuerySelector(".shop-variant-image-selected-icon").Should().NotBeNull();
        await cut.Find($"[data-image-id='{_second.Id}']").ClickAsync(new());
        cut.FindAll("[aria-pressed='true']").Should().BeEmpty();
        cut.FindAll(".shop-variant-image-selected-icon").Should().BeEmpty();
        await cut.Find("[data-testid='variant-image-save']").ClickAsync(new());
        await cut.Find("[data-testid='variant-image-save']").ClickAsync(new());
        await cut.InvokeAsync(() => cut.FindComponent<ShopDialog>().Instance.DismissAsync());
        results.Should().ContainSingle().Which.Should().Be(new VariantImagePinResult(null, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dismiss_CancelOrBrowserDismiss_ReturnsNullOnce(bool browserDismiss)
    {
        var results = new List<VariantImagePinResult?>();
        var cut = Render<VariantImageDialog>(p => p
            .Add(c => c.GalleryImages, [_first])
            .Add(c => c.OnCompleted, value => results.Add(value)));
        await cut.Find("[data-image-id]").ClickAsync(new());
        if (browserDismiss)
            await cut.InvokeAsync(() => cut.FindComponent<ShopDialog>().Instance.DismissAsync());
        else
            await cut.Find("[data-testid='variant-image-cancel']").ClickAsync(new());
        await cut.Find("[data-testid='variant-image-save']").ClickAsync(new());
        results.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public async Task Scope_SharedVariants_UsesExclusiveShopCheckboxesAndReturnsChoice()
    {
        VariantImagePinResult? result = null;
        var cut = Render<VariantImageDialog>(p => p
            .Add(c => c.GalleryImages, [_first])
            .Add(c => c.SharedScopeLabel, "Color = Red")
            .Add(c => c.SharedScopeCount, 2)
            .Add(c => c.OnCompleted, value => result = value));
        cut.Find("fieldset legend").TextContent.Should().Be(Strings.VariantImage_ApplyToLabel);
        cut.FindAll("input[type='radio']").Should().BeEmpty();
        cut.FindAll("input[type='checkbox']").Should().HaveCount(2);
        cut.FindComponents<ShopCheckbox>().Should().OnlyContain(c => c.Instance.Size == TheShop.Web.Common.UI.ShopSize.Medium);
        cut.Find("[data-testid='variant-image-scope-this']").HasAttribute("checked").Should().BeTrue();
        cut.Find("[data-testid='variant-image-scope-all']").Change(true);
        cut.Find("[data-testid='variant-image-scope-this']").HasAttribute("checked").Should().BeFalse();
        cut.Find("[data-testid='variant-image-scope-all']").HasAttribute("checked").Should().BeTrue();
        cut.Find("[data-testid='variant-image-scope-all']").Change(false);
        cut.Find("[data-testid='variant-image-scope-all']").HasAttribute("checked").Should().BeTrue("one scope must remain selected");
        cut.Find("[data-testid='variant-image-scope-this']").Change(true);
        cut.Find("[data-testid='variant-image-scope-all']").HasAttribute("checked").Should().BeFalse();
        cut.Find("[data-testid='variant-image-scope-this']").HasAttribute("checked").Should().BeTrue();
        cut.Find("[data-testid='variant-image-scope-this']").Change(false);
        cut.Find("[data-testid='variant-image-scope-this']").HasAttribute("checked").Should().BeTrue("one scope must remain selected");
        cut.Find("[data-testid='variant-image-scope-all']").Change(true);
        await cut.Find("[data-image-id]").ClickAsync(new());
        await cut.Find("[data-testid='variant-image-save']").ClickAsync(new());
        result.Should().Be(new VariantImagePinResult(_first.Id, true));
    }

    [Fact]
    public async Task Parameters_GalleryAndScopeRemoved_DropsStaleSelectionAndScope()
    {
        VariantImagePinResult? result = null;
        var cut = Render<VariantImageDialog>(p => p
            .Add(c => c.GalleryImages, [_first])
            .Add(c => c.CurrentImageId, _first.Id)
            .Add(c => c.SharedScopeLabel, "Color = Red")
            .Add(c => c.SharedScopeCount, 2)
            .Add(c => c.OnCompleted, value => result = value));
        cut.Find("[data-testid='variant-image-scope-all']").Change(true);
        cut.Render(p => p.Add(c => c.GalleryImages, []).Add(c => c.SharedScopeCount, 1));
        cut.FindAll("fieldset, [data-image-id]").Should().BeEmpty();
        cut.Find(".shop-variant-image-empty").TextContent.Should().Be(Strings.VariantImage_NoImages);
        await cut.Find("[data-testid='variant-image-save']").ClickAsync(new());
        result.Should().Be(new VariantImagePinResult(null, false));
    }
}
