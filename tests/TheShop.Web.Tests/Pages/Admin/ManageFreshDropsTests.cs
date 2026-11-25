using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFreshDrops;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsForAdmin;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

public sealed class ManageFreshDropsTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public ManageFreshDropsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(_ => true).SetVoidResult();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_user);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);
        Services.AddSingleton<BusyState>();
        Services.AddSingleton<BreadcrumbState>();
        _localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(true);
        _mediator.Send(Arg.Any<GetFreshDropsForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<FreshDropsDto?>(null));
        _mediator.Send(Arg.Any<GetFreshDropsProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<FreshDropsProductDto>>([]));
    }

    private void CanCreate() => _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
    private void CanEdit() => _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(true);
    private static FreshDropsDto Existing(FreshDropsSelectionMode mode = FreshDropsSelectionMode.Newest,
        IReadOnlyList<Guid>? ids = null) => new("Saved drops", null, null, mode, ids ?? [], false);

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Render_AbsentCreator_ShowsNewestAndFigmaDefaults()
    {
        CanCreate();
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-editor]").GetAttribute("data-state").Should().Be("add"));
        cut.Find("h1").TextContent.Should().Be("Add Fresh Drops");
        cut.Find("[data-testid=fresh-drops-newest]").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("[data-testid=fresh-drops-title]").GetAttribute("value").Should().Be("Fresh Drops");
        cut.Find("[data-testid=fresh-drops-button-label]").GetAttribute("value").Should().Be("View All Products");
        cut.Find("[data-testid=fresh-drops-button-link]").GetAttribute("value").Should().Be("/products");
        cut.Find("[data-testid=fresh-drops-automatic-list] caption").Should().NotBeNull();
        cut.Find("[data-testid=fresh-drops-save]").Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Render_EditAndTitleChange_UpdatesHeadingAndManualTable()
    {
        CanEdit();
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetFreshDropsForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<FreshDropsDto?>(Existing(FreshDropsSelectionMode.Manual, [id])));
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-editor]").GetAttribute("data-state").Should().Be("edit"));
        cut.Find("h1").TextContent.Should().Be("Edit Saved drops");
        cut.Find("[data-testid=fresh-drops-manual-list] tbody tr").Should().NotBeNull();
        cut.Find("[data-testid=fresh-drops-manual-list] thead").TextContent.Should().Contain(Strings.FreshDrops_Actions);
        cut.Find("[data-testid=fresh-drops-title]").Input("Changed title");
        cut.Find("h1").TextContent.Should().Be("Edit Changed title");
        cut.Find("[data-testid=fresh-drops-title]").Input(" ");
        cut.Find("h1").TextContent.Should().Be("Edit Fresh Drops");
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Render_ViewOnly_HidesMutationControls()
    {
        _mediator.Send(Arg.Any<GetFreshDropsForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<FreshDropsDto?>(Existing()));
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-editor]").GetAttribute("data-state").Should().Be("edit"));
        cut.FindAll("[data-testid=fresh-drops-save], [data-testid=fresh-drops-open-picker]").Should().BeEmpty();
        cut.Find("[data-testid=fresh-drops-title]").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Render_LoadFailure_ShowsRetryWithoutAddForm()
    {
        CanCreate();
        _mediator.Send(Arg.Any<GetFreshDropsForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<FreshDropsDto?>(FreshDropsErrorKeys.LoadFailed));
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-editor]").GetAttribute("data-state").Should().Be("load-error"));
        cut.Find("[data-testid=fresh-drops-reload]").Should().NotBeNull();
        cut.FindAll("[data-testid=fresh-drops-save]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Mode_UnsavedSwitch_RestoresManualChoices()
    {
        CanEdit();
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetFreshDropsForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<FreshDropsDto?>(Existing(FreshDropsSelectionMode.Manual, [id])));
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-manual-list] tbody tr").Should().NotBeNull());
        await cut.Find("[data-testid=fresh-drops-newest]").ClickAsync(new());
        cut.FindAll("[data-testid=fresh-drops-manual-list]").Should().BeEmpty();
        await cut.Find("[data-testid=fresh-drops-manual]").ClickAsync(new());
        cut.FindAll("[data-testid=fresh-drops-manual-list] tbody tr").Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Manual_AddProduct_OpensDialogWithoutRenderingError()
    {
        CanCreate();
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-editor]").GetAttribute("data-state").Should().Be("add"));
        await cut.Find("[data-testid=fresh-drops-manual]").ClickAsync(new());
        await cut.Find("[data-testid=fresh-drops-open-picker]").ClickAsync(new());
        cut.Find("[data-testid=fresh-drops-product-dialog]").Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Manual_MoveAndRemove_SendsRemainingIdsInNewOrder()
    {
        CanEdit();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetFreshDropsForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<FreshDropsDto?>(Existing(FreshDropsSelectionMode.Manual, [first, second, third])));
        _mediator.Send(Arg.Any<TheShop.Application.Features.Homepage.Commands.UpdateFreshDrops.UpdateFreshDropsCommand>(),
            Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=fresh-drops-manual-list] tbody tr").Should().HaveCount(3));
        await cut.FindAll("[data-testid=fresh-drops-manual-list] tbody tr")[0]
            .QuerySelectorAll("button")[1].ClickAsync(new());
        await cut.FindAll("[data-testid=fresh-drops-manual-list] tbody tr")[2]
            .QuerySelectorAll("button")[2].ClickAsync(new());
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<TheShop.Application.Features.Homepage.Commands.UpdateFreshDrops.UpdateFreshDropsCommand>(command =>
            command.ManualProductIds.SequenceEqual(new[] { second, first })), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Cancel_UnsavedAdd_IsNavigationWithoutSave()
    {
        CanCreate();
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-save]").Should().NotBeNull());
        cut.Find("a[href='/admin/homepage']").Should().NotBeNull();
        _mediator.DidNotReceive().Send(Arg.Any<CreateFreshDropsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Save_ValidAdd_DispatchesCreateAndNavigates()
    {
        CanCreate();
        _mediator.Send(Arg.Any<CreateFreshDropsCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var cut = Render<ManageFreshDrops>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=fresh-drops-save]").Should().NotBeNull());
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<CreateFreshDropsCommand>(command =>
            command.Title == "Fresh Drops" && command.ButtonLabel == "View All Products" &&
            command.ButtonLink == "/products" && command.SelectionMode == FreshDropsSelectionMode.Newest &&
            command.IsVisible), Arg.Any<CancellationToken>());
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Admin.ManageHomepage);
    }
}
