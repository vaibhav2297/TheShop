using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.DeleteProducts;
using TheShop.Application.Features.Products.Commands.SetProductStatus;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductFilters;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Common.Sorting;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="ManageProducts"/> page: that every criteria change (search, status,
/// sort, page) re-queries the list through the URL round trip, carrying the other active criteria
/// with it and resetting to page 1 where required, clearing the transient row selection (AC-2,
/// AC-6); the full row content and pagination (AC-1, FR-17); the access boundary and
/// permission-gated controls (AC-18); the id-keyed edit link (AC-8/AC-9 half); single and bulk
/// delete, including the reference-blocked refusal and the partial-success outcome that keeps
/// referenced products selected (AC-13, AC-14, AC-15, AC-16, AC-17); and the inline/bulk status
/// toggle, where activating is immediate and deactivating confirms first (AC-10, AC-11, AC-23).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class ManageProductsTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly List<GetAdminProductsPageQuery> _receivedQueries = [];

    public ManageProductsTests()
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
        Services.Replace(ServiceDescriptor.Singleton(_dialogService));

        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });

        _mediator.Send(Arg.Any<GetAdminProductFiltersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AdminProductFiltersDto([], [], new RangeFilterDto(0, 100))));

        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetAdminProductsPageQuery>();
                _receivedQueries.Add(query);
                return Task.FromResult(Result.Ok(BuildPage(query.Pagination, totalCount: 24, itemCount: 10)));
            });
    }

    private void AuthorizeAsProductManager()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(
            PolicyNames.Permission(PermissionCatalogue.Products.View.Code),
            PolicyNames.Permission(PermissionCatalogue.Products.Create.Code),
            PolicyNames.Permission(PermissionCatalogue.Products.Edit.Code),
            PolicyNames.Permission(PermissionCatalogue.Products.Delete.Code));
    }

    private void AuthorizeAsProductViewerOnly()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("viewer-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.View.Code));
    }

    private void AuthorizeAsProductEditorOnly()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("editor-user");
        authContext.SetPolicies(
            PolicyNames.Permission(PermissionCatalogue.Products.View.Code),
            PolicyNames.Permission(PermissionCatalogue.Products.Edit.Code));
    }

    private static PagedResult<ProductListItemDto> BuildPage(PaginationRequest pagination, int totalCount, int itemCount)
    {
        var items = Enumerable.Range(1, itemCount).Select(i => Item($"Product {i}")).ToList();
        return new PagedResult<ProductListItemDto>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    private static ProductListItemDto Item(
        string name, bool isPublished = true, int variantCount = 0, decimal? minPrice = 24.99m, decimal? maxPrice = 24.99m) =>
        new(Guid.NewGuid(), name, $"SKU-{name}", "https://example.com/photo.webp",
            "Elf Bar", "Disposables", minPrice, maxPrice, variantCount, "CAD", isPublished);

    private void SetUpKnownProductsList(IReadOnlyList<ProductListItemDto> items) =>
        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetAdminProductsPageQuery>();
                _receivedQueries.Add(query);
                return Task.FromResult(Result.Ok(new PagedResult<ProductListItemDto>(
                    items, query.Pagination.Page, query.Pagination.PageSize, items.Count)));
            });

    /// <summary>
    /// Configures <see cref="_dialogService"/> so the next <c>ShopConfirmDialog</c> it is asked to
    /// show resolves as either confirmed or cancelled/dismissed, without ever rendering the real
    /// dialog (RULE-2, RULE-7).
    /// </summary>
    private void SetUpConfirmDialogResult(bool confirmed)
    {
        var dialogReference = Substitute.For<IDialogReference>();
        dialogReference.Result.Returns(Task.FromResult<DialogResult?>(confirmed ? DialogResult.Ok(true) : DialogResult.Cancel()));
        _dialogService.ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>())
                      .Returns(Task.FromResult(dialogReference));
    }

    private async Task<IRenderedComponent<ManageProducts>> RenderListAsync()
    {
        AuthorizeAsProductManager();
        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });
        _receivedQueries.Clear();
        return cut;
    }

    private static async Task SelectProductsAsync(IRenderedComponent<ManageProducts> cut, int count)
    {
        var table = cut.FindComponent<MudTable<ProductListItemDto>>();
        var selected = table.Instance.Items!.Take(count).ToHashSet();
        await cut.InvokeAsync(() => table.Instance.SelectedItemsChanged.InvokeAsync(selected));
    }

    // =========================================================================
    // Initial load (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_OnInitialize_QueriesTheFirstPageWithNoNarrowingAndNameAToZ()
    {
        AuthorizeAsProductManager();

        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Search.Should().BeNull();
        _receivedQueries[0].Status.Should().BeNull("no status selection means products of every status");
        _receivedQueries[0].BrandIds.Should().BeNull();
        _receivedQueries[0].CategoryIds.Should().BeNull();
        _receivedQueries[0].Sort.Should().Be(AdminProductSortOption.NameAToZ);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Criteria changes re-query the list (AC-2..AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task ChangeStatusFilter_WhenStaffFiltersByInactive_ReQueriesWithThatStatusFromPageOne()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "inactive")));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Status.Should().Be(ProductStatusFilter.Inactive);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task ChangeSort_WhenStaffReversesTheNameOrder_ReQueriesWithThatSortFromPageOne()
    {
        var cut = await RenderListAsync();

        var sortSelect = cut.FindComponent<ShopSortSelect<AdminProductSortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(AdminProductSortOption.NameZToA));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Sort.Should().Be(AdminProductSortOption.NameZToA);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Search_WhenStaffTypesATerm_ReQueriesWithThatTermFromPageOne()
    {
        var cut = await RenderListAsync();

        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("  elf  "));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Search.Should().Be("elf", "the term is trimmed before it is queried");
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task ChangeBrandFilter_WhenStaffTogglesABrand_ReQueriesWithThatBrandIdFromPageOne()
    {
        var brandId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetAdminProductFiltersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AdminProductFiltersDto(
                [new FilterOptionDto(brandId.ToString(), "Elf Bar", null)], [], new RangeFilterDto(0, 100))));
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.FilterToggled.InvokeAsync(
            new FilterToggle(ProductFilterKeys.Brand, brandId.ToString(), true)));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].BrandIds.Should().ContainSingle(id => id == brandId);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task ChangePriceRange_WhenStaffSettlesTheSlider_ReQueriesWithThoseBoundsFromPageOne()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.RangeChanged.InvokeAsync(
            new RangeSelection(ProductFilterKeys.Price, 15m, 25m)));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].PriceMin.Should().Be(15m);
        _receivedQueries[0].PriceMax.Should().Be(25m);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Pagination preserves the active criteria (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task ChangePage_AfterFilteringAndSorting_KeepsBothCriteriaOnTheNewPage()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "active")));

        var sortSelect = cut.FindComponent<ShopSortSelect<AdminProductSortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(AdminProductSortOption.NameZToA));
        _receivedQueries.Clear();

        var pagination = cut.FindComponent<ShopPagination>();
        await cut.InvokeAsync(() => pagination.Instance.PageChanged.InvokeAsync(2));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Pagination.Page.Should().Be(2);
        _receivedQueries[0].Status.Should().Be(ProductStatusFilter.Active);
        _receivedQueries[0].Sort.Should().Be(AdminProductSortOption.NameZToA);
    }

    // =========================================================================
    // Selection is cleared by every criteria change (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task ChangeSort_WithProductsSelected_ClearsTheSelectionAndHidesTheBulkBar()
    {
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 2);

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeTrue();

        var sortSelect = cut.FindComponent<ShopSortSelect<AdminProductSortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(AdminProductSortOption.NameZToA));

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeFalse();
    }

    // =========================================================================
    // Select-all only ever offers the current page's rows (AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task SelectAll_OnAPageWithMoreProductsThanFit_SelectsOnlyTheCurrentPagesProducts()
    {
        // 24 total products, 10 on the current page (constructor default) — selecting every row
        // the table offers must never reach into the other 14 on later pages.
        var cut = await RenderListAsync();

        await SelectProductsAsync(cut, count: 10);

        cut.FindComponent<ShopBulkActionBar>().Instance.SelectedCount.Should().Be(10,
            "the table is bound to only the current page's items, so a bulk action can never reach another page's rows");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task CloseBulkBar_WhenStaffDismissesIt_ClearsTheSelectionAndHidesTheBar()
    {
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 3);

        var bar = cut.FindComponent<ShopBulkActionBar>();
        bar.Instance.Visible.Should().BeTrue();
        bar.Instance.SelectedCount.Should().Be(3);

        await cut.InvokeAsync(() => bar.Instance.OnClose.InvokeAsync());

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeFalse();
        cut.FindComponent<MudTable<ProductListItemDto>>().Instance.SelectedItems.Should().BeEmpty();
        _receivedQueries.Should().BeEmpty("dismissing a selection is local UI state, not a re-query");
    }

    // =========================================================================
    // Full row content (AC-1, FR-17) and pagination controls
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenProductsExist_ShowsEachProductsNameSkuBrandCategoryAndStatus()
    {
        var item = Item("Elf Bar BC5000");
        SetUpKnownProductsList([item]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(item.Name);
        cut.Markup.Should().Contain(item.Sku);
        cut.Markup.Should().Contain(item.BrandName);
        cut.Markup.Should().Contain(item.CategoryName);
        cut.Markup.Should().Contain(Strings.ManageProducts_StatusActive);
    }

    [Fact]
    [Trait("Feature", "reusable-image-treatments")]
    public async Task Render_WhenProductsExist_UsesThumbnailTreatmentForPrimaryImages()
    {
        var item = Item("Elf Bar BC5000");
        SetUpKnownProductsList([item]);

        var cut = await RenderListAsync();

        cut.FindComponents<ShopImage>().Should().Contain(image =>
            image.Instance.Src == item.PrimaryImageUrl && image.Instance.Treatment == ShopImageTreatment.Thumbnail);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenAProductIsUnpublished_ShowsInactiveStatus()
    {
        SetUpKnownProductsList([Item("Elf Bar BC5000", isPublished: false)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.ManageProducts_StatusInactive);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenAProductHasVariants_ShowsTheVariantCountCaption()
    {
        SetUpKnownProductsList([Item("Elf Bar BC5000", variantCount: 3, minPrice: 10m, maxPrice: 30m)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(string.Format(Strings.ManageProducts_VariantCountMany, 3));
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenAProductHasNoVariants_OmitsTheVariantCountCaption()
    {
        SetUpKnownProductsList([Item("Elf Bar BC5000", variantCount: 0)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().NotContain(Strings.ManageProducts_VariantCountOne);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenPriceRangeDiffers_ShowsBothEndsFormattedAsARange()
    {
        SetUpKnownProductsList([Item("Elf Bar BC5000", variantCount: 2, minPrice: 10m, maxPrice: 30m)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(string.Format(Strings.ManageProducts_PriceRange, "$10.00", "$30.00"));
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenProductsSpanMultiplePages_ShowsPaginationControls()
    {
        var cut = await RenderListAsync();

        cut.FindComponent<ShopPagination>().Instance.TotalPages.Should().Be(3);
    }

    // =========================================================================
    // Singular variant-count wording and equal-price collapse (AC-25)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenAProductHasExactlyOneVariant_ShowsSingularVariantCountWording()
    {
        SetUpKnownProductsList([Item("Elf Bar BC5000", variantCount: 1, minPrice: 10m, maxPrice: 10m)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.ManageProducts_VariantCountOne);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenEveryVariantSharesThePrice_ShowsOneAmountNotARange()
    {
        SetUpKnownProductsList([Item("Elf Bar BC5000", variantCount: 2, minPrice: 10m, maxPrice: 10m)]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain("$10.00");
        cut.Markup.Should().NotContain(string.Format(Strings.ManageProducts_PriceRange, "$10.00", "$10.00"));
    }

    // =========================================================================
    // Loading state shows progress, never a false empty/no-match message (AC-21)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhileTheFirstPageIsStillLoading_ShowsSkeletonsNotAFalseEmptyMessage()
    {
        var pending = new TaskCompletionSource<Result<PagedResult<ProductListItemDto>>>();
        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        AuthorizeAsProductManager();

        var cut = Render<ManageProducts>();

        cut.FindComponents<MudSkeleton>().Should().NotBeEmpty();
        cut.Markup.Should().NotContain(Strings.ManageProducts_NoMatchTitle);
        cut.Markup.Should().NotContain(Strings.ManageProducts_EmptyTitle);

        pending.SetResult(Result.Ok(BuildPage(new PaginationRequest(1, 10), totalCount: 0, itemCount: 0)));
        await cut.InvokeAsync(() => { });
    }

    // =========================================================================
    // Access boundary and permission-gated controls (AC-18)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ManageProducts_Always_CarriesTheProductsViewAuthorizePolicy()
    {
        var attributes = typeof(ManageProducts).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Products.View.Code),
            "the route-level policy is the only gate between a non-viewer and the list — " +
            "App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenUserHoldsOnlyProductsView_DoesNotShowTheAddProductButton()
    {
        AuthorizeAsProductViewerOnly();

        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });

        cut.Markup.Should().NotContain(Strings.AddProduct_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenUserHoldsOnlyProductsView_DoesNotShowEditOrDeleteRowControls()
    {
        var item = Item("Elf Bar BC5000");
        SetUpKnownProductsList([item]);
        AuthorizeAsProductViewerOnly();

        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });

        cut.FindAll($"[aria-label='{string.Format(Strings.ManageProducts_EditAria, item.Name)}']").Should().BeEmpty();
        cut.FindAll($"[aria-label='{string.Format(Strings.ManageProducts_DeleteAria, item.Name)}']").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenUserHoldsOnlyProductsView_RendersStatusAsPlainTextNotAClickableToggle()
    {
        SetUpKnownProductsList([Item("Elf Bar BC5000")]);
        AuthorizeAsProductViewerOnly();

        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });

        cut.FindComponents<MudChip<string>>().Should().BeEmpty(
            "a view-only staff member cannot flip a product's status, so it renders as text, not a clickable chip");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenUserHoldsEditButNotDelete_BulkBarOffersActivateAndDeactivateButNotDelete()
    {
        var items = new[] { Item("Elf Bar") };
        SetUpKnownProductsList(items);
        AuthorizeAsProductEditorOnly();
        var cut = Render<ManageProducts>();
        await cut.InvokeAsync(() => { });
        await SelectProductsAsync(cut, count: 1);

        cut.Markup.Should().Contain(Strings.ManageProducts_BulkSetActive);
        cut.Markup.Should().Contain(Strings.ManageProducts_BulkSetInactive);
        cut.Markup.Should().NotContain(Strings.ManageProducts_BulkDelete);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_EditControl_LinksToTheProductsIdKeyedEditRoute()
    {
        var item = Item("Elf Bar BC5000");
        SetUpKnownProductsList([item]);

        var cut = await RenderListAsync();

        cut.Find($"[aria-label='{string.Format(Strings.ManageProducts_EditAria, item.Name)}']")
           .GetAttribute("href").Should().Be(Routes.Admin.EditProduct(item.Id));
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenUserHoldsProductsCreatePermission_ShowsTheAddProductButtonLinkedToTheAddRoute()
    {
        var cut = await RenderListAsync();

        var addLink = cut.FindAll("a").Single(a => a.TextContent.Contains(Strings.AddProduct_Heading));
        addLink.GetAttribute("href").Should().Be(Routes.Admin.AddProduct);
    }

    // =========================================================================
    // No products at all — the bare empty state (Figma 2629:4487)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenNoProductsExistYet_ShowsTheEmptyStateMessageAndHidesTheFilterPanel()
    {
        SetUpKnownProductsList([]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.ManageProducts_EmptyTitle);
        cut.Markup.Should().Contain(Strings.ManageProducts_EmptyDescription);
        cut.FindComponents<ShopFilterPanel>().Should().BeEmpty();
    }

    // =========================================================================
    // No matches under active criteria — distinct from the empty catalogue (AC-21)
    // =========================================================================

    /// <summary>
    /// The catalogue itself is non-empty (so the search field renders on first load), but any
    /// search term narrows the result to zero rows — the in-grid no-match state, not the bare
    /// empty-catalogue state.
    /// </summary>
    private void SetUpProductsThatDisappearUnderSearch()
    {
        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetAdminProductsPageQuery>();
                _receivedQueries.Add(query);
                var items = query.Search is null ? [Item("Elf Bar")] : Array.Empty<ProductListItemDto>();
                return Task.FromResult(Result.Ok(new PagedResult<ProductListItemDto>(
                    items, query.Pagination.Page, query.Pagination.PageSize, items.Length)));
            });
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Render_WhenSearchMatchesNothing_ShowsTheNoMatchMessageAndKeepsTheFilterPanel()
    {
        SetUpProductsThatDisappearUnderSearch();
        var cut = await RenderListAsync();

        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("nonexistent"));

        cut.Markup.Should().Contain(Strings.ManageProducts_NoMatchTitle);
        cut.Markup.Should().Contain(Strings.ManageProducts_NoMatchDescription);
        cut.FindComponents<ShopFilterPanel>().Should().NotBeEmpty(
            "a criteria-narrowed empty result keeps the filter panel available, unlike the bare empty catalogue");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task ClearFilters_FromTheNoMatchState_RestoresUnrestrictedResults()
    {
        SetUpProductsThatDisappearUnderSearch();
        var cut = await RenderListAsync();
        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("nonexistent"));

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.OnClearFilters.InvokeAsync());

        cut.Markup.Should().Contain("Elf Bar");
        _receivedQueries.Last().Search.Should().BeNull();
    }

    // =========================================================================
    // Delete a single, unreferenced product (AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteSingle_WhenConfirmed_SendsDeleteProductsCommandWithThatProductsId()
    {
        var cut = await RenderListAsync();
        var product = cut.FindComponent<MudTable<ProductListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductDeletionOutcomeDto(1, [])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageProducts_DeleteAria, product.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new MouseEventArgs()));

        await _mediator.Received(1).Send(
            Arg.Is<DeleteProductsCommand>(c => c.ProductIds.Count == 1 && c.ProductIds[0] == product.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteSingle_WhenCancelled_DoesNotSendTheCommand()
    {
        var cut = await RenderListAsync();
        var product = cut.FindComponent<MudTable<ProductListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: false);

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageProducts_DeleteAria, product.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new MouseEventArgs()));

        await _mediator.DidNotReceive().Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Deleting the last product on the last page recovers to the new last page (AC-22, Decision 12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteSingle_WhenItEmptiesTheLastPage_RecoversToTheNewLastPage()
    {
        var totalCount = 11;
        _mediator.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetAdminProductsPageQuery>();
                _receivedQueries.Add(query);
                var itemCount = query.Pagination.Page == 1 ? 10 : Math.Max(0, totalCount - 10);
                return Task.FromResult(Result.Ok(BuildPage(query.Pagination, totalCount, itemCount)));
            });
        var cut = await RenderListAsync();
        var pagination = cut.FindComponent<ShopPagination>();
        await cut.InvokeAsync(() => pagination.Instance.PageChanged.InvokeAsync(2));
        var product = cut.FindComponent<MudTable<ProductListItemDto>>().Instance.Items!.Single();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductDeletionOutcomeDto(1, [])));
        totalCount = 10;

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageProducts_DeleteAria, product.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new MouseEventArgs()));

        cut.FindComponent<ShopPagination>().Instance.Page.Should().Be(1,
            "deleting page 2's only remaining product leaves just one page, so the list recovers to it " +
            "rather than showing a stranded empty page");
    }

    // =========================================================================
    // Delete a single product — a technical failure is reported, never claimed as success (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteSingle_WhenTheCommandFails_ShowsTheErrorMessageAndNeverClaimsSuccess()
    {
        var cut = await RenderListAsync();
        var product = cut.FindComponent<MudTable<ProductListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<ProductDeletionOutcomeDto>(ProductErrorKeys.DeleteFailed));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageProducts_DeleteAria, product.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new MouseEventArgs()));

        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error);
        _snackbar.DidNotReceive().Add(Arg.Any<string>(), Severity.Success);
    }

    // =========================================================================
    // Delete a single, referenced product — refused, named with its reference count (AC-15)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task DeleteSingle_WhenTheProductIsReferenced_ShowsTheInUseMessageNamingItAndItsReferenceCount()
    {
        var cut = await RenderListAsync();
        var product = cut.FindComponent<MudTable<ProductListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductDeletionOutcomeDto(0, [new ReferencedProductDto(product.Id, product.Name, 3)])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageProducts_DeleteAria, product.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new MouseEventArgs()));

        _snackbar.Received(1).Add(string.Format(Strings.Product_InUse, product.Name, 3), Severity.Warning);
    }

    // =========================================================================
    // Bulk delete — mixed and all-blocked outcomes (AC-16, AC-17)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkDelete_WhenConfirmed_SendsDeleteProductsCommandWithEverySelectedId()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary"), Item("Geek Bar") };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductDeletionOutcomeDto(3, [])));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _mediator.Received(1).Send(
            Arg.Is<DeleteProductsCommand>(c => c.ProductIds.Count == 3 && items.All(i => c.ProductIds.Contains(i.Id))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkDelete_WithAMixedOutcome_ReportsBothCountsAndKeepsOnlyTheReferencedProductsSelected()
    {
        var deletable = new[] { Item("Elf Bar"), Item("Geek Bar"), Item("Vaporesso") };
        var referenced = new[] { Item("Lost Mary"), Item("Smok") };
        var items = deletable.Concat(referenced).ToArray();
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: items.Length);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new ProductDeletionOutcomeDto(3, [.. referenced.Select(p => new ReferencedProductDto(p.Id, p.Name, 2))]);
        _mediator.Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        _snackbar.Received(1).Add(string.Format(Strings.Product_BulkDeletePartial, 3, 2), Severity.Warning);
        cut.FindComponent<ShopBulkActionBar>().Instance.SelectedCount.Should().Be(2,
            "the referenced products stay selected so the staff member can act on them next (AC-16)");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkDelete_WhenEverySelectedProductIsReferenced_ShowsTheAllBlockedMessage()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary") };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new ProductDeletionOutcomeDto(0, [.. items.Select(i => new ReferencedProductDto(i.Id, i.Name, 1))]);
        _mediator.Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        _snackbar.Received(1).Add(Strings.Product_BulkDeleteAllBlocked, Severity.Warning);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkDelete_WhenCancelled_DoesNotSendTheCommandAndKeepsTheSelection()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary") };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: false);

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _mediator.DidNotReceive().Send(Arg.Any<DeleteProductsCommand>(), Arg.Any<CancellationToken>());
        cut.FindComponent<ShopBulkActionBar>().Instance.SelectedCount.Should().Be(2);
    }

    // =========================================================================
    // Inline status toggle — activate immediate, deactivate confirms (AC-10, AC-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task StatusToggle_WhenActivatingAnInactiveProduct_SendsImmediatelyWithoutConfirmation()
    {
        var items = new[] { Item("Elf Bar", isPublished: false) };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        _mediator.Send(Arg.Any<SetProductStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductStatusChangeDto(1, [])));

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new MouseEventArgs()));

        await _dialogService.DidNotReceive().ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(
            Arg.Is<SetProductStatusCommand>(c => c.ProductIds.Count == 1 && c.ProductIds[0] == items[0].Id && c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task StatusToggle_WhenDeactivatingAnActiveProduct_AsksForConfirmationBeforeSending()
    {
        var items = new[] { Item("Elf Bar", isPublished: true) };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetProductStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductStatusChangeDto(1, [])));

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new MouseEventArgs()));

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(Arg.Is<SetProductStatusCommand>(c => !c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task StatusToggle_WhenDeactivationIsCancelled_DoesNotSendTheCommand()
    {
        var items = new[] { Item("Elf Bar", isPublished: true) };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        SetUpConfirmDialogResult(confirmed: false);

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new MouseEventArgs()));

        await _mediator.DidNotReceive().Send(Arg.Any<SetProductStatusCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Bulk activate/deactivate — activate immediate, deactivate confirms with a count (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkActivate_SendsImmediatelyWithoutConfirmation()
    {
        var items = new[] { Item("Elf Bar", isPublished: false), Item("Lost Mary", isPublished: false) };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 2);
        _mediator.Send(Arg.Any<SetProductStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductStatusChangeDto(2, [])));

        var activateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkSetActive));
        await cut.InvokeAsync(() => activateButton.Instance.OnClick.InvokeAsync());

        await _dialogService.DidNotReceive().ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(
            Arg.Is<SetProductStatusCommand>(c => c.ProductIds.Count == 2 && c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkActivate_WhenSomeAreSkippedForMissingAPrice_ReportsBothTheChangedAndSkippedCounts()
    {
        var items = new[] { Item("Elf Bar", isPublished: false), Item("Lost Mary", isPublished: false) };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 2);
        var skipped = new BlockedProductDto(items[1].Id, items[1].Name);
        _mediator.Send(Arg.Any<SetProductStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductStatusChangeDto(1, [skipped])));

        var activateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkSetActive));
        await cut.InvokeAsync(() => activateButton.Instance.OnClick.InvokeAsync());

        _snackbar.Received(1).Add(string.Format(Strings.ManageProducts_ActivatedSuccess, 1), Severity.Success);
        _snackbar.Received(1).Add(string.Format(Strings.ManageProducts_ActivateSkipped, 1), Severity.Warning);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkDeactivate_WhenConfirmed_SendsWithEverySelectedIdAndReportsHowManyChanged()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary"), Item("Geek Bar") };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetProductStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new ProductStatusChangeDto(3, [])));

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _mediator.Received(1).Send(
            Arg.Is<SetProductStatusCommand>(c => c.ProductIds.Count == 3 && !c.IsActive), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add(string.Format(Strings.ManageProducts_DeactivatedSuccess, 3), Severity.Success);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task BulkDeactivate_WhenCancelled_DoesNotSendTheCommand()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary") };
        SetUpKnownProductsList(items);
        var cut = await RenderListAsync();
        await SelectProductsAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: false);

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageProducts_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _mediator.DidNotReceive().Send(Arg.Any<SetProductStatusCommand>(), Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (defaults, row content, pagination): Render_OnInitialize_QueriesTheFirstPageWithNoNarrowingAndNameAToZ,
//        Render_WhenProductsExist_ShowsEachProductsNameSkuBrandCategoryAndStatus,
//        Render_WhenAProductIsUnpublished_ShowsInactiveStatus, Render_WhenProductsSpanMultiplePages_ShowsPaginationControls
// AC-2: ChangePage_AfterFilteringAndSorting_KeepsBothCriteriaOnTheNewPage
// AC-3: Search_WhenStaffTypesATerm_ReQueriesWithThatTermFromPageOne
// AC-4: ChangeStatusFilter_WhenStaffFiltersByInactive_ReQueriesWithThatStatusFromPageOne,
//        ChangeBrandFilter_WhenStaffTogglesABrand_ReQueriesWithThatBrandIdFromPageOne,
//        ChangePriceRange_WhenStaffSettlesTheSlider_ReQueriesWithThoseBoundsFromPageOne
// AC-5: ChangeSort_WhenStaffReversesTheNameOrder_ReQueriesWithThatSortFromPageOne
// AC-6: ChangeSort_WithProductsSelected_ClearsTheSelectionAndHidesTheBulkBar,
//        CloseBulkBar_WhenStaffDismissesIt_ClearsTheSelectionAndHidesTheBar
// AC-7: SelectAll_OnAPageWithMoreProductsThanFit_SelectsOnlyTheCurrentPagesProducts
// AC-10, AC-11: StatusToggle_WhenActivatingAnInactiveProduct_SendsImmediatelyWithoutConfirmation,
//        StatusToggle_WhenDeactivatingAnActiveProduct_AsksForConfirmationBeforeSending,
//        StatusToggle_WhenDeactivationIsCancelled_DoesNotSendTheCommand
// AC-13: DeleteSingle_WhenConfirmed_SendsDeleteProductsCommandWithThatProductsId
// AC-14: DeleteSingle_WhenCancelled_DoesNotSendTheCommand, BulkDelete_WhenCancelled_DoesNotSendTheCommandAndKeepsTheSelection
// AC-15: DeleteSingle_WhenTheProductIsReferenced_ShowsTheInUseMessageNamingItAndItsReferenceCount
// AC-22: DeleteSingle_WhenItEmptiesTheLastPage_RecoversToTheNewLastPage
// AC-23 (failed action, never a false success): DeleteSingle_WhenTheCommandFails_ShowsTheErrorMessageAndNeverClaimsSuccess
// AC-16: BulkDelete_WhenConfirmed_SendsDeleteProductsCommandWithEverySelectedId,
//        BulkDelete_WithAMixedOutcome_ReportsBothCountsAndKeepsOnlyTheReferencedProductsSelected
// AC-17: BulkDelete_WhenEverySelectedProductIsReferenced_ShowsTheAllBlockedMessage
// AC-18: ManageProducts_Always_CarriesTheProductsViewAuthorizePolicy,
//        Render_WhenUserHoldsOnlyProductsView_DoesNotShowTheAddProductButton,
//        Render_WhenUserHoldsOnlyProductsView_DoesNotShowEditOrDeleteRowControls,
//        Render_WhenUserHoldsOnlyProductsView_RendersStatusAsPlainTextNotAClickableToggle,
//        Render_WhenUserHoldsEditButNotDelete_BulkBarOffersActivateAndDeactivateButNotDelete
// AC-21 (loading/empty/no-match, clearing restores results): Render_WhenNoProductsExistYet_ShowsTheEmptyStateMessageAndHidesTheFilterPanel,
//        Render_WhileTheFirstPageIsStillLoading_ShowsSkeletonsNotAFalseEmptyMessage,
//        Render_WhenSearchMatchesNothing_ShowsTheNoMatchMessageAndKeepsTheFilterPanel,
//        ClearFilters_FromTheNoMatchState_RestoresUnrestrictedResults
// AC-23: BulkActivate_SendsImmediatelyWithoutConfirmation,
//        BulkActivate_WhenSomeAreSkippedForMissingAPrice_ReportsBothTheChangedAndSkippedCounts,
//        BulkDeactivate_WhenConfirmed_SendsWithEverySelectedIdAndReportsHowManyChanged,
//        BulkDeactivate_WhenCancelled_DoesNotSendTheCommand
// AC-24: Render_WhenAProductHasVariants_ShowsTheVariantCountCaption, Render_WhenAProductHasNoVariants_OmitsTheVariantCountCaption,
//        Render_WhenPriceRangeDiffers_ShowsBothEndsFormattedAsARange
// AC-25: Render_WhenAProductHasExactlyOneVariant_ShowsSingularVariantCountWording,
//        Render_WhenEveryVariantSharesThePrice_ShowsOneAmountNotARange
// AC-8 (Add flow reachable; navigation half only — completion/round-trip deferred to E2E):
//        Render_WhenUserHoldsProductsCreatePermission_ShowsTheAddProductButtonLinkedToTheAddRoute
// AC-9 (Edit link id-keyed; navigation half only — completion/round-trip deferred to E2E):
//        Render_EditControl_LinksToTheProductsIdKeyedEditRoute
