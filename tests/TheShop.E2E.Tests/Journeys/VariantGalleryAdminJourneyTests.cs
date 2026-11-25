using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Admin gallery journeys for .specs/007_variant-urls-and-galleries/spec.md acceptance criteria.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "007_variant-urls-and-galleries")]
public sealed class VariantGalleryAdminJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    private ILocator Dialog => Page.GetByRole(AriaRole.Dialog, new() { Name = Strings.VariantImage_Title });
    private ILocator Image(Guid id) => Dialog.Locator($"[data-image-id='{id}'] .shop-image-tile-surface");
    private ILocator SelectedImages => Dialog.Locator(".shop-variant-image-gallery .shop-image-tile-selected");
    private async Task<EditProductPage> EditAsync(VariantGalleryData data)
    {
        var edit = new EditProductPage(Page);
        await edit.GotoAsync(data.Id);
        await edit.Form.NameField.WaitForAsync(new() { Timeout = 30_000 });
        return edit;
    }
    private async Task SaveAsync(EditProductPage edit, VariantGalleryData data)
    {
        await edit.Form.SaveAsync();
        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == TheShop.Web.Common.Routes.Admin.ManageProducts);
        data.AdminSaved = true;
    }

    [Fact]
    public async Task AC4_ReorderingSavedImagesChangesVariantCover()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var edit = await EditAsync(data);
        await edit.Form.OpenPinDialogAsync("Red / Small");
        await Assertions.Expect(SelectedImages).ToHaveCountAsync(5);
        await Image(data.Images[1]).ClickAsync();
        await Image(data.Images[1]).ClickAsync();
        await Assertions.Expect(SelectedImages.First).ToHaveAttributeAsync("data-image-id", data.Images[1].ToString());
        await Assertions.Expect(Image(data.Images[2]).Locator("..")).ToContainTextAsync(Strings.ImageUpload_Primary);
        await Dialog.GetByTestId("variant-image-save").ClickAsync(); await SaveAsync(edit, data);
        var detail = new VariantGalleryPage(Page, data); await detail.OpenVariantAsync(0);
        await detail.ExpectVariantAsync(0, 2, 5, 6, 7, 1);
    }

    [Fact]
    public async Task AC7_RemovingCoverPromotesNextAndFinalRemovalShowsPlaceholder()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var edit = await EditAsync(data);
        await edit.Form.OpenPinDialogAsync("Red / Small");
        await Image(data.Images[1]).ClickAsync();
        await Assertions.Expect(SelectedImages.First).ToHaveAttributeAsync("data-image-id", data.Images[2].ToString());
        await Dialog.GetByTestId("variant-image-save").ClickAsync();
        await edit.Form.OpenPinDialogAsync("Blue / Small");
        await Image(data.Images[3]).ClickAsync();
        await Assertions.Expect(SelectedImages).ToHaveCountAsync(0);
        await Dialog.GetByTestId("variant-image-save").ClickAsync(); await SaveAsync(edit, data);
        var detail = new VariantGalleryPage(Page, data); await detail.OpenVariantAsync(0);
        await detail.ExpectVariantAsync(0, 2, 5, 6, 7);
        await detail.OpenVariantAsync(1); await detail.ExpectVariantAsync(1);
    }

    [Fact]
    public async Task AC16_ForeignProductImagesCannotBeSelectedAndDuplicatesStaySingle()
    {
        await using var data = await VariantGalleryData.CreateAsync();
        await using var foreign = await VariantGalleryData.CreateAsync();
        var edit = await EditAsync(data); await edit.Form.OpenPinDialogAsync("Red / Small");
        await Assertions.Expect(Image(foreign.Images[1])).ToHaveCountAsync(0);
        await Image(data.Images[3]).ClickAsync(); await Image(data.Images[3]).ClickAsync();
        await Image(data.Images[3]).ClickAsync();
        await Assertions.Expect(Dialog.Locator($".shop-image-tile-selected[data-image-id='{data.Images[3]}']")).ToHaveCountAsync(1);
        await Assertions.Expect(SelectedImages).ToHaveCountAsync(6);
        await Dialog.GetByTestId("variant-image-save").ClickAsync(); await SaveAsync(edit, data);
        var rows = await data.RowsAsync("product_variant_images", $"variant_id=eq.{data.Variants[0]}");
        rows.EnumerateArray().Count(row => row.GetProperty("image_id").GetGuid() == data.Images[3]).Should().Be(1);

        // A forged admin request cannot bypass the product image library boundary.
        edit = await EditAsync(data);
        await Context.RouteAsync("**/rest/v1/rpc/save_product", async route =>
        {
            var request = JsonNode.Parse(route.Request.PostData!)!;
            request["payload"]!["variants"]!.AsArray()[0]!["image_ids"] =
                new JsonArray(JsonValue.Create(foreign.Images[1]));
            await route.ContinueAsync(new() { PostData = Encoding.UTF8.GetBytes(request.ToJsonString()) });
        });
        await edit.Form.SaveAsync();
        await Assertions.Expect(Page.GetByTestId("notification"))
            .ToContainTextAsync(Strings.Product_VariantImageNotInGallery);
        var unchanged = await data.RowsAsync("product_variant_images", $"variant_id=eq.{data.Variants[0]}");
        unchanged.GetArrayLength().Should().Be(rows.GetArrayLength());
    }

    [Fact]
    public async Task AC21_FailedSavePreservesStagedGalleryForRetry()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var edit = await EditAsync(data);
        await edit.Form.OpenPinDialogAsync("Blue / Small");
        await Image(data.Images[2]).ClickAsync();
        await Dialog.GetByTestId("variant-image-save").ClickAsync();
        var failed = false;
        await Context.RouteAsync("**/rest/v1/rpc/save_product", async route =>
        {
            if (!failed) { failed = true; await route.AbortAsync(); }
            else await route.ContinueAsync();
        });
        await edit.Form.SaveAsync();
        await Assertions.Expect(Page.GetByTestId("notification")).ToBeVisibleAsync();
        var before = await data.RowsAsync("product_variant_images", $"variant_id=eq.{data.Variants[1]}");
        before.GetArrayLength().Should().Be(1);
        await SaveAsync(edit, data);
        var detail = new VariantGalleryPage(Page, data); await detail.OpenVariantAsync(1);
        await detail.ExpectVariantAsync(1, 3, 2);
    }

    [Fact]
    public async Task AC24_VariantUploadJoinsLibraryWithoutChangingSiblingOrPrimary()
    {
        await using var data = await VariantGalleryData.CreateAsync(); var edit = await EditAsync(data);
        await edit.Form.OpenPinDialogAsync("Blue / Small");
        await Dialog.Locator("input[type=file]").SetInputFilesAsync(ProductImageFixtures.Png("e2e-variant-new.png"));
        await Assertions.Expect(SelectedImages).ToHaveCountAsync(2);
        await Dialog.GetByTestId("variant-image-save").ClickAsync(); await SaveAsync(edit, data);
        var library = await data.RowsAsync("product_images", $"product_id=eq.{data.Id}");
        library.GetArrayLength().Should().Be(9);
        library.EnumerateArray().Single(row => row.GetProperty("is_primary").GetBoolean())
            .GetProperty("id").GetGuid().Should().Be(data.Images[0]);
        var detail = new VariantGalleryPage(Page, data);
        await detail.OpenVariantAsync(0); await detail.ExpectVariantAsync(0, 1, 2, 5, 6, 7);
        await detail.OpenVariantAsync(1); await Assertions.Expect(detail.Slides).ToHaveCountAsync(2);
        await Assertions.Expect(detail.Slides.First.Locator("img")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex("/3\\.png"));
    }
}
