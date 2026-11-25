using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductFilters;
using TheShop.Application.Features.Products.Queries.GetAdminProductListing;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Common.Dialogs;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

public class VariantManageProductsTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly List<GetAdminProductListingQuery> _requests = [];
    private Func<GetAdminProductListingQuery, CancellationToken, Task<Result<ProductListingPageDto<ProductListItemDto>>>> _respond;
    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();
    private static FilterGroupDto Colour => new("variant:colour", "Filter_Variant", FilterKind.MultiSelect,
        [new("red", "Red", null), new("blue", "Blue", null)], null, "Colour");
    private static readonly Guid ItemId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public VariantManageProductsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton(_mediator); Services.AddSingleton(_notifications);
        Services.AddSingleton(Substitute.For<IShopDialogService>());
        Services.AddSingleton<BusyState>(); Services.AddSingleton<BreadcrumbState>();
        var localizer = Substitute.For<IStringLocalizer<Strings>>();
        localizer[Arg.Any<string>()].Returns(c => new LocalizedString(c.Arg<string>(), c.Arg<string>()));
        Services.AddSingleton(localizer);
        var auth = this.AddAuthorization(); auth.SetAuthorized("viewer");
        auth.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.View.Code));
        _mediator.Send(Arg.Any<GetAdminProductFiltersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AdminProductFiltersDto([], [], new(0, 100))));
        _respond = (q, _) => Task.FromResult(Success(q));
        _mediator.Send(Arg.Any<GetAdminProductListingQuery>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            var query = c.Arg<GetAdminProductListingQuery>(); _requests.Add(query);
            return _respond(query, c.Arg<CancellationToken>());
        });
    }

    private static Result<ProductListingPageDto<ProductListItemDto>> Success(GetAdminProductListingQuery query,
        bool empty = false, string name = "Product") => Result.Ok(new ProductListingPageDto<ProductListItemDto>(
            new PagedResult<ProductListItemDto>(empty ? [] : [new(ItemId, name, "SKU", null, "Brand", "Category", 10, 40, 2, "CAD", false)],
                query.PageQuery.Pagination.Page, 10, empty ? 0 : 21), [Colour], query.PageQuery.VariantFilters ?? [], false));

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Toggle_VariantCriteria_PreservesSearchStatusFixedFiltersAndClearsRowSelection()
    {
        var brand = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var category = Guid.Parse("00000000-0000-0000-0000-000000000003");
        Nav.NavigateTo($"/admin/products?search=needle&status=inactive&brand={brand}&category={category}&price_min=10&price_max=40&sort=price-desc&page=2");
        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => cut.FindComponent<ShopTable<ProductListItemDto>>().Instance.SelectedKeysChanged.InvokeAsync(new HashSet<object> { ItemId }));
        await cut.InvokeAsync(() => cut.FindComponents<ShopFilterPanel>()[0].Instance.FilterToggled.InvokeAsync(new FilterToggle("variant:colour", "blue", true)));

        cut.WaitForAssertion(() => _requests.Last().PageQuery.VariantFilters!.Single().Values.Should().Equal("blue"));
        var query = _requests.Last().PageQuery;
        query.Pagination.Page.Should().Be(1); query.Search.Should().Be("needle"); query.Status.Should().Be(ProductStatusFilter.Inactive);
        query.BrandIds.Should().Equal(brand); query.CategoryIds.Should().Equal(category);
        query.PriceMin.Should().Be(10); query.PriceMax.Should().Be(40); query.Sort.Should().Be(AdminProductSortOption.LowestVariantPriceDesc);
        cut.FindComponent<ShopTable<ProductListItemDto>>().Instance.SelectedKeys.Should().BeEmpty();
        cut.FindAll("a").Select(a => a.GetAttribute("href") ?? "").Should()
            .NotContain(href => href.Contains("edit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Paging_RetainsMultiSelectionsAndFullRowRollups()
    {
        Nav.NavigateTo("/admin/products?variant%3Acolour=red&variant%3Acolour=blue");
        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => cut.FindComponent<ShopPagination>().Instance.PageChanged.InvokeAsync(2));

        cut.WaitForAssertion(() => _requests.Last().PageQuery.Pagination.Page.Should().Be(2));
        _requests.Last().PageQuery.VariantFilters!.Single().Values.Should().Equal("red", "blue");
        var item = cut.FindComponent<ShopTable<ProductListItemDto>>().Instance.Items.Single();
        item.VariantCount.Should().Be(2); item.MinPrice.Should().Be(10); item.MaxPrice.Should().Be(40);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task NoMatch_ClearRemovesVariantCriteriaAndRestoresResults()
    {
        _respond = (q, _) => Task.FromResult(Success(q, empty: q.PageQuery.VariantFilters?.Count > 0));
        Nav.NavigateTo("/admin/products?variant%3Acolour=blue");
        var cut = Render<ManageProducts>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(Strings.ManageProducts_NoMatchTitle));

        await cut.InvokeAsync(() => cut.FindComponents<ShopFilterPanel>()[0].Instance.OnClearFilters.InvokeAsync());

        cut.WaitForAssertion(() => cut.FindComponent<ShopTable<ProductListItemDto>>().Instance.Items.Should().ContainSingle());
        _requests.Last().PageQuery.VariantFilters.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Feature", "002_variant-filters")]
    public void LoadFailure_OffersRetryWithoutNoMatch(bool metadataFailure)
    {
        if (metadataFailure)
            _mediator.Send(Arg.Any<GetAdminProductFiltersQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Fail<AdminProductFiltersDto>("unavailable"));
        else _respond = (_, _) => Task.FromResult(Result.Fail<ProductListingPageDto<ProductListItemDto>>("unavailable"));
        var cut = Render<ManageProducts>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=listing-load-error]").TextContent.Should().Contain(Strings.VariantFilters_LoadFailed));
        cut.Markup.Should().NotContain(Strings.ManageProducts_NoMatchTitle);
        _mediator.Send(Arg.Any<GetAdminProductFiltersQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(new AdminProductFiltersDto([], [], new(0, 100))));
        _respond = (q, _) => Task.FromResult(Success(q));

        cut.Find("[data-testid=listing-retry]").Click();

        cut.WaitForAssertion(() => cut.FindComponent<ShopTable<ProductListItemDto>>().Instance.Items.Should().ContainSingle());
        cut.FindAll("[data-testid=listing-load-error]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void StaleLink_DropsUnknownValuesAndKeepsValidFiltersWithNotice()
    {
        _respond = (q, _) => Task.FromResult(q.PageQuery.VariantFilters!.Any(f => f.Values.Contains("gone"))
            ? Result.Ok(Success(q).Value with { RemovedVariantSelections = true, EffectiveVariantFilters = [new("variant:colour", ["blue"])] })
            : Success(q));
        Nav.NavigateTo("/admin/products?variant%3Acolour=blue&variant%3Acolour=gone&search=needle&status=inactive");

        var cut = Render<ManageProducts>();

        cut.WaitForAssertion(() => Nav.Uri.Should().NotContain("gone"));
        Nav.Uri.Should().Contain("search=needle").And.Contain("status=inactive");
        _notifications.Received(1).Show(Strings.VariantFilters_SelectionsRemoved, ShopNotificationKind.Info);
        cut.FindComponents<ShopFilterPanel>()[0].Instance.SelectedFilters.Single(f => f.Key == "variant:colour").Values.Should().Equal("blue");
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public void DrawerAndSidebar_PreserveSameVariantChoicesAndSelectedState()
    {
        Nav.NavigateTo("/admin/products?variant%3Acolour=blue");
        var cut = Render<ManageProducts>();
        cut.Find(".shop-products-filter-button").Click();

        cut.WaitForAssertion(() => cut.FindComponents<ShopFilterPanel>().Should().HaveCount(2));
        var panels = cut.FindComponents<ShopFilterPanel>();
        panels[0].Instance.Groups.Should().BeEquivalentTo(panels[1].Instance.Groups);
        panels[0].Instance.SelectedFilters.Should().BeEquivalentTo(panels[1].Instance.SelectedFilters);
    }
}
