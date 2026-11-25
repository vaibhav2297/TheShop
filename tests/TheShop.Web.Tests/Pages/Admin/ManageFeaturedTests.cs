using System.Reflection;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFeatured;
using TheShop.Application.Features.Homepage.Commands.UpdateFeatured;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetFeaturedForAdmin;
using TheShop.Application.Features.Homepage.Queries.ValidateFeaturedImage;
using TheShop.Application.Features.Roles;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

public sealed class ManageFeaturedTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly BunitJSModuleInterop _scroll;
    private static FeaturedDto Existing() => new("Saved section",
        new("Label One", "First", "Shop One", "/one", new("key-one", "https://example.test/one.png")),
        new(null, "Second", null, null, new("key-two", "https://example.test/two.png")), false);

    public ManageFeaturedTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(_ => true).SetVoidResult();
        _scroll = JSInterop.SetupModule("./js/featuredEditor.js").SetupModule("mount", _ => true);
        _scroll.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/shopImageUpload.js").Setup<string>("createObjectUrl", _ => true).SetResult("blob:featured");
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_user);
        Services.AddSingleton(_notifications);
        Services.AddSingleton<BusyState>();
        Services.AddSingleton<BreadcrumbState>();
        var localizer = Substitute.For<IStringLocalizer<Strings>>();
        localizer[Arg.Any<string>()].Returns(c => new LocalizedString(c.Arg<string>(), c.Arg<string>()));
        Services.AddSingleton(localizer);
        _mediator.Send(Arg.Any<GetFeaturedForAdminQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok<FeaturedDto?>(null));
        _mediator.Send(Arg.Any<ValidateFeaturedImageQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(true);
    }
    private IRenderedComponent<ManageFeatured> Add()
    {
        _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
        return Ready("add");
    }
    private IRenderedComponent<ManageFeatured> Edit()
    {
        _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(true);
        _mediator.Send(Arg.Any<GetFeaturedForAdminQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok<FeaturedDto?>(Existing()));
        return Ready("edit");
    }
    private IRenderedComponent<ManageFeatured> Ready(string state)
    {
        var cut = Render<ManageFeatured>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=featured-admin]").GetAttribute("data-state").Should().Be(state));
        return cut;
    }
    private static IBrowserFile File(string name = "new.png")
    {
        var file = Substitute.For<IBrowserFile>();
        file.Name.Returns(name); file.ContentType.Returns("image/png"); file.Size.Returns(3);
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream([1, 2, 3]));
        return file;
    }
    private static IRenderedComponent<ShopImageUpload> Upload(IRenderedComponent<ManageFeatured> cut, int index) =>
        cut.FindComponents<FeaturedTileEditor>()[index].FindComponent<ShopImageUpload>();
    private string Uri => Services.GetRequiredService<NavigationManager>().Uri;

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Add_DefaultsToFeaturedVisibleAndExactlyTwoDistinctAccessibleTiles()
    {
        var cut = Add();
        cut.Find("h1").TextContent.Should().Be(string.Format(Strings.Featured_AddHeading, Strings.Homepage_Featured));
        cut.Find("#featured-title").GetAttribute("value").Should().Be(Strings.Homepage_Featured);
        cut.Find("[data-testid=featured-visible]").GetAttribute("aria-pressed").Should().Be("true");
        cut.FindComponents<FeaturedTileEditor>().Should().HaveCount(2);
        var ids = cut.FindAll("input[id]").Select(x => x.Id).ToArray();
        ids.Should().OnlyHaveUniqueItems();
        foreach (var input in cut.FindAll("input[id]").Where(x => x.GetAttribute("type") != "file"))
            cut.Find($"label[for='{input.Id}']").TextContent.Should().NotBeNullOrWhiteSpace();
        foreach (var uploader in cut.FindComponents<ShopImageUpload>())
        {
            uploader.Instance.PreviewPreset.Should().Be(ShopImagePreset.SquareContain);
            uploader.Instance.Layout.Should().Be(ShopImageUploadLayout.SinglePreview);
            uploader.Instance.MaxFileSize.Should().Be(5_242_880);
            uploader.Instance.ResponsivePreview.Should().BeFalse();
        }
        Services.GetRequiredService<BreadcrumbState>().Trail.Should().HaveCount(3);
        cut.Markup.Should().Contain(Strings.Featured_ImageHint);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Heading_UsesTrimmedLiveTitleAndBlankFallbackWithoutChangingInput()
    {
        var cut = Add();
        cut.Find("#featured-title").Input(" Summer Picks ");
        cut.Find("h1").TextContent.Should().Be(string.Format(Strings.Featured_AddHeading, "Summer Picks"));
        cut.Find("#featured-title").Input("   ");
        cut.Find("h1").TextContent.Should().Be(string.Format(Strings.Featured_AddHeading, Strings.Homepage_Featured));
        cut.Find("#featured-title").GetAttribute("value").Should().Be("   ");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Edit_HydratesEveryFieldAndHiddenStatusAndUsesLiveEditHeading()
    {
        var cut = Edit();
        cut.Find("#featured-title").GetAttribute("value").Should().Be("Saved section");
        cut.Find("#featured-tile-1-label").GetAttribute("value").Should().Be("Label One");
        cut.Find("#featured-tile-1-title").GetAttribute("value").Should().Be("First");
        cut.Find("#featured-tile-1-button-label").GetAttribute("value").Should().Be("Shop One");
        cut.Find("#featured-tile-1-button-link").GetAttribute("value").Should().Be("/one");
        cut.Find("#featured-tile-2-title").GetAttribute("value").Should().Be("Second");
        cut.Find("[data-testid=featured-hidden]").GetAttribute("aria-pressed").Should().Be("true");
        Upload(cut, 0).Instance.Files.Single().PreviewUrl.Should().Be(Existing().Tile1.Image.PreviewUrl);
        Upload(cut, 1).Instance.Files.Single().PreviewUrl.Should().Be(Existing().Tile2.Image.PreviewUrl);
        cut.Find("#featured-title").Input("Changed");
        cut.Find("h1").TextContent.Should().Be(string.Format(Strings.Featured_EditHeading, "Changed"));
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void ViewOnly_SavedContentHasNoMutationControlsAndAbsentHasNoAddForm()
    {
        var absent = Ready("absent");
        absent.Markup.Should().Contain(Strings.Featured_Absent);
        absent.FindAll("form").Should().BeEmpty();
        _mediator.Send(Arg.Any<GetFeaturedForAdminQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok<FeaturedDto?>(Existing()));
        var saved = Ready("edit");
        saved.Markup.Should().Contain("Saved section").And.Contain("First").And.Contain("Second");
        saved.FindAll("input").Should().BeEmpty();
        saved.FindAll("[data-testid=featured-save], [data-testid=featured-cancel]").Should().BeEmpty();
        saved.FindComponents<FeaturedTileEditor>().Should().OnlyContain(x => x.Instance.ReadOnly);
        typeof(ManageFeatured).GetCustomAttribute<AuthorizePermissionAttribute>()!.Policy.Should().Be("perm:homepage.view");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Loading_ShowsSkeletonAndNoEditableAddUntilReadCompletes()
    {
        _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
        var pending = new TaskCompletionSource<Result<FeaturedDto?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediator.Send(Arg.Any<GetFeaturedForAdminQuery>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = Ready("loading");
        cut.Find("[aria-busy=true]").Should().NotBeNull();
        cut.FindAll("form, [data-testid=featured-save]").Should().BeEmpty();
        pending.SetResult(Result.Ok<FeaturedDto?>(null));
        cut.WaitForAssertion(() => cut.Find("[data-testid=featured-save]").Should().NotBeNull());
        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task LoadFailure_ShowsLocalizedAlertAndRetryRestoresSavedEdit()
    {
        _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(true);
        _mediator.Send(Arg.Any<GetFeaturedForAdminQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Fail<FeaturedDto?>(FeaturedErrorKeys.LoadFailed));
        var cut = Ready("load-error");
        cut.Find("[role=alert]").TextContent.Should().Contain(FeaturedErrorKeys.LoadFailed);
        cut.FindAll("form").Should().BeEmpty();
        _mediator.Send(Arg.Any<GetFeaturedForAdminQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok<FeaturedDto?>(Existing()));
        await cut.Find("[data-testid=featured-reload]").ClickAsync(new());
        cut.Find("#featured-title").GetAttribute("value").Should().Be("Saved section");
        cut.Find("[data-testid=featured-admin]").GetAttribute("data-state").Should().Be("edit");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task ImageReplacementAndRemovalOnlyChangeTheSelectedTile()
    {
        var cut = Edit();
        var other = Upload(cut, 1).Instance.Files.Single();
        await Upload(cut, 0).InvokeAsync(() => Upload(cut, 0).Instance.AddFilesAsync([File()]));
        Upload(cut, 0).Instance.Files.Single().IsExisting.Should().BeFalse();
        Upload(cut, 1).Instance.Files.Single().Should().BeSameAs(other);
        await cut.Find("[data-testid=featured-tile-1] .shop-image-tile-remove").ClickAsync(new());
        Upload(cut, 0).Instance.Files.Should().BeEmpty();
        Upload(cut, 1).Instance.Files.Single().Should().BeSameAs(other);
        await Upload(cut, 1).InvokeAsync(() => Upload(cut, 1).Instance.AddFilesAsync([]));
        Upload(cut, 1).Instance.Files.Single().Should().BeSameAs(other);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task RejectedImagePreservesPriorPreviewAndDisplaysLocalizedError()
    {
        var cut = Edit();
        var saved = Upload(cut, 0).Instance.Files.Single();
        _mediator.Send(Arg.Any<ValidateFeaturedImageQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Fail(FeaturedErrorKeys.ImageDimensionsInvalid));
        await Upload(cut, 0).InvokeAsync(() => Upload(cut, 0).Instance.AddFilesAsync([File()]));
        Upload(cut, 0).Instance.Files.Single().Should().BeSameAs(saved);
        cut.Find("[data-testid=featured-tile-1]").TextContent.Should().Contain(FeaturedErrorKeys.ImageDimensionsInvalid);
        Upload(cut, 1).Instance.Files.Single().PreviewUrl.Should().Be(Existing().Tile2.Image.PreviewUrl);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Save_WaitsForAcceptedImageAndCommitBeforeSuccessAndSuppressesDuplicateSubmit()
    {
        var cut = Edit();
        var validation = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commit = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediator.Send(Arg.Any<ValidateFeaturedImageQuery>(), Arg.Any<CancellationToken>()).Returns(validation.Task);
        _mediator.Send(Arg.Any<UpdateFeaturedCommand>(), Arg.Any<CancellationToken>()).Returns(commit.Task);
        var selection = Upload(cut, 0).InvokeAsync(() => Upload(cut, 0).Instance.AddFilesAsync([File()]));
        cut.WaitForAssertion(() => Upload(cut, 0).Find("[data-upload-disabled=true]").Should().NotBeNull());
        var submit = cut.Find("form").SubmitAsync();
        await _mediator.DidNotReceive().Send(Arg.Any<UpdateFeaturedCommand>(), Arg.Any<CancellationToken>());
        await cut.Find("form").SubmitAsync();
        validation.SetResult(Result.Ok());
        await selection;
        cut.WaitForAssertion(() => _mediator.Received(1).Send(Arg.Is<UpdateFeaturedCommand>(x => x.Tile1.NewImage != null &&
            x.Tile1.NewImage.Content.SequenceEqual(new byte[] { 1, 2, 3 }) && x.Tile2.NewImage == null), Arg.Any<CancellationToken>()));
        _notifications.DidNotReceiveWithAnyArgs().Show(default!, default);
        Uri.Should().NotEndWith(Routes.Admin.ManageHomepage);
        commit.SetResult(Result.Ok());
        await submit;
        _notifications.Received(1).Show(Strings.Featured_Saved, ShopNotificationKind.Success);
        Uri.Should().EndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Save_AddDispatchesBothDraftTilesAndSelectedVisibility()
    {
        var cut = Add();
        _mediator.Send(Arg.Any<CreateFeaturedCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        cut.Find("#featured-title").Input("Summer");
        cut.Find("#featured-tile-1-title").Input("First draft");
        cut.Find("#featured-tile-2-title").Input("Second draft");
        await Upload(cut, 0).InvokeAsync(() => Upload(cut, 0).Instance.AddFilesAsync([File("one.png")]));
        await Upload(cut, 1).InvokeAsync(() => Upload(cut, 1).Instance.AddFilesAsync([File("two.png")]));
        await cut.Find("[data-testid=featured-hidden]").ClickAsync(new());
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<CreateFeaturedCommand>(x => x.Title == "Summer" &&
            x.Tile1.Title == "First draft" && x.Tile1.NewImage!.FileName == "one.png" &&
            x.Tile2.Title == "Second draft" && x.Tile2.NewImage!.FileName == "two.png" && !x.IsVisible), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task FieldErrors_MapToNestedImageAndTextFieldsAndClearOnlyChangedField()
    {
        var cut = Add();
        _mediator.Send(Arg.Any<CreateFeaturedCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Fail(FeaturedErrorKeys.SectionTitleRequired, [],
            [new("Title", FeaturedErrorKeys.SectionTitleRequired), new("Tile1.NewImage", FeaturedErrorKeys.ImageRequired),
             new("Tile2.ButtonLink", FeaturedErrorKeys.ButtonPairRequired)]));
        cut.Find("#featured-title").Input(" ");
        await cut.Find("form").SubmitAsync();
        cut.Find("#featured-title").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("#featured-tile-1-image-error").TextContent.Should().Contain(FeaturedErrorKeys.ImageRequired);
        cut.Find("#featured-tile-2-button-link").GetAttribute("aria-invalid").Should().Be("true");
        cut.Find("#featured-title").Input("Fixed");
        cut.Find("#featured-title").GetAttribute("aria-invalid").Should().NotBe("true");
        cut.Find("#featured-tile-1-image-error").TextContent.Should().Contain(FeaturedErrorKeys.ImageRequired);
        await Upload(cut, 0).InvokeAsync(() => Upload(cut, 0).Instance.AddFilesAsync([File()]));
        cut.Find("#featured-tile-1-image-error").TextContent.Should().BeEmpty();
        cut.Find("#featured-tile-2-button-link").GetAttribute("aria-invalid").Should().Be("true");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task SaveFailureOrPermissionLossRetainsTextImagesAndStatusWithoutSuccess()
    {
        var cut = Edit();
        cut.Find("#featured-title").Input("Draft");
        await Upload(cut, 0).InvokeAsync(() => Upload(cut, 0).Instance.AddFilesAsync([File()]));
        foreach (var error in new[] { FeaturedErrorKeys.UploadFailed, FeaturedErrorKeys.SaveFailed, RbacErrorKeys.AccessDenied })
        {
            _mediator.Send(Arg.Any<UpdateFeaturedCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Fail(error));
            await cut.Find("form").SubmitAsync();
            cut.Find("#featured-title").GetAttribute("value").Should().Be("Draft");
            Upload(cut, 0).Instance.Files.Single().FileName.Should().Be("new.png");
            cut.Find("[data-testid=featured-hidden]").GetAttribute("aria-pressed").Should().Be("true");
            _notifications.Received().Show(error, ShopNotificationKind.Error);
        }
        _notifications.DidNotReceive().Show(Arg.Any<string>(), ShopNotificationKind.Success);
        Uri.Should().NotEndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task ConflictOrUnconfirmedSaveBlocksRetryUntilExplicitReload()
    {
        var cut = Edit();
        foreach (var error in new[] { FeaturedErrorKeys.AlreadyExists, FeaturedErrorKeys.NotFound, FeaturedErrorKeys.SaveUnconfirmed })
        {
            _mediator.ClearReceivedCalls();
            cut.Find("#featured-title").Input("Draft");
            _mediator.Send(Arg.Any<UpdateFeaturedCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Fail(error));
            await cut.Find("form").SubmitAsync();
            cut.Find("[data-testid=featured-save]").HasAttribute("disabled").Should().BeTrue();
            await cut.Find("form").SubmitAsync();
            await _mediator.Received(1).Send(Arg.Any<UpdateFeaturedCommand>(), Arg.Any<CancellationToken>());
            cut.Find("#featured-title").GetAttribute("value").Should().Be("Draft");
            await cut.Find("[data-testid=featured-reload]").ClickAsync(new());
            cut.Find("#featured-title").GetAttribute("value").Should().Be("Saved section");
            cut.Find("[data-testid=featured-save]").HasAttribute("disabled").Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Cancel_AddAndEditProvideHomepageNavigationWithoutSaveOrUploadCommand()
    {
        var add = Add();
        add.Find("#featured-title").Input("Unsaved Add");
        add.Find("[data-testid=featured-cancel]").GetAttribute("href").Should().Be(Routes.Admin.ManageHomepage);
        var edit = Edit();
        edit.Find("#featured-title").Input("Unsaved Edit");
        edit.Find("[data-testid=featured-cancel]").GetAttribute("href").Should().Be(Routes.Admin.ManageHomepage);
        await _mediator.DidNotReceive().Send(Arg.Any<CreateFeaturedCommand>(), Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<UpdateFeaturedCommand>(), Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<ValidateFeaturedImageQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task ScrollLifecycle_MountsOnceRefreshesAfterEditsAndDisposesOnNavigation()
    {
        var cut = Add();
        cut.Find("#featured-title").Input("Changed");
        JSInterop.Invocations["import"].Count(x => Equals(x.Arguments[0], "./js/featuredEditor.js")).Should().Be(1);
        _scroll.Invocations["refresh"].Should().NotBeEmpty();
        await DisposeComponentsAsync();
        _scroll.Invocations["dispose"].Should().ContainSingle();
    }
}
