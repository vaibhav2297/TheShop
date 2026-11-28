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
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageBrand;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandChoices;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandForAdmin;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

public sealed class ManageHomepageBrandTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private static readonly Guid[] Ids = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();

    public ManageHomepageBrandTests()
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
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageBrandDto?>(null));
        _mediator.Send(Arg.Any<GetHomepageBrandChoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<BrandDto>>([]));
    }

    private void CanCreate() => _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
    private void CanEdit() => _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(true);
    private static BrandDto Card(int index, bool active = true, string? logo = null) =>
        new(Ids[index], $"Brand {index}", null, logo, active);
    private static HomepageBrandDto Existing(bool visible = false, IReadOnlyList<Guid>? selected = null) =>
        new("Saved section", "Browse", "/saved", selected ?? [Ids[2], Ids[0], Ids[1]],
            [Card(0, logo: "https://shop.test/brand.svg"), Card(1), Card(2)], visible);

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Add_OpensWithEmptySelectionAndEditablePrefilledFields()
    {
        CanCreate();
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-editor]").GetAttribute("data-state").Should().Be("add"));
        cut.Find("h1").TextContent.Should().Be("Add Shop by Brand");
        cut.Find("[data-testid=homepage-brand-title]").GetAttribute("value").Should().Be("Shop by Brand");
        cut.Find("[data-testid=homepage-brand-button-label]").GetAttribute("value").Should().Be("View All Brands");
        cut.Find("[data-testid=homepage-brand-button-link]").GetAttribute("value").Should().Be("/brands");
        cut.Find("[data-testid=homepage-brand-editor]").TextContent.Should().Contain("0 of 6 selected");
        cut.FindAll("[data-testid=homepage-brand-selected] tbody tr strong").Should().ContainSingle()
            .Which.TextContent.Should().Be("No brands added yet");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Visible")
            .GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("[data-testid=homepage-brand-title]").Input("Custom title");
        cut.Find("h1").TextContent.Should().Be("Add Custom title");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Edit_RestoresSavedFieldsOrderLogoAndHiddenStatus()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageBrandDto?>(Existing()));
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-editor]").GetAttribute("data-state").Should().Be("edit"));
        cut.Find("[data-testid=homepage-brand-title]").GetAttribute("value").Should().Be("Saved section");
        cut.Find("[data-testid=homepage-brand-button-label]").GetAttribute("value").Should().Be("Browse");
        cut.Find("[data-testid=homepage-brand-button-link]").GetAttribute("value").Should().Be("/saved");
        var rows = cut.FindAll("[data-testid=homepage-brand-selected] tbody tr");
        rows.Select(row => row.QuerySelector("strong")!.TextContent).Should().Equal("Brand 2", "Brand 0", "Brand 1");
        rows[1].QuerySelector("img")!.GetAttribute("src").Should().Be("https://shop.test/brand.svg");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Hidden")
            .GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Picker_ApplyChangesDraftButDismissKeepsPreviousSelection()
    {
        CanCreate();
        _mediator.Send(Arg.Any<GetHomepageBrandChoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<BrandDto>>([Card(0), Card(1)]));
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-open-picker]").Should().NotBeNull());
        await cut.Find("[data-testid=homepage-brand-open-picker]").ClickAsync(new());
        await cut.FindAll(".shop-homepage-brand-choice-grid .shop-image-tile-surface")[0].ClickAsync(new());
        await cut.Find("[data-testid=homepage-brand-confirm-picker]").ClickAsync(new());
        cut.FindAll("[data-testid=homepage-brand-selected] tbody tr strong").Select(x => x.TextContent).Should().Equal("Brand 0");
        cut.Find("[data-testid=homepage-brand-editor]").TextContent.Should().Contain("1 of 6 selected");

        await cut.Find("[data-testid=homepage-brand-open-picker]").ClickAsync(new());
        await cut.FindAll(".shop-homepage-brand-choice-grid .shop-image-tile-surface")[1].ClickAsync(new());
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").ClickAsync(new());
        cut.FindAll("[data-testid=homepage-brand-selected] tbody tr strong").Select(x => x.TextContent).Should().Equal("Brand 0");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task FullSelection_DisablesAddUntilOneRemoved()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageBrandDto?>(Existing(selected: Ids.Take(6).ToArray())));
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=homepage-brand-selected] tbody tr").Should().HaveCount(6));
        cut.Find("[data-testid=homepage-brand-open-picker]").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid=homepage-brand-editor]").TextContent.Should().Contain("Remove a brand to add another.");
        await cut.FindAll("[data-testid=homepage-brand-selected] tbody tr")[0].QuerySelectorAll("button")[2].ClickAsync(new());
        cut.Find("[data-testid=homepage-brand-open-picker]").HasAttribute("disabled").Should().BeFalse();
        cut.Find("[data-testid=homepage-brand-editor]").TextContent.Should().Contain("5 of 6 selected");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Edit_MoveAndSave_SendsAllChangedFieldsInNewOrder()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageBrandDto?>(Existing()));
        _mediator.Send(Arg.Any<UpdateHomepageBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=homepage-brand-selected] tbody tr").Should().HaveCount(3));
        var rows = cut.FindAll("[data-testid=homepage-brand-selected] tbody tr");
        rows[0].QuerySelectorAll("button")[0].HasAttribute("disabled").Should().BeTrue();
        rows[2].QuerySelectorAll("button")[1].HasAttribute("disabled").Should().BeTrue();
        await rows[0].QuerySelectorAll("button")[1].ClickAsync(new());
        cut.Find("[data-testid=homepage-brand-title]").Input("Updated");
        cut.Find("[data-testid=homepage-brand-button-label]").Input("Explore");
        cut.Find("[data-testid=homepage-brand-button-link]").Input("/brands");
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<UpdateHomepageBrandCommand>(command =>
            command.Title == "Updated" && command.ButtonLabel == "Explore" && command.ButtonLink == "/brands" &&
            !command.IsVisible && command.BrandIds.SequenceEqual(new[] { Ids[0], Ids[2], Ids[1] })), Arg.Any<CancellationToken>());
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Save_HiddenEmptySelection_SendsCreateWithNoBrands()
    {
        CanCreate();
        _mediator.Send(Arg.Any<CreateHomepageBrandCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-save]").Should().NotBeNull());
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Hidden").ClickAsync(new());
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<CreateHomepageBrandCommand>(command =>
            !command.IsVisible && command.BrandIds.Count == 0 && command.Title == "Shop by Brand"), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Save_ValidationError_RetainsDraftAndDoesNotNavigate()
    {
        CanCreate();
        _mediator.Send(Arg.Any<CreateHomepageBrandCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(HomepageBrandErrorKeys.SelectionInvalid, [],
                [new FieldValidationError("BrandIds", HomepageBrandErrorKeys.SelectionInvalid)]));
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-save]").Should().NotBeNull());
        cut.Find("[data-testid=homepage-brand-title]").Input("Draft");
        await cut.Find("form").SubmitAsync();
        cut.Find(".shop-field-error").TextContent.Should().Contain(HomepageBrandErrorKeys.SelectionInvalid);
        cut.Find("[data-testid=homepage-brand-title]").GetAttribute("value").Should().Be("Draft");
        Services.GetRequiredService<NavigationManager>().Uri.Should().NotEndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void ViewOnlyAndLoadFailure_DoNotExposeSaveOrPrematureAdd()
    {
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageBrandDto?>(Existing()));
        var view = Render<ManageHomepageBrand>();
        view.WaitForAssertion(() => view.Find("[data-testid=homepage-brand-editor]").GetAttribute("data-state").Should().Be("edit"));
        view.FindAll("[data-testid=homepage-brand-save], [data-testid=homepage-brand-open-picker]").Should().BeEmpty();
        view.Find("[data-testid=homepage-brand-title]").HasAttribute("disabled").Should().BeTrue();

        CanCreate();
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<HomepageBrandDto?>(HomepageBrandErrorKeys.LoadFailed));
        var failed = Render<ManageHomepageBrand>();
        failed.WaitForAssertion(() => failed.Find("[data-testid=homepage-brand-editor]").GetAttribute("data-state").Should().Be("load-error"));
        failed.Find("[data-testid=homepage-brand-reload]").Should().NotBeNull();
        failed.FindAll("[data-testid=homepage-brand-save]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Edit_PendingLoadShowsSkeletonThenFailureAllowsRetry()
    {
        CanEdit();
        var pending = new TaskCompletionSource<Result<HomepageBrandDto?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = Render<ManageHomepageBrand>();
        cut.Find("[data-testid=homepage-brand-skeleton]").Should().NotBeNull();
        cut.FindAll("[data-testid=homepage-brand-save]").Should().BeEmpty();

        pending.SetResult(Result.Fail<HomepageBrandDto?>(HomepageBrandErrorKeys.LoadFailed));
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-reload]").Should().NotBeNull());
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageBrandDto?>(Existing()));
        await cut.Find("[data-testid=homepage-brand-reload]").ClickAsync(new());
        cut.Find("[data-testid=homepage-brand-editor]").GetAttribute("data-state").Should().Be("edit");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Cancel_UnsavedChange_OffersNavigationWithoutSaving()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageBrandForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageBrandDto?>(Existing()));
        var cut = Render<ManageHomepageBrand>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-title]").Should().NotBeNull());
        cut.Find("[data-testid=homepage-brand-title]").Input("Unsaved");
        cut.Find("a[href='/admin/homepage']").Should().NotBeNull();
        _mediator.DidNotReceive().Send(Arg.Any<UpdateHomepageBrandCommand>(), Arg.Any<CancellationToken>());
    }
}
