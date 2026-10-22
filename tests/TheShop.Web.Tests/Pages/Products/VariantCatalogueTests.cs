using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetCatalogueFilters;
using TheShop.Application.Features.Products.Queries.GetProductCatalogueListing;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Pages.Products;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Products;

public class VariantCatalogueTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly List<GetProductCatalogueListingQuery> _requests = [];
    private Func<GetProductCatalogueListingQuery, CancellationToken, Task<Result<ProductListingPageDto<ProductSummaryDto>>>> _respond;
    private static FilterGroupDto Colour => new("variant:colour", "Filter_Variant", FilterKind.MultiSelect,
        [new("red", "Red", null), new("blue", "Blue", null)], null, "Colour");
    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    public VariantCatalogueTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton(_mediator); Services.AddSingleton(_notifications);
        Services.AddSingleton<BusyState>(); Services.AddSingleton<BreadcrumbState>();
        var localizer = Substitute.For<IStringLocalizer<Strings>>();
        localizer[Arg.Any<string>()].Returns(c => new LocalizedString(c.Arg<string>(), c.Arg<string>()));
        Services.AddSingleton(localizer);
        _mediator.Send(Arg.Any<GetCatalogueFiltersQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(new CatalogueFiltersDto([])));
        _respond = (q, _) => Task.FromResult(Success(q));
        _mediator.Send(Arg.Any<GetProductCatalogueListingQuery>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            var query = c.Arg<GetProductCatalogueListingQuery>();
            _requests.Add(query);
            return _respond(query, c.Arg<CancellationToken>());
        });
    }

    private static Result<ProductListingPageDto<ProductSummaryDto>> Success(GetProductCatalogueListingQuery query,
        string name = "Latest product", bool empty = false, bool removed = false)
    {
        IReadOnlyList<ProductSummaryDto> items = empty ? [] :
            [new(Guid.Parse("00000000-0000-0000-0000-000000000001"), name, null!, 25, null, false, "CAD", true, "Brand", null, true)];
        var page = new PagedResult<ProductSummaryDto>(items, query.PageQuery.Pagination.Page, 12, empty ? 0 : 25);
        return Result.Ok(new ProductListingPageDto<ProductSummaryDto>(page, [Colour],
            [.. query.PageQuery.SelectedFilters.Where(f => f.Key.StartsWith("variant:", StringComparison.Ordinal))], removed));
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Toggle_TwoValues_ReturnsToFirstPageAndRetainsChoicesWhenPaging()
    {
        Nav.NavigateTo("/products?page=2&sort=price-desc&brand=brand-id");
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => cut.FindComponent<ShopFilterPanel>().Instance.FilterToggled.InvokeAsync(new FilterToggle("variant:colour", "red", true)));
        await cut.InvokeAsync(() => cut.FindComponent<ShopFilterPanel>().Instance.FilterToggled.InvokeAsync(new FilterToggle("variant:colour", "blue", true)));
        cut.WaitForAssertion(() => _requests.Last().PageQuery.SelectedFilters.Single(f => f.Key == "variant:colour").Values.Should().Equal("red", "blue"));
        _requests.Last().PageQuery.Pagination.Page.Should().Be(1);
        _requests.Last().PageQuery.Sort.Should().Be(ProductSortOption.PriceHighToLow);
        _requests.Last().PageQuery.SelectedFilters.Should().Contain(f => f.Key == "brand");
        await cut.InvokeAsync(() => cut.FindComponent<ShopPagination>().Instance.PageChanged.InvokeAsync(2));
        cut.WaitForAssertion(() => _requests.Last().PageQuery.Pagination.Page.Should().Be(2));
        _requests.Last().PageQuery.SelectedFilters.Single(f => f.Key == "variant:colour").Values.Should().Equal("red", "blue");
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task EmptyFilteredResults_ClearActionRemovesVariantsAndRestoresListing()
    {
        _respond = (q, _) => Task.FromResult(Success(q, empty: q.PageQuery.SelectedFilters.Count > 0));
        Nav.NavigateTo("/products?variant%3Acolour=red");
        var cut = Render<ProductCatalogue>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(Strings.Empty_NoResults));

        await cut.InvokeAsync(() => cut.FindComponent<ShopFilterPanel>().Instance.OnClearFilters.InvokeAsync());

        cut.WaitForAssertion(() => cut.FindComponents<ProductCard>().Should().ContainSingle());
        _requests.Last().PageQuery.SelectedFilters.Should().BeEmpty();
        new Uri(Nav.Uri).Query.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void StaleLink_RemovesInvalidSelectionWithNoticeAndPreservesOtherCriteria()
    {
        _respond = (q, _) =>
        {
            var stale = q.PageQuery.SelectedFilters.Any(f => f.Values.Contains("gone"));
            var result = Success(q);
            return Task.FromResult(stale ? Result.Ok(result.Value with
            {
                EffectiveVariantFilters = [new("variant:colour", ["red"])],
                RemovedVariantSelections = true,
            }) : result);
        };
        Nav.NavigateTo("/products?variant%3Acolour=red&variant%3Acolour=gone&brand=brand-id&price_min=10&sort=price-desc");

        var cut = Render<ProductCatalogue>();

        cut.WaitForAssertion(() => Nav.Uri.Should().NotContain("gone"));
        Nav.Uri.Should().Contain("brand=brand-id").And.Contain("price_min=10").And.Contain("sort=price-desc");
        _notifications.Received(1).Show(Strings.VariantFilters_SelectionsRemoved, ShopNotificationKind.Info);
        cut.FindComponent<ShopFilterPanel>().Instance.SelectedFilters.Single(f => f.Key == "variant:colour").Values.Should().Equal("red");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Feature", "002_variant-filters")]
    public void LoadFailure_ShowsRetryWithoutNoMatchAndRecovers(bool metadataFailure)
    {
        if (metadataFailure)
            _mediator.Send(Arg.Any<GetCatalogueFiltersQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Fail<CatalogueFiltersDto>("unavailable"));
        else
            _respond = (_, _) => Task.FromResult(Result.Fail<ProductListingPageDto<ProductSummaryDto>>("unavailable"));
        var cut = Render<ProductCatalogue>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=listing-load-error]").TextContent.Should().Contain(Strings.VariantFilters_LoadFailed));
        cut.Markup.Should().NotContain(Strings.Empty_NoResults);
        _mediator.Send(Arg.Any<GetCatalogueFiltersQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(new CatalogueFiltersDto([])));
        _respond = (q, _) => Task.FromResult(Success(q));

        cut.Find("[data-testid=listing-retry]").Click();

        cut.WaitForAssertion(() => cut.FindComponents<ProductCard>().Should().ContainSingle());
        cut.FindAll("[data-testid=listing-load-error]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task DrawerAndSidebar_ExposeSameGroupsAndSelections()
    {
        Nav.NavigateTo("/products?variant%3Acolour=red");
        var cut = Render<ProductCatalogue>();
        cut.Find(".shop-catalogue-filter-button").Click();
        cut.WaitForAssertion(() => cut.FindComponents<ShopFilterPanel>().Should().HaveCount(2));
        var panels = cut.FindComponents<ShopFilterPanel>();
        panels[0].Instance.Groups.Should().BeEquivalentTo(panels[1].Instance.Groups);
        panels[0].Instance.SelectedFilters.Should().BeEquivalentTo(panels[1].Instance.SelectedFilters);

        await cut.InvokeAsync(() => panels[1].Instance.FilterToggled.InvokeAsync(new FilterToggle("variant:colour", "blue", true)));

        cut.WaitForAssertion(() => cut.FindComponents<ShopFilterPanel>().Should().OnlyContain(p =>
            p.Instance.SelectedFilters.Single().Values.Count == 2));
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task SupersededResponse_AfterClearCannotRestoreResultsSelectionsNoticeOrUrl()
    {
        var pending = new TaskCompletionSource<Result<ProductListingPageDto<ProductSummaryDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken oldToken = default;
        GetProductCatalogueListingQuery? oldQuery = null;
        _respond = (q, ct) =>
        {
            if (q.PageQuery.SelectedFilters.Count == 0) return Task.FromResult(Success(q));
            oldToken = ct; oldQuery = q;
            return pending.Task;
        };
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => cut.FindComponent<ShopFilterPanel>().Instance.FilterToggled.InvokeAsync(new FilterToggle("variant:colour", "red", true)));
        cut.WaitForState(() => oldQuery is not null);
        await cut.InvokeAsync(() => cut.FindComponent<ShopFilterPanel>().Instance.OnClearFilters.InvokeAsync());
        oldToken.IsCancellationRequested.Should().BeTrue();
        await cut.InvokeAsync(() => pending.SetResult(Success(oldQuery!, "Stale product", removed: true)));

        cut.WaitForAssertion(() => cut.FindComponent<ProductCard>().Instance.Product.Name.Should().Be("Latest product"));
        new Uri(Nav.Uri).Query.Should().BeEmpty();
        cut.FindComponent<ShopFilterPanel>().Instance.SelectedFilters.Should().BeEmpty();
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<ShopNotificationKind>());
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task DisposedPage_CancelsPendingReadAndIgnoresLateResponse()
    {
        var pending = new TaskCompletionSource<Result<ProductListingPageDto<ProductSummaryDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        GetProductCatalogueListingQuery? request = null;
        _respond = (q, ct) => { request = q; token = ct; return pending.Task; };
        var cut = Render<ProductCatalogue>();
        cut.WaitForState(() => request is not null);

        await DisposeComponentsAsync();
        token.IsCancellationRequested.Should().BeTrue();
        pending.SetResult(Success(request!, "Stale product", removed: true));
        await cut.InvokeAsync(() => { });

        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<ShopNotificationKind>());
    }
}
