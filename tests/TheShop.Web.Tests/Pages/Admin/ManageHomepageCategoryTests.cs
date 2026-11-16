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
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHomepageCategory;
using TheShop.Application.Features.Homepage.Commands.UpdateHomepageCategory;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryChoices;
using TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryForAdmin;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

public sealed class ManageHomepageCategoryTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private static readonly Guid[] Ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    public ManageHomepageCategoryTests()
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
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(null));
        _mediator.Send(Arg.Any<GetHomepageCategoryChoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CategoryDto>>([]));
    }

    private void CanCreate() => _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
    private void CanEdit() => _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(true);
    private static CategoryDto Card(int index, string? image = null) =>
        new(Ids[index], new[] { "Pens", "Pods", "Kits" }[index], null, image, true);
    private static HomepageCategoryDto Existing(bool visible = false) =>
        new("Saved section", "Browse", "/saved", [Ids[2], Ids[0], Ids[1]],
            [Card(0, "https://shop.test/pens.png"), Card(1), Card(2)], visible);

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Add_OpensWithPrefilledFieldsAndEmptySelection()
    {
        CanCreate();
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-category-editor]").GetAttribute("data-state").Should().Be("add"));
        cut.Find("h1").TextContent.Should().Be("Add Discover by Category");
        cut.Find("[data-testid=homepage-category-title]").GetAttribute("value").Should().Be("Discover by Category");
        cut.Find("[data-testid=homepage-category-button-label]").GetAttribute("value").Should().Be("View All Categories");
        cut.Find("[data-testid=homepage-category-button-link]").GetAttribute("value").Should().Be("/categories");
        cut.Find("[data-testid=homepage-category-selected]").TextContent.Should().Contain("No categories added yet");
        cut.Find("[data-testid=homepage-category-editor]").TextContent.Should().Contain("0 of 3 selected");
        cut.FindAll("[data-testid=homepage-category-selected] tbody tr strong").Should().ContainSingle();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Visible")
            .GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Heading_TracksTitleAndUsesFallbackInAddAndEdit()
    {
        CanCreate();
        var add = Render<ManageHomepageCategory>();
        add.WaitForAssertion(() => add.Find("[data-testid=homepage-category-title]").Should().NotBeNull());
        add.Find("[data-testid=homepage-category-title]").Input(" New title ");
        add.Find("h1").TextContent.Should().Be("Add New title");
        add.Find("[data-testid=homepage-category-title]").Input(" ");
        add.Find("h1").TextContent.Should().Be("Add Discover by Category");

        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(Existing()));
        var edit = Render<ManageHomepageCategory>();
        edit.WaitForAssertion(() => edit.Find("h1").TextContent.Should().Be("Edit Saved section"));
        edit.Find("[data-testid=homepage-category-title]").Input("Edited");
        edit.Find("h1").TextContent.Should().Be("Edit Edited");
        edit.Find("[data-testid=homepage-category-title]").Input("");
        edit.Find("h1").TextContent.Should().Be("Edit Discover by Category");
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Edit_RestoresSavedFieldsOrderImagesAndHiddenStatus()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(Existing()));
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-category-editor]").GetAttribute("data-state").Should().Be("edit"));
        cut.Find("[data-testid=homepage-category-title]").GetAttribute("value").Should().Be("Saved section");
        cut.Find("[data-testid=homepage-category-button-label]").GetAttribute("value").Should().Be("Browse");
        cut.Find("[data-testid=homepage-category-button-link]").GetAttribute("value").Should().Be("/saved");
        var rows = cut.FindAll("[data-testid=homepage-category-selected] tbody tr");
        rows.Select(row => row.QuerySelector("strong")!.TextContent).Should().Equal("Kits", "Pens", "Pods");
        rows[1].QuerySelector("img")!.GetAttribute("src").Should().Be("https://shop.test/pens.png");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Hidden")
            .GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("[data-testid=homepage-category-save]").Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void FullSelection_DisablesAddAndExplainsRemoveFirst()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(Existing()));
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-category-open-picker]").Should().NotBeNull());
        cut.Find("[data-testid=homepage-category-open-picker]").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid=homepage-category-editor]").TextContent.Should().ContainEquivalentOf("remove one");
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Picker_ChoosesThreeFromEmpty_DisablesFourthAndShowsNames()
    {
        CanCreate();
        var fourth = new CategoryDto(Guid.NewGuid(), "Vapes", null, null, true);
        _mediator.Send(Arg.Any<GetHomepageCategoryChoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CategoryDto>>([Card(0), Card(1), Card(2), fourth]));
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-category-open-picker]").Should().NotBeNull());
        await cut.Find("[data-testid=homepage-category-open-picker]").ClickAsync(new());
        var choices = cut.FindAll("[data-testid=homepage-category-picker] .shop-image-tile-surface");
        choices.Should().HaveCount(4);
        foreach (var choice in choices.Take(3)) await choice.ClickAsync(new());
        cut.FindAll("[data-testid=homepage-category-picker] .shop-image-tile-surface")[3]
            .HasAttribute("disabled").Should().BeTrue();
        await cut.Find("[data-testid=homepage-category-confirm-picker]").ClickAsync(new());
        cut.FindAll("[data-testid=homepage-category-selected] tbody tr strong")
            .Select(name => name.TextContent).Should().Equal("Pens", "Pods", "Kits");
        cut.Find("[data-testid=homepage-category-editor]").TextContent.Should().Contain("3 of 3 selected");
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Edit_MoveAndRemove_UpdatesOrderedSelectionBeforeSave()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(Existing()));
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=homepage-category-selected] tbody tr").Should().HaveCount(3));
        await cut.FindAll("[data-testid=homepage-category-selected] tbody tr")[0].QuerySelectorAll("button")[1].ClickAsync(new());
        cut.FindAll("[data-testid=homepage-category-selected] tbody tr").Select(row => row.QuerySelector("strong")!.TextContent)
            .Should().Equal("Pens", "Kits", "Pods");
        await cut.FindAll("[data-testid=homepage-category-selected] tbody tr")[2].QuerySelectorAll("button")[2].ClickAsync(new());
        cut.Find("[data-testid=homepage-category-editor]").TextContent.Should().Contain("2 of 3 selected");
        cut.Find("[data-testid=homepage-category-open-picker]").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Save_ValidEdit_SendsChangedDetailsOrderAndVisibility()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(Existing()));
        _mediator.Send(Arg.Any<UpdateHomepageCategoryCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-category-save]").Should().NotBeNull());
        cut.Find("[data-testid=homepage-category-title]").Input("Updated");
        cut.Find("[data-testid=homepage-category-button-label]").Input("Explore");
        cut.Find("[data-testid=homepage-category-button-link]").Input("/categories");
        await cut.FindAll("[data-testid=homepage-category-selected] tbody tr")[0].QuerySelectorAll("button")[1].ClickAsync(new());
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Is<UpdateHomepageCategoryCommand>(command =>
            command.Title == "Updated" && command.ButtonLabel == "Explore" && command.ButtonLink == "/categories" &&
            !command.IsVisible && command.CategoryIds.SequenceEqual(new[] { Ids[0], Ids[2], Ids[1] })), Arg.Any<CancellationToken>());
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public async Task Save_ValidationErrorsRemainInlineAndDoNotNavigate()
    {
        CanCreate();
        _mediator.Send(Arg.Any<CreateHomepageCategoryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(HomepageCategoryErrorKeys.SelectionInvalid, [],
                [new FieldValidationError("CategoryIds", HomepageCategoryErrorKeys.SelectionInvalid)]));
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-category-save]").Should().NotBeNull());
        await cut.Find("form").SubmitAsync();
        cut.Find(".shop-field-error").TextContent.Should().Contain(HomepageCategoryErrorKeys.SelectionInvalid);
        Services.GetRequiredService<NavigationManager>().Uri.Should().NotEndWith(Routes.Admin.ManageHomepage);
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void ViewOnly_AndLoadFailure_DoNotExposeMutation()
    {
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(Existing()));
        var view = Render<ManageHomepageCategory>();
        view.WaitForAssertion(() => view.Find("[data-testid=homepage-category-editor]").GetAttribute("data-state").Should().Be("edit"));
        view.FindAll("[data-testid=homepage-category-save], [data-testid=homepage-category-open-picker]").Should().BeEmpty();
        view.Find("[data-testid=homepage-category-title]").HasAttribute("disabled").Should().BeTrue();

        CanCreate();
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<HomepageCategoryDto?>(HomepageCategoryErrorKeys.LoadFailed));
        var failed = Render<ManageHomepageCategory>();
        failed.WaitForAssertion(() => failed.Find("[data-testid=homepage-category-editor]").GetAttribute("data-state").Should().Be("load-error"));
        failed.Find("[data-testid=homepage-category-reload]").Should().NotBeNull();
        failed.FindAll("[data-testid=homepage-category-save]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "006_homepage-category")]
    public void Cancel_UnsavedChanges_IsNavigationWithoutSave()
    {
        CanEdit();
        _mediator.Send(Arg.Any<GetHomepageCategoryForAdminQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<HomepageCategoryDto?>(Existing()));
        var cut = Render<ManageHomepageCategory>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-category-title]").Should().NotBeNull());
        cut.Find("[data-testid=homepage-category-title]").Input("Unsaved");
        cut.Find("a[href='/admin/homepage']").Should().NotBeNull();
        _mediator.DidNotReceive().Send(Arg.Any<UpdateHomepageCategoryCommand>(), Arg.Any<CancellationToken>());
    }
}
