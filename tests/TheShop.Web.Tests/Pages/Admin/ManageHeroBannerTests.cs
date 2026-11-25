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
using TheShop.Application.Features.Homepage.Commands.CreateHeroBanner;
using TheShop.Application.Features.Homepage.Commands.UpdateHeroBanner;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetHeroBannerForAdmin;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

public sealed class ManageHeroBannerTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public ManageHeroBannerTests()
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
        _mediator.Send(Arg.Any<GetHeroBannerForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HeroBannerDto?>(null));
        _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(true);
    }

    private void CanCreate() => _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
    private void CanEdit() => _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(true);
    private static HeroBannerDto Existing() => new("Saved heading", "Saved description", "Shop", "/products", false,
        new("hero/desktop.png", "https://example.com/desktop.png"),
        new("hero/mobile.png", "https://example.com/mobile.png"));

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Render_AbsentAndCreatePermission_ShowsAddFormWithVisibleDefault()
    {
        CanCreate();
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-banner-admin]").GetAttribute("data-state").Should().Be("add"));
        cut.Find("[data-testid=hero-save]").Should().NotBeNull();
        cut.Find("[data-testid=hero-cancel]").GetAttribute("href").Should().Be(Routes.Admin.ManageHomepage);
        cut.Find("[data-testid=hero-visible]").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Render_ExistingAndEditPermission_LoadsValuesAndHiddenStatus()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHeroBannerForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HeroBannerDto?>(Existing()));
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-banner-admin]").GetAttribute("data-state").Should().Be("edit"));
        cut.Find("[data-testid=hero-heading]").GetAttribute("value").Should().Be("Saved heading");
        cut.Find("[data-testid=hero-button-link]").GetAttribute("value").Should().Be("/products");
        cut.Find("[data-testid=hero-hidden]").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Render_ViewOnly_ShowsReadOnlyBannerWithoutSave()
    {
        _mediator.Send(Arg.Any<GetHeroBannerForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HeroBannerDto?>(Existing()));
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-banner-admin]").GetAttribute("data-state").Should().Be("edit"));
        cut.FindAll("[data-testid=hero-save]").Should().BeEmpty();
        cut.Markup.Should().Contain("Saved heading");
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Render_AbsentViewOnly_ShowsAbsentWithoutMutation()
    {
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-banner-admin]").GetAttribute("data-state").Should().Be("absent"));
        cut.Markup.Should().Contain(Strings.HeroBanner_Absent);
        cut.FindAll("[data-testid=hero-save]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Render_LoadFailure_ShowsRetryInsteadOfAdd()
    {
        CanCreate();
        _mediator.Send(Arg.Any<GetHeroBannerForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<HeroBannerDto?>(HeroBannerErrorKeys.LoadFailed));
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-banner-admin]").GetAttribute("data-state").Should().Be("load-error"));
        cut.Find("[role=alert]").Should().NotBeNull();
        cut.Find("[data-testid=hero-reload]").Should().NotBeNull();
        cut.FindAll("[data-testid=hero-save]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Save_Success_NotifiesAndNavigatesToManageHomepage()
    {
        CanCreate();
        _mediator.Send(Arg.Any<CreateHeroBannerCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-save]").Should().NotBeNull());
        cut.Find("[data-testid=hero-heading]").Input("New heading");
        await cut.Find("form").SubmitAsync();
        _notifications.Received(1).Show(Strings.HeroBanner_Saved, ShopNotificationKind.Success);
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Save_Edit_DispatchesUpdateWithoutReplacingSavedArtwork()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHeroBannerForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HeroBannerDto?>(Existing()));
        _mediator.Send(Arg.Any<UpdateHeroBannerCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-save]").Should().NotBeNull());
        cut.Find("[data-testid=hero-heading]").Input("Updated heading");
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<UpdateHeroBannerCommand>(command =>
            command.Heading == "Updated heading" && command.NewDesktopImage == null &&
            command.NewMobileImage == null && !command.RemoveDesktopImage &&
            !command.RemoveMobileImage && !command.IsVisible), Arg.Any<CancellationToken>());
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Save_Failure_StaysOnEditorWithDraft()
    {
        CanCreate();
        _mediator.Send(Arg.Any<CreateHeroBannerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(HeroBannerErrorKeys.UploadFailed));
        var cut = Render<ManageHeroBanner>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=hero-save]").Should().NotBeNull());
        cut.Find("[data-testid=hero-heading]").Input("Draft heading");
        await cut.Find("form").SubmitAsync();
        cut.Find("[data-testid=hero-heading]").GetAttribute("value").Should().Be("Draft heading");
        Services.GetRequiredService<NavigationManager>().Uri.Should().NotEndWith(Routes.Admin.ManageHomepage);
        _notifications.Received(1).Show(Arg.Any<string>(), ShopNotificationKind.Error);
    }
}
