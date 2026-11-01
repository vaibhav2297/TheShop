using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;
using TheShop.Web.Common;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Admin;

public sealed class FreshDropsProductDialogTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly FreshDropsProductDto _first = new(Guid.NewGuid(), "First", "Brand", 10, "CAD", null);
    private readonly FreshDropsProductDto _second = new(Guid.NewGuid(), "Second", "Brand", 20, "CAD", null);

    public FreshDropsProductDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(_ => true).SetVoidResult();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_localizer);
        Services.AddSingleton<BusyState>();
        _localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        _mediator.Send(Arg.Any<GetFreshDropsProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<FreshDropsProductDto>>([_first, _second]));
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Confirm_SelectedProducts_ReturnsDistinctIdsAndCards()
    {
        FreshDropsProductDialogResult? result = null;
        var cut = Render<FreshDropsProductDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, [])
            .Add(component => component.OnCompleted, value => result = value));
        cut.WaitForAssertion(() => cut.FindAll(".shop-fresh-drops-picker-grid li").Should().HaveCount(2));
        await cut.FindAll(".shop-fresh-drops-picker-grid .shop-image-tile-surface")[0].ClickAsync(new());
        await cut.FindAll(".shop-fresh-drops-picker-grid .shop-image-tile-surface")[1].ClickAsync(new());
        await cut.Find("[data-testid=fresh-drops-confirm-picker]").ClickAsync(new());
        result.Should().NotBeNull();
        result!.ProductIds.Should().Equal(_first.Id, _second.Id);
        result.Cards.Select(card => card.Id).Should().Equal(_first.Id, _second.Id);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Cancel_AfterSelection_ReturnsNullOnce()
    {
        var results = new List<FreshDropsProductDialogResult?>();
        var cut = Render<FreshDropsProductDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, [])
            .Add(component => component.OnCompleted, value => results.Add(value)));
        cut.WaitForAssertion(() => cut.FindAll(".shop-fresh-drops-picker-grid li").Should().HaveCount(2));
        await cut.Find(".shop-fresh-drops-picker-grid .shop-image-tile-surface").ClickAsync(new());
        await cut.InvokeAsync(() => cut.FindComponent<ShopDialog>().Instance.DismissAsync());
        await cut.Find("[data-testid=fresh-drops-confirm-picker]").ClickAsync(new());
        results.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public void Search_NoMatches_ShowsGuidanceWithoutLosingSelection()
    {
        _mediator.Send(Arg.Any<GetFreshDropsProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => ((GetFreshDropsProductsQuery)call[0]).Search == "unmatched"
                ? Result.Ok<IReadOnlyList<FreshDropsProductDto>>([])
                : Result.Ok<IReadOnlyList<FreshDropsProductDto>>([_first]));
        var cut = Render<FreshDropsProductDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, [_first.Id]));
        cut.WaitForAssertion(() => cut.Find(".shop-fresh-drops-picker-grid li").Should().NotBeNull());
        cut.Find("[data-testid=fresh-drops-search]").Input("unmatched");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(Strings.FreshDrops_NoResults));
        cut.Markup.Should().Contain(string.Format(Strings.FreshDrops_SelectedCount, 1));
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Search_ClearIcon_RestoresResults()
    {
        _mediator.Send(Arg.Any<GetFreshDropsProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => ((GetFreshDropsProductsQuery)call[0]).Search == "unmatched"
                ? Result.Ok<IReadOnlyList<FreshDropsProductDto>>([])
                : Result.Ok<IReadOnlyList<FreshDropsProductDto>>([_first]));
        var cut = Render<FreshDropsProductDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, []));
        cut.Find("[data-testid=fresh-drops-search]").Input("unmatched");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(Strings.FreshDrops_NoResults));
        await cut.Find($"button[aria-label='{Strings.FreshDrops_ClearSearch}']").ClickAsync(new());
        cut.WaitForAssertion(() => cut.FindAll(".shop-fresh-drops-picker-grid li").Should().ContainSingle());
        cut.Find("[data-testid=fresh-drops-search]").GetAttribute("value").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Select_ThirdProduct_DisablesFourthAndNeverDuplicatesSelection()
    {
        var third = new FreshDropsProductDto(Guid.NewGuid(), "Third", "Brand", 30, "CAD", null);
        var fourth = new FreshDropsProductDto(Guid.NewGuid(), "Fourth", "Brand", 40, "CAD", null);
        _mediator.Send(Arg.Any<GetFreshDropsProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<FreshDropsProductDto>>([_first, _second, third, fourth]));
        FreshDropsProductDialogResult? result = null;
        var cut = Render<FreshDropsProductDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, [_first.Id, _second.Id])
            .Add(component => component.OnCompleted, value => result = value));
        cut.WaitForAssertion(() => cut.FindAll(".shop-fresh-drops-picker-grid li").Should().HaveCount(4));
        await cut.FindAll(".shop-fresh-drops-picker-grid .shop-image-tile-surface")[2].ClickAsync(new());
        cut.FindAll(".shop-fresh-drops-picker-grid .shop-image-tile-surface")[3]
            .HasAttribute("disabled").Should().BeTrue();
        await cut.Find("[data-testid=fresh-drops-confirm-picker]").ClickAsync(new());
        result!.ProductIds.Should().Equal(_first.Id, _second.Id, third.Id);
    }
}
