using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetHomepageBrandChoices;
using TheShop.Web.Common;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Admin;

public sealed class HomepageBrandPickerDialogTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly BrandDto _active = new(Guid.NewGuid(), "Active Brand", null, "https://shop.test/active.svg", true);
    private readonly BrandDto _inactive = new(Guid.NewGuid(), "Inactive Brand", null, "https://shop.test/inactive.svg", false);

    public HomepageBrandPickerDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(_ => true).SetVoidResult();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_localizer);
        Services.AddSingleton<BusyState>();
        _localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        _mediator.Send(Arg.Any<GetHomepageBrandChoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<BrandDto>>([_active, _inactive]));
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Picker_WithInactiveSavedBrand_ShowsOnlyActivePreviewAndReturnsActiveSelection()
    {
        HomepageBrandPickerResult? result = null;
        var cut = Render<HomepageBrandPickerDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, [_inactive.Id])
            .Add(component => component.OnCompleted, value => result = value));

        cut.WaitForAssertion(() => cut.FindAll(".shop-homepage-brand-choice-grid li").Should().ContainSingle());
        cut.Find(".shop-homepage-brand-choice-grid img").GetAttribute("src").Should().Be(_active.LogoUrl);
        cut.Find("[data-testid=homepage-brand-picker]").TextContent.Should().Contain("0 of 6 selected");
        cut.Find("[data-testid=homepage-brand-search]").Input("unmatched");
        var noResults = cut.Find("[data-testid=homepage-brand-no-results]");
        noResults.QuerySelector("h3")!.TextContent.Should().Be("No brands found");
        noResults.QuerySelector("p")!.TextContent.Should().Be("No brands match “unmatched”. Try a different brand name.");
        cut.Find("[data-testid=homepage-brand-search]").Input("Active");
        await cut.Find(".shop-homepage-brand-choice-grid .shop-image-tile-surface").ClickAsync(new());
        await cut.Find("[data-testid=homepage-brand-confirm-picker]").ClickAsync(new());

        result.Should().NotBeNull();
        result!.BrandIds.Should().Equal(_active.Id);
        result.Cards.Should().ContainSingle().Which.LogoUrl.Should().Be(_active.LogoUrl);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Picker_DismissAfterSelection_ReturnsNullOnce()
    {
        var results = new List<HomepageBrandPickerResult?>();
        var cut = Render<HomepageBrandPickerDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, [])
            .Add(component => component.OnCompleted, value => results.Add(value)));

        cut.WaitForAssertion(() => cut.FindAll(".shop-homepage-brand-choice-grid li").Should().ContainSingle());
        await cut.Find(".shop-homepage-brand-choice-grid .shop-image-tile-surface").ClickAsync(new());
        await cut.InvokeAsync(() => cut.FindComponent<ShopDialog>().Instance.DismissAsync());
        await cut.Find("[data-testid=homepage-brand-confirm-picker]").ClickAsync(new());

        results.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public void Picker_PendingChoices_ShowsSkeletonUntilBrandsLoad()
    {
        var pending = new TaskCompletionSource<Result<IReadOnlyList<BrandDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediator.Send(Arg.Any<GetHomepageBrandChoicesQuery>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = Render<HomepageBrandPickerDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, []));
        cut.Find("[data-testid=homepage-brand-picker-skeleton]").Should().NotBeNull();
        pending.SetResult(Result.Ok<IReadOnlyList<BrandDto>>([_active]));
        cut.WaitForAssertion(() => cut.FindAll(".shop-homepage-brand-choice-grid li").Should().ContainSingle());
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Picker_LoadFailure_OffersRetryAndPreservesInitialSelection()
    {
        _mediator.Send(Arg.Any<GetHomepageBrandChoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<BrandDto>>("HomepageBrand_ChoicesLoadFailed"),
                Result.Ok<IReadOnlyList<BrandDto>>([_active]));
        var cut = Render<HomepageBrandPickerDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, [_active.Id]));
        cut.WaitForAssertion(() => cut.Find("[data-testid=homepage-brand-retry-choices]").Should().NotBeNull());
        await cut.Find("[data-testid=homepage-brand-retry-choices]").ClickAsync(new());
        cut.Find("[data-testid=homepage-brand-picker]").TextContent.Should().Contain("1 of 6 selected");
        cut.FindAll(".shop-homepage-brand-choice-grid li").Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Picker_AtSixSelections_DisablesSeventhUntilOneRemoved()
    {
        var cards = Enumerable.Range(0, 7)
            .Select(index => new BrandDto(Guid.NewGuid(), $"Brand {index}", null, null, true)).ToArray();
        _mediator.Send(Arg.Any<GetHomepageBrandChoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<BrandDto>>(cards));
        var cut = Render<HomepageBrandPickerDialog>(parameters => parameters
            .Add(component => component.InitialSelectedIds, cards.Take(6).Select(card => card.Id).ToArray()));
        cut.WaitForAssertion(() => cut.FindAll(".shop-homepage-brand-choice-grid li").Should().HaveCount(7));
        cut.FindAll(".shop-homepage-brand-choice-grid .shop-image-tile-surface")[6]
            .HasAttribute("disabled").Should().BeTrue();
        await cut.FindAll(".shop-homepage-brand-choice-grid .shop-image-tile-surface")[0].ClickAsync(new());
        cut.FindAll(".shop-homepage-brand-choice-grid .shop-image-tile-surface")[6]
            .HasAttribute("disabled").Should().BeFalse();
    }
}
