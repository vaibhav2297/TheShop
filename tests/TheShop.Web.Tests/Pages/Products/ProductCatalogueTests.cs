using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetCatalogueFilters;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Pages.Products;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Products;

/// <summary>
/// Tests for the <see cref="ProductCatalogue"/> page — dispatches the filters and page
/// queries, renders the grid, and re-queries (always resetting to page 1) whenever sort,
/// filters, or the price range change, while pagination preserves the active criteria.
/// Covers FR-1, FR-6, FR-7, FR-8; AC-1, AC-6, AC-7, AC-8, AC-10, and the "still loading" edge
/// case.
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductCatalogueTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly List<GetProductCataloguePageQuery> _receivedPageQueries = [];

    public ProductCatalogueTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddSingleton<BusyState>();
        Services.AddSingleton<BreadcrumbState>();
        Services.AddMudServices();

        var popoverService = Substitute.For<IPopoverService>();
        popoverService.PopoverOptions.Returns(new PopoverOptions());
        Services.Replace(ServiceDescriptor.Singleton(popoverService));

        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });

        // Default happy-path stubs; individual tests override as needed.
        _mediator.Send(Arg.Any<GetCatalogueFiltersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new CatalogueFiltersDto([CategoryGroup()])));

        _mediator.Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetProductCataloguePageQuery>();
                _receivedPageQueries.Add(query);
                return Task.FromResult(Result.Ok(BuildPage(query.Pagination, totalCount: 24, itemCount: 12)));
            });
    }

    private static FilterGroupDto CategoryGroup() =>
        new(
            "category", "Filter_Category", FilterKind.MultiSelect,
            [new FilterOptionDto("cat-1", "Disposables", null)], null);

    private static ProductSummaryDto BuildDto(string name) =>
        new(Guid.NewGuid(), name, "https://example.com/photo.webp", 24.99m, null, false, "CAD", true, "Elf Bar", null, null);

    private static PagedResult<ProductSummaryDto> BuildPage(PaginationRequest pagination, int totalCount, int itemCount)
    {
        var items = Enumerable.Range(1, itemCount).Select(i => BuildDto($"Product {i}")).ToList();
        return new PagedResult<ProductSummaryDto>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    // =========================================================================
    // Happy path — dispatches both queries, renders the grid (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Render_OnInitialize_DispatchesFiltersAndPageQueries()
    {
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });

        await _mediator.Received(1).Send(Arg.Any<GetCatalogueFiltersQuery>(), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Render_WithProducts_RendersAProductCardPerItem()
    {
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });

        cut.FindComponents<ProductCard>().Should().HaveCount(12);
    }

    // =========================================================================
    // Public access — no sign-in required (FR-9, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithoutAnyAuthenticationContextRegistered_RendersSuccessfully()
    {
        // No AuthenticationStateProvider / CascadingAuthenticationState is registered anywhere
        // in this test class's DI container — the catalogue page must not require one, proving
        // both guests and signed-in customers can browse it (FR-9, AC-3).
        var act = () => Render<ProductCatalogue>();

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Render_OnInitialize_DefaultsToNewestFirstSortAndPageOne()
    {
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });

        _receivedPageQueries.Should().ContainSingle();
        _receivedPageQueries[0].Sort.Should().Be(ProductSortOption.NewestFirst);
        _receivedPageQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Empty state (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Render_WhenNoProductsMatch_ShowsEmptyStateMessage()
    {
        _mediator.Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result.Ok(
                PagedResult<ProductSummaryDto>.Empty(call.Arg<GetProductCataloguePageQuery>().Pagination))));

        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });

        cut.Markup.Should().Contain(Strings.Empty_NoResults);
        cut.FindComponents<ProductCard>().Should().BeEmpty();
    }

    // =========================================================================
    // Sort resets to page 1 (AC-7 + "returns to the first page" constraint)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ChangeSort_WhenUserSelectsADifferentOption_ResetsToPageOneAndSendsNewSort()
    {
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });
        _receivedPageQueries.Clear();

        var sortControl = cut.FindComponent<ProductSortControl>();
        await cut.InvokeAsync(() => sortControl.Instance.SortChanged.InvokeAsync(ProductSortOption.PriceLowToHigh));

        _receivedPageQueries.Should().ContainSingle();
        _receivedPageQueries[0].Sort.Should().Be(ProductSortOption.PriceLowToHigh);
        _receivedPageQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Filter change resets to page 1 (AC-6 + "returns to the first page" constraint)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ApplyFilter_WhenUserSelectsAFilterOption_ResetsToPageOneWithTheFilterApplied()
    {
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });
        _receivedPageQueries.Clear();

        var filterPanel = cut.FindComponent<ProductFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.FilterToggled.InvokeAsync(
            new FilterToggle("category", "cat-1", IsSelected: true)));

        _receivedPageQueries.Should().ContainSingle();
        _receivedPageQueries[0].SelectedFilters.Should().ContainSingle(f => f.Key == "category");
        _receivedPageQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ApplyFilters_WhenTwoOptionsAreToggledInRapidSuccession_BothSurviveInTheQuery()
    {
        // Regression: rapid successive toggles must compound. Previously the second toggle was
        // built from a selection that only refreshed after the first toggle's URL round-trip, so it
        // silently dropped the first. Firing both before the round-trips settle proves they now
        // accumulate because the page merges each toggle into its own synchronously-updated state.
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });
        _receivedPageQueries.Clear();

        var filterPanel = cut.FindComponent<ProductFilterPanel>();
        await cut.InvokeAsync(async () =>
        {
            await filterPanel.Instance.FilterToggled.InvokeAsync(new FilterToggle("category", "cat-1", IsSelected: true));
            await filterPanel.Instance.FilterToggled.InvokeAsync(new FilterToggle("brand", "brand-1", IsSelected: true));
        });

        var latest = _receivedPageQueries.Last();
        latest.SelectedFilters.Should().Contain(f => f.Key == "category" && f.Values.Contains("cat-1"));
        latest.SelectedFilters.Should().Contain(f => f.Key == "brand" && f.Values.Contains("brand-1"));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ClearFilters_AfterNarrowingThePriceRange_ResetsPriceBoundsToNull()
    {
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });

        var filterPanel = cut.FindComponent<ProductFilterPanel>();

        // Narrow the price range, then clear everything.
        await cut.InvokeAsync(() => filterPanel.Instance.PriceMinChanged.InvokeAsync(25m));
        await cut.InvokeAsync(() => filterPanel.Instance.PriceMaxChanged.InvokeAsync(60m));
        _receivedPageQueries.Clear();

        await cut.InvokeAsync(() => filterPanel.Instance.OnClearFilters.InvokeAsync());

        _receivedPageQueries.Should().ContainSingle();
        _receivedPageQueries[0].PriceMin.Should().BeNull();
        _receivedPageQueries[0].PriceMax.Should().BeNull();
        _receivedPageQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Pagination preserves the active sort/filters (AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task ChangePage_WhenUserNavigatesToPage2_PreservesTheActiveSort()
    {
        var cut = Render<ProductCatalogue>();
        await cut.InvokeAsync(() => { });

        // Establish a non-default sort first, then paginate, and confirm it survives.
        var sortControl = cut.FindComponent<ProductSortControl>();
        await cut.InvokeAsync(() => sortControl.Instance.SortChanged.InvokeAsync(ProductSortOption.NameAToZ));
        _receivedPageQueries.Clear();

        var pagination = cut.FindComponent<ShopPagination>();
        await cut.InvokeAsync(() => pagination.Instance.PageChanged.InvokeAsync(2));

        _receivedPageQueries.Should().ContainSingle();
        _receivedPageQueries[0].Pagination.Page.Should().Be(2);
        _receivedPageQueries[0].Sort.Should().Be(ProductSortOption.NameAToZ);
    }

    // =========================================================================
    // Loading edge case
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WhileQueriesAreInFlight_ShowsTwelveProductSkeletonsAndFiveFilterSkeletons()
    {
        var filtersTcs = new TaskCompletionSource<Result<CatalogueFiltersDto>>();
        var productsTcs = new TaskCompletionSource<Result<PagedResult<ProductSummaryDto>>>();
        _mediator.Send(Arg.Any<GetCatalogueFiltersQuery>(), Arg.Any<CancellationToken>()).Returns(filtersTcs.Task);
        _mediator.Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>()).Returns(productsTcs.Task);

        var cut = Render<ProductCatalogue>();

        cut.FindComponents<MudSkeleton>().Should().HaveCount(17);
        cut.FindComponents<ProductCard>().Should().BeEmpty();
        cut.FindComponents<ProductFilterPanel>().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Render_AfterFiltersLoad_ReplacesTheFilterSkeletonWithThePanelAndDoesNotReskeletonOnLaterBusyToggles()
    {
        var filtersTcs = new TaskCompletionSource<Result<CatalogueFiltersDto>>();
        _mediator.Send(Arg.Any<GetCatalogueFiltersQuery>(), Arg.Any<CancellationToken>()).Returns(filtersTcs.Task);

        var cut = Render<ProductCatalogue>();
        cut.FindComponents<ProductFilterPanel>().Should().BeEmpty();

        filtersTcs.SetResult(Result.Ok(new CatalogueFiltersDto([CategoryGroup()])));
        await cut.InvokeAsync(() => { });

        cut.FindComponent<ProductFilterPanel>().Should().NotBeNull();

        // A later catalogue busy toggle (e.g. paginating) must not re-show the filter skeleton —
        // the filters themselves never change after the initial load.
        var pagination = cut.FindComponent<ShopPagination>();
        await cut.InvokeAsync(() => pagination.Instance.PageChanged.InvokeAsync(2));

        cut.FindComponent<ProductFilterPanel>().Should().NotBeNull();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Render_OnInitialize_DispatchesFiltersAndPageQueries, Render_WithProducts_RendersAProductCardPerItem
// AC-3 (guest/signed-in access half): Render_WithoutAnyAuthenticationContextRegistered_RendersSuccessfully
// AC-6: ApplyFilter_WhenUserSelectsAFilterOption_ResetsToPageOneWithTheFilterApplied
// AC-7: ChangeSort_WhenUserSelectsADifferentOption_ResetsToPageOneAndSendsNewSort,
//        Render_OnInitialize_DefaultsToNewestFirstSortAndPageOne
// AC-8: ChangePage_WhenUserNavigatesToPage2_PreservesTheActiveSort
// AC-10: Render_WhenNoProductsMatch_ShowsEmptyStateMessage
