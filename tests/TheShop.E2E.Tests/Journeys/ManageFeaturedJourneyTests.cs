using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Admin browser evidence for .specs/005_homepage-featured/spec.md.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "005_homepage-featured")]
public sealed class ManageFeaturedJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    private ManageFeaturedPage Editor => new(Page);
    private async Task OpenEmptyAsync(int width = 1440, int height = 900)
    {
        using var data = new FeaturedData();
        await data.ResetAsync();
        await Page.SetViewportSizeAsync(width, height);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
    }
    private static async Task<string> SourceAsync(ILocator image) => (await image.Locator("img").GetAttributeAsync("src"))!;
    private async Task AssertSavedAsync(FeaturedData data)
    {
        var row = (await data.RowsAsync())[0];
        row.GetProperty("title").GetString().Should().Be(data.Title);
        row.GetProperty("tile1_image_key").GetString().Should().Be(data.Key1);
        row.GetProperty("tile2_image_key").GetString().Should().Be(data.Key2);
        row.GetProperty("is_visible").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task AC1_HomepageNavigationShowsDefaultsAndLiveAddHeading()
    {
        using var data = new FeaturedData();
        await data.ResetAsync();
        await Editor.OpenFromHomepageAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.Homepage_Featured);
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.Featured_AddHeading, Strings.Homepage_Featured));
        await Assertions.Expect(Editor.Status(true)).ToHaveAttributeAsync("aria-pressed", "true");
        for (var number = 1; number <= 2; number++)
        {
            await Assertions.Expect(Editor.TileTitle(number)).ToHaveValueAsync("");
            await Assertions.Expect(Editor.Image(number).Locator("img")).ToHaveCountAsync(0);
        }
        var first = (await Editor.Tile(1).BoundingBoxAsync())!;
        (await Editor.Tile(2).BoundingBoxAsync())!.X.Should().BeGreaterThan(first.X);
        await Assertions.Expect(Editor.Save).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Cancel).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Navigation, new() { Name = Strings.Breadcrumb_AriaLabel })).ToContainTextAsync(Strings.Homepage_Featured);
        await Editor.Title.FillAsync("Summer Picks");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.Featured_AddHeading, "Summer Picks"));
    }

    [Fact]
    public async Task AC2_HiddenEditHydratesBothTilesAndLiveHeading()
    {
        using var data = new FeaturedData();
        await data.SeedAsync(await Editor.ArtworkAsync());
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
        for (var number = 1; number <= 2; number++)
        {
            var name = number == 1 ? "First" : "Second";
            await Assertions.Expect(Editor.TileTitle(number)).ToHaveValueAsync(name + " saved tile");
            await Assertions.Expect(Editor.Label(number)).ToHaveValueAsync(name + " label");
            await Assertions.Expect(Editor.ButtonLabel(number)).ToHaveValueAsync(name + " button");
            await Editor.Image(number).Locator("img").EvaluateAsync("el=>el.decode()");
        }
        await Assertions.Expect(Editor.ButtonLink(1)).ToHaveValueAsync("/products");
        await Assertions.Expect(Editor.ButtonLink(2)).ToHaveValueAsync("https://example.test/second");
        await Assertions.Expect(Editor.Status(false)).ToHaveAttributeAsync("aria-pressed", "true");
        await Editor.Title.FillAsync("Summer Picks");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.Featured_EditHeading, "Summer Picks"));
    }

    [Fact]
    public async Task AC5_PickDropReplaceRemoveAndCancelledPickerPreserveOtherTileAndSavedState()
    {
        using var data = new FeaturedData();
        var png = await Editor.ArtworkAsync();
        await data.SeedAsync(png);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
        var other = await SourceAsync(Editor.Image(2));
        await Editor.SelectValidAsync(1, png);
        var selected = await SourceAsync(Editor.Image(1));
        var chooser = await Page.RunAndWaitForFileChooserAsync(() => Editor.Image(1).GetByRole(AriaRole.Button, new() { Name = Strings.ImageUpload_Replace }).ClickAsync());
        await chooser.SetFilesAsync(Array.Empty<FilePayload>());
        await Assertions.Expect(Editor.Image(1).Locator("img")).ToHaveAttributeAsync("src", selected);
        var replacement = await Editor.ArtworkAsync(color: "#3399cc");
        await Editor.Image(1).Locator("[data-file-dropzone]").EvaluateAsync("""
            (el, base64) => { const bytes = Uint8Array.from(atob(base64), c=>c.charCodeAt(0)); const transfer=new DataTransfer();
                transfer.items.add(new File([bytes],'dropped.png',{type:'image/png'}));
                el.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:transfer})); }
            """, Convert.ToBase64String(replacement));
        await Assertions.Expect(Editor.Image(1).Locator("img")).Not.ToHaveAttributeAsync("src", selected);
        await Assertions.Expect(Editor.Image(2).Locator("img")).ToHaveAttributeAsync("src", other);
        await Editor.Image(1).GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape(string.Format(Strings.Featured_RemoveImage, 1))) }).ClickAsync();
        await Assertions.Expect(Editor.Image(1).Locator("img")).ToHaveCountAsync(0);
        await Assertions.Expect(Editor.TileTitle(1)).ToHaveValueAsync("First saved tile");
        await Assertions.Expect(Editor.Image(2).Locator("img")).ToHaveAttributeAsync("src", other);
        await AssertSavedAsync(data);
    }

    [Fact]
    public async Task AC6_RealPngJpegWebpAndExactFiveMegabytesDecodeInSquareFrames()
    {
        await OpenEmptyAsync();
        foreach (var (mime, extension) in new[] { ("image/png", "png"), ("image/jpeg", "jpg"), ("image/webp", "webp") })
        {
            await Editor.SelectValidAsync(1, await Editor.ArtworkAsync(mime), mime, "tile." + extension);
            var frame = (await Editor.Image(1).Locator(".shop-image-tile").BoundingBoxAsync())!;
            frame.Width.Should().BeApproximately(frame.Height, 2);
        }
        var exact = new byte[5_242_880];
        (await Editor.ArtworkAsync()).CopyTo(exact, 0);
        await Editor.SelectValidAsync(2, exact);
        await Assertions.Expect(Editor.Image(2)).ToContainTextAsync(Strings.Featured_UploadHint);
        await Assertions.Expect(Editor.Tile(2)).ToContainTextAsync(Strings.Featured_ImageHint);
    }

    [Fact]
    public async Task AC7_InvalidSelectionsKeepAcceptedArtworkAndShowSpecificFeedback()
    {
        await OpenEmptyAsync();
        await Editor.SelectValidAsync(1, await Editor.ArtworkAsync());
        var accepted = await SourceAsync(Editor.Image(1));
        var invalid = new[] {
            (Bytes: new byte[5_242_881], Mime: "image/png", Error: Strings.Featured_ImageTooLarge),
            (Bytes: await Editor.ArtworkAsync(width: 1199, height: 1199), Mime: "image/png", Error: Strings.Featured_ImageDimensionsInvalid),
            (Bytes: await Editor.ArtworkAsync(width: 1200, height: 1201), Mime: "image/png", Error: Strings.Featured_ImageDimensionsInvalid),
            (Bytes: new byte[] {137,80,78,71,13,10,26,10,0,0}, Mime: "image/png", Error: Strings.Featured_ImageUnreadable),
            (Bytes: new byte[] {1,2,3}, Mime: "image/gif", Error: Strings.Featured_ImageInvalidType) };
        foreach (var candidate in invalid)
        {
            await Editor.SelectAsync(1, candidate.Bytes, candidate.Mime);
            await Assertions.Expect(Editor.Image(1)).ToContainTextAsync(candidate.Error);
            await Assertions.Expect(Editor.Image(1).Locator("img")).ToHaveAttributeAsync("src", accepted);
        }
    }

    [Fact]
    public async Task AC8_MissingRequiredFieldsShowErrorsWithoutSavingOrFillingHeadingFallback()
    {
        await OpenEmptyAsync();
        await Editor.Title.FillAsync("   ");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.Featured_AddHeading, Strings.Homepage_Featured));
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Title).ToHaveAttributeAsync("aria-invalid", "true");
        for (var number = 1; number <= 2; number++)
        {
            await Assertions.Expect(Editor.Tile(number)).ToContainTextAsync(Strings.Featured_ImageRequired);
            await Assertions.Expect(Editor.TileTitle(number)).ToHaveAttributeAsync("aria-invalid", "true");
        }
        using var data = new FeaturedData();
        (await data.RowsAsync()).GetArrayLength().Should().Be(0);
        await Assertions.Expect(Editor.Title).ToHaveValueAsync("   ");
        await Editor.FillValidAsync(data.Title, await Editor.ArtworkAsync());
        await Editor.SaveAndReturnAsync();
        await Editor.GotoAsync();
        await Editor.Title.FillAsync(" ");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.Featured_EditHeading, Strings.Homepage_Featured));
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(" ");
        (await data.RowsAsync())[0].GetProperty("title").GetString().Should().Be(data.Title);
    }

    [Fact]
    public async Task AC9_IncompleteButtonPairsTargetMissingFieldsAndBlankPairsSave()
    {
        await OpenEmptyAsync();
        await Editor.FillValidAsync("No buttons", await Editor.ArtworkAsync());
        await Editor.ButtonLabel(1).FillAsync("Shop");
        await Editor.ButtonLink(2).FillAsync("/products");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.ButtonLink(1)).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(Editor.ButtonLabel(2)).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(Editor.Tile(1)).ToContainTextAsync(Strings.Featured_ButtonPairRequired);
        await Editor.ButtonLabel(1).FillAsync("");
        await Editor.ButtonLink(2).FillAsync("");
        await Editor.SaveAndReturnAsync();
        await Editor.GotoAsync();
        for (var number = 1; number <= 2; number++)
        {
            await Assertions.Expect(Editor.ButtonLabel(number)).ToHaveValueAsync("");
            await Assertions.Expect(Editor.ButtonLink(number)).ToHaveValueAsync("");
        }
    }

    [Fact]
    public async Task AC10_UnsafeLinksFailAndRootRelativeAndHttpLinksPersist()
    {
        await OpenEmptyAsync();
        await Editor.FillValidAsync("Linked tiles", await Editor.ArtworkAsync());
        await Editor.ButtonLabel(1).FillAsync("Shop");
        await Editor.ButtonLabel(2).FillAsync("More");
        await Editor.ButtonLink(2).FillAsync("https://example.test/products");
        foreach (var link in new[] { "//example.test", "javascript:alert(1)", "data:text/html,test", "/bad\\path", "/bad%zz" })
        {
            await Editor.ButtonLink(1).FillAsync(link);
            await Editor.Save.ClickAsync();
            await Assertions.Expect(Editor.ButtonLink(1)).ToHaveAttributeAsync("aria-invalid", "true");
            await Assertions.Expect(Editor.Tile(1)).ToContainTextAsync(Strings.Featured_ButtonLinkInvalid);
        }
        await Editor.ButtonLink(1).FillAsync("/products?brand=1#top");
        await Editor.SaveAndReturnAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.ButtonLink(1)).ToHaveValueAsync("/products?brand=1#top");
        await Assertions.Expect(Editor.ButtonLink(2)).ToHaveValueAsync("https://example.test/products");
        await Editor.ButtonLink(2).FillAsync("http://example.test/unavailable");
        await Editor.SaveAndReturnAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.ButtonLink(2)).ToHaveValueAsync("http://example.test/unavailable");
    }

    [Fact]
    public async Task AC11_SuccessWaitsForCompleteCommitAndReopenPreservesHiddenThenVisibleContent()
    {
        await OpenEmptyAsync();
        using var data = new FeaturedData();
        await Editor.FillValidAsync(data.Title, await Editor.ArtworkAsync());
        await Editor.Label(1).FillAsync("First label");
        await Editor.Label(2).FillAsync("Second label");
        await Editor.ButtonLabel(1).FillAsync("Browse");
        await Editor.ButtonLink(1).FillAsync("/products");
        await Editor.Status(false).ClickAsync();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Hold(IRoute route) { arrived.TrySetResult(); await release.Task; await route.ContinueAsync(); }
        await Context.RouteAsync("**/rest/v1/rpc/save_featured_admin", Hold);
        try
        {
            await Editor.Save.ClickAsync();
            await arrived.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Assertions.Expect(Editor.Save).ToBeDisabledAsync();
            await Assertions.Expect(Editor.Snackbar.Filter(new() { HasText = Strings.Featured_Saved })).ToHaveCountAsync(0);
            (await data.RowsAsync()).GetArrayLength().Should().Be(0);
        }
        finally { release.TrySetResult(); await Context.UnrouteAsync("**/rest/v1/rpc/save_featured_admin", Hold); }
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Assertions.Expect(Editor.Snackbar).ToContainTextAsync(Strings.Featured_Saved);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
        for (var number = 1; number <= 2; number++)
        {
            await Assertions.Expect(Editor.TileTitle(number)).ToHaveValueAsync($"{data.Title} tile {number}");
            await Assertions.Expect(Editor.Label(number)).ToHaveValueAsync(number == 1 ? "First label" : "Second label");
            await Editor.Image(number).Locator("img").EvaluateAsync("el=>el.decode()");
        }
        await Assertions.Expect(Editor.Status(false)).ToHaveAttributeAsync("aria-pressed", "true");
        var row = (await data.RowsAsync())[0];
        await Editor.Status(true).ClickAsync();
        await Editor.SaveAndReturnAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Status(true)).ToHaveAttributeAsync("aria-pressed", "true");
        var visible = (await data.RowsAsync())[0];
        visible.GetProperty("tile1_image_key").GetString().Should().Be(row.GetProperty("tile1_image_key").GetString());
        visible.GetProperty("tile2_image_key").GetString().Should().Be(row.GetProperty("tile2_image_key").GetString());
        await Assertions.Expect(Editor.ButtonLink(1)).ToHaveValueAsync("/products");
    }

    [Fact]
    public async Task AC12_CancelAddAndEditDiscardsTextImageReplacementRemovalAndStatus()
    {
        await OpenEmptyAsync();
        using var data = new FeaturedData();
        var png = await Editor.ArtworkAsync();
        await Editor.FillValidAsync("Discard Add", png);
        await Editor.Cancel.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        (await data.RowsAsync()).GetArrayLength().Should().Be(0);
        await data.SeedAsync(png);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
        await Editor.Title.FillAsync("Discard Edit");
        await Editor.SelectValidAsync(1, png);
        await Editor.Image(2).Locator(".shop-image-tile-remove").ClickAsync();
        await Editor.Status(true).ClickAsync();
        await Editor.Cancel.ClickAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
        await Assertions.Expect(Editor.Image(1).Locator("img")).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Image(2).Locator("img")).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Status(false)).ToHaveAttributeAsync("aria-pressed", "true");
        await AssertSavedAsync(data);
    }

    [Fact]
    public async Task AC13_TwoCreatorsAndRepeatedSaveNeverDuplicateAndConflictRequiresReload()
    {
        await OpenEmptyAsync();
        using var data = new FeaturedData();
        var png = await Editor.ArtworkAsync();
        var secondPage = await Context.NewPageAsync();
        var second = new ManageFeaturedPage(secondPage);
        try
        {
            await second.GotoAsync();
            await Assertions.Expect(second.Root).ToHaveAttributeAsync("data-state", "add");
            await Editor.FillValidAsync(data.Title, png);
            await second.FillValidAsync("Stale creator", png);
            var saves = 0;
            Page.Request += (_, request) => { if (request.Url.EndsWith("/rpc/save_featured_admin")) saves++; };
            await Editor.Root.Locator("form").EvaluateAsync("form=>{form.requestSubmit();form.requestSubmit();}");
            await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
            saves.Should().Be(1);
            await second.Save.ClickAsync();
            await Assertions.Expect(second.Snackbar).ToContainTextAsync(Strings.Featured_AlreadyExists);
            await Assertions.Expect(second.Save).ToBeDisabledAsync();
            await Assertions.Expect(second.Title).ToHaveValueAsync("Stale creator");
            (await data.RowsAsync()).GetArrayLength().Should().Be(1);
            await second.Reload.ClickAsync();
            await Assertions.Expect(second.Title).ToHaveValueAsync(data.Title);
            await Editor.GotoAsync();
            await Editor.Title.FillAsync("Alice edit");
            await Editor.SaveAndReturnAsync();
            await second.Title.FillAsync("Bob older draft");
            await second.SaveAndReturnAsync();
            await Editor.GotoAsync();
            await Assertions.Expect(Editor.Title).ToHaveValueAsync("Bob older draft");
        }
        finally { await secondPage.CloseAsync(); }
    }

    [Fact]
    public async Task AC14_ResponsiveLoadingSkeletonAndFailedReadRetryNeverExposeFalseAdd()
    {
        using var data = new FeaturedData();
        await data.SeedAsync(await Editor.ArtworkAsync());
        foreach (var width in new[] { 1440, 834, 390 })
        {
            await Page.SetViewportSizeAsync(width, 900);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task Hold(IRoute route) { await release.Task; await route.ContinueAsync(); }
            await Context.RouteAsync("**/rest/v1/rpc/get_featured_admin", Hold);
            try
            {
                await Editor.GotoAsync();
                await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "loading");
                await Assertions.Expect(Editor.Root.Locator("[aria-busy=true]")).ToBeVisibleAsync();
                await Assertions.Expect(Editor.Root.Locator(".shop-featured-skeleton-image")).ToHaveCountAsync(2);
                await Assertions.Expect(Editor.Save).ToHaveCountAsync(0);
                await Assertions.Expect(Editor.Title).ToHaveCountAsync(0);
            }
            finally { release.TrySetResult(); await Context.UnrouteAsync("**/rest/v1/rpc/get_featured_admin", Hold); }
            await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
        }
        async Task Fail(IRoute route) => await route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{}" });
        await Context.RouteAsync("**/rest/v1/rpc/get_featured_admin", Fail);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "load-error");
        await Assertions.Expect(Editor.Root.GetByRole(AriaRole.Alert)).ToContainTextAsync(Strings.Featured_LoadFailed);
        await Assertions.Expect(Editor.Save).ToHaveCountAsync(0);
        await Context.UnrouteAsync("**/rest/v1/rpc/get_featured_admin", Fail);
        await Editor.Reload.ClickAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
    }

    [Fact]
    public async Task AC15_SecondUploadFailurePermissionDenialAndUncertainSaveRetainDraftAndSavedRow()
    {
        using var data = new FeaturedData();
        var png = await Editor.ArtworkAsync();
        await data.SeedAsync(png);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
        await Editor.Title.FillAsync("Retained draft");
        await Editor.SelectValidAsync(1, png);
        await Editor.SelectValidAsync(2, png);
        var sources = new[] { await SourceAsync(Editor.Image(1)), await SourceAsync(Editor.Image(2)) };
        var uploads = 0;
        async Task FailSecond(IRoute route)
        {
            if (route.Request.Method == "POST" && ++uploads == 2)
                await route.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{}" });
            else await route.ContinueAsync();
        }
        await Context.RouteAsync("**/storage/v1/object/featured-images/**", FailSecond);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Snackbar.Filter(new() { HasText = Strings.Featured_UploadFailed })).ToBeVisibleAsync();
        await Context.UnrouteAsync("**/storage/v1/object/featured-images/**", FailSecond);
        await AssertSavedAsync(data);
        async Task Deny(IRoute route) => await route.FulfillAsync(new() { Status = 403, ContentType = "application/json", Body = "{\"code\":\"42501\",\"message\":\"access denied\"}" });
        await Context.RouteAsync("**/rest/v1/rpc/get_featured_admin", Deny);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Snackbar.Filter(new() { HasText = Strings.Rbac_AccessDenied })).ToBeVisibleAsync();
        await Context.UnrouteAsync("**/rest/v1/rpc/get_featured_admin", Deny);
        async Task LoseResponse(IRoute route) => await route.AbortAsync();
        await Context.RouteAsync("**/rest/v1/rpc/save_featured_admin", LoseResponse);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Snackbar.Filter(new() { HasText = Strings.Featured_SaveUnconfirmed })).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Save).ToBeDisabledAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync("Retained draft");
        for (var number = 1; number <= 2; number++)
            await Assertions.Expect(Editor.Image(number).Locator("img")).ToHaveAttributeAsync("src", sources[number - 1]);
        await Assertions.Expect(Editor.Status(false)).ToHaveAttributeAsync("aria-pressed", "true");
        await AssertSavedAsync(data);
        await Context.UnrouteAsync("**/rest/v1/rpc/save_featured_admin", LoseResponse);
        await Editor.Reload.ClickAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(data.Title);
    }

    [Fact]
    public async Task AC16_TallSettingsScrollThenStopWith24PixelsClearanceAndReleaseWithinEditor()
    {
        await OpenEmptyAsync(1920, 350);
        await Editor.WaitForStickyOffsetAsync();
        var initial = (await Editor.Settings.BoundingBoxAsync())!;
        var viewport = (await Editor.Scroll.BoundingBoxAsync())!;
        initial.Height.Should().BeGreaterThan(viewport.Height);
        var threshold = initial.Y - viewport.Y - Math.Min(0, viewport.Height - initial.Height);
        await Editor.ScrollToAsync((int)(threshold / 2));
        (await Editor.Settings.BoundingBoxAsync())!.Y.Should().BeLessThan(initial.Y);
        await Editor.ScrollToAsync((int)threshold + 30);
        var stopped = (await Editor.Settings.BoundingBoxAsync())!;
        var left = (await Editor.Tile(1).BoundingBoxAsync())!;
        await Editor.ScrollToAsync((int)threshold + 100);
        (await Editor.Settings.BoundingBoxAsync())!.Y.Should().BeApproximately(stopped.Y, 2);
        (await Editor.Tile(1).BoundingBoxAsync())!.Y.Should().BeLessThan(left.Y);
        var lastAction = (await Editor.Cancel.BoundingBoxAsync())!;
        (viewport.Y + viewport.Height - lastAction.Y - lastAction.Height).Should().BeApproximately(24, 2);
        await Editor.ScrollToAsync(100000);
        var root = (await Editor.Root.BoundingBoxAsync())!;
        var released = (await Editor.Settings.BoundingBoxAsync())!;
        (released.Y + released.Height).Should().BeLessThanOrEqualTo(root.Y + root.Height + 1);
        await Editor.AssertHealthyAsync();
    }

    [Fact]
    public async Task AC17_ShortSettingsStayBelowHeaderWithAccessibleActionsAndEditorContainment()
    {
        await OpenEmptyAsync(1920, 900);
        await Editor.WaitForStickyOffsetAsync();
        var initial = (await Editor.Settings.BoundingBoxAsync())!;
        var viewport = (await Editor.Scroll.BoundingBoxAsync())!;
        initial.Height.Should().BeLessThan(viewport.Height);
        await Editor.ScrollToAsync((int)(initial.Y - viewport.Y) + 60);
        (await Editor.Settings.BoundingBoxAsync())!.Y.Should().BeApproximately(viewport.Y, 2);
        await Assertions.Expect(Editor.Save).ToBeInViewportAsync();
        await Assertions.Expect(Editor.Cancel).ToBeInViewportAsync();
        await Editor.ScrollToAsync(100000);
        var panel = (await Editor.Settings.BoundingBoxAsync())!;
        var root = (await Editor.Root.BoundingBoxAsync())!;
        (panel.Y + panel.Height).Should().BeLessThanOrEqualTo(root.Y + root.Height + 1);
    }

    [Fact]
    public async Task AC18_ReverseScrollErrorsResizeTextZoomAndFocusKeepPanelReachableAndDraftIntact()
    {
        await OpenEmptyAsync(1920, 350);
        await Editor.TileTitle(1).FillAsync("Retained first tile");
        await Editor.ScrollToAsync(300);
        await Editor.WaitForStickyOffsetAsync();
        await Editor.ScrollToAsync(0);
        await Editor.Title.FocusAsync();
        await Assertions.Expect(Editor.Title).ToBeInViewportAsync();
        var before = (await Editor.Settings.BoundingBoxAsync())!.Height;
        await Editor.Title.FillAsync(" ");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Title).ToHaveAttributeAsync("aria-invalid", "true");
        (await Editor.Settings.BoundingBoxAsync())!.Height.Should().BeGreaterThan(before);
        await Editor.WaitForStickyOffsetAsync();
        await Page.SetViewportSizeAsync(1920, 1000);
        await Editor.WaitForStickyOffsetAsync();
        await Assertions.Expect(Editor.Settings).ToHaveCSSAsync("top", "0px");
        await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
        await Editor.WaitForStickyOffsetAsync();
        await Editor.Title.FocusAsync();
        await Assertions.Expect(Editor.Title).ToBeInViewportAsync();
        await Editor.Cancel.FocusAsync();
        await Assertions.Expect(Editor.Cancel).ToBeInViewportAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(" ");
        await Assertions.Expect(Editor.TileTitle(1)).ToHaveValueAsync("Retained first tile");
        await Page.SetViewportSizeAsync(834, 900);
        await Assertions.Expect(Editor.Settings).ToHaveCSSAsync("position", "static");
        await Assertions.Expect(Editor.TileTitle(1)).ToHaveValueAsync("Retained first tile");
    }

    [Fact]
    public async Task AC19_TabletAndPhoneUseNormalScrollingAndPreserveDraftAcrossLayouts()
    {
        await OpenEmptyAsync(834, 1100);
        await Editor.Title.FillAsync("Responsive draft");
        await Editor.TileTitle(2).FillAsync("Second draft");
        foreach (var width in new[] { 834, 390, 834 })
        {
            await Page.SetViewportSizeAsync(width, 900);
            await Editor.ScrollToAsync(0);
            var first = (await Editor.Tile(1).BoundingBoxAsync())!;
            var second = (await Editor.Tile(2).BoundingBoxAsync())!;
            var settings = (await Editor.Settings.BoundingBoxAsync())!;
            if (width >= 768) { second.X.Should().BeGreaterThan(first.X); second.Y.Should().BeApproximately(first.Y, 2); }
            else { second.Y.Should().BeGreaterThanOrEqualTo(first.Y + first.Height); second.X.Should().BeApproximately(first.X, 2); }
            settings.Y.Should().BeGreaterThanOrEqualTo(second.Y + second.Height);
            await Assertions.Expect(Editor.Settings).ToHaveCSSAsync("position", "static");
            await Editor.ScrollToAsync(100);
            (await Editor.Settings.BoundingBoxAsync())!.Y.Should().BeApproximately(settings.Y - 100, 2);
            await Assertions.Expect(Editor.Title).ToHaveValueAsync("Responsive draft");
            await Assertions.Expect(Editor.TileTitle(2)).ToHaveValueAsync("Second draft");
        }
    }

    [Fact]
    public async Task AC20_KeyboardLabelsErrorsImageActionsAndEnglishControlsRemainUsableWithoutOverflow()
    {
        await OpenEmptyAsync();
        var png = await Editor.ArtworkAsync();
        foreach (var width in new[] { 1440, 834, 390 })
        {
            await Page.SetViewportSizeAsync(width, 900);
            await Editor.Title.FocusAsync();
            await Assertions.Expect(Editor.Title).ToBeFocusedAsync();
            var focus = await Editor.Title.EvaluateAsync<bool>("el=>{const control=el.closest('.shop-field-control');return el.matches(':focus') && getComputedStyle(control).boxShadow.includes(getComputedStyle(control).getPropertyValue('--shop-focus-width').trim());}");
            focus.Should().BeTrue();
            await Page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(Editor.Status(true)).ToBeFocusedAsync();
            await Page.Keyboard.PressAsync("Space");
            await Assertions.Expect(Editor.Status(true)).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(Editor.Save).ToHaveAccessibleNameAsync(Strings.Save);
            await Assertions.Expect(Editor.Cancel).ToHaveAccessibleNameAsync(Strings.Cancel);
            for (var number = 1; number <= 2; number++)
            {
                await Assertions.Expect(Editor.TileTitle(number)).ToHaveAccessibleNameAsync(Strings.Featured_Title);
                await Assertions.Expect(Editor.ButtonLink(number)).ToHaveAccessibleNameAsync(Strings.Featured_ButtonLink);
            }
            (await Page.EvaluateAsync<bool>("()=>document.documentElement.scrollWidth<=innerWidth && document.querySelector('main[data-shop-scroll-root]').scrollWidth<=document.querySelector('main[data-shop-scroll-root]').clientWidth+1")).Should().BeTrue();
        }
        await Editor.Image(1).Locator("[data-file-picker]").FocusAsync();
        var chooser = await Page.RunAndWaitForFileChooserAsync(() => Page.Keyboard.PressAsync("Enter"));
        await chooser.SetFilesAsync(new FilePayload { Name = "keyboard.png", MimeType = "image/png", Buffer = png });
        await Assertions.Expect(Editor.Image(1).Locator("img")).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Image(1).Locator(".shop-image-tile-remove")).ToHaveAccessibleNameAsync(new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape(string.Format(Strings.Featured_RemoveImage, 1))));
        await Editor.Title.FillAsync("");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Title).ToHaveAttributeAsync("aria-invalid", "true");
        var description = await Editor.Title.GetAttributeAsync("aria-describedby");
        description.Should().NotBeNullOrWhiteSpace();
        await Assertions.Expect(Editor.Root).ToContainTextAsync(Strings.Featured_SectionTitleRequired);
        await Editor.AssertHealthyAsync();
    }
}
