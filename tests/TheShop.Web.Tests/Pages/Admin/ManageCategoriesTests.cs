using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.Commands.DeleteCategories;
using TheShop.Application.Features.Categories.Commands.SetCategoryStatus;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Queries.GetCategoriesPage;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="ManageCategories"/> page: that every criteria change (search, status
/// filter, sort, page) actually re-queries the list through the URL round trip, carrying the other
/// active criteria with it and resetting to page 1 where required, and that the transient row
/// selection is cleared on each change (AC-2, AC-3, AC-4, AC-5, AC-27); the full row content and
/// pagination (AC-1); the access boundary and permission-gated controls (AC-20, AC-21, AC-31); the
/// id-keyed edit link (AC-23); single and bulk delete, including the in-use refusal and the
/// partial-success outcome that keeps blocked categories selected (AC-17, AC-19, AC-29, AC-30); and
/// the inline/bulk status toggle, where activating is immediate and deactivating confirms first
/// (AC-16, AC-28).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class ManageCategoriesTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly List<GetCategoriesPageQuery> _receivedQueries = [];

    public ManageCategoriesTests()
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

        _mediator.Send(Arg.Any<GetCategoriesPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetCategoriesPageQuery>();
                _receivedQueries.Add(query);
                return Task.FromResult(Result.Ok(BuildPage(query.Pagination, totalCount: 24, itemCount: 10)));
            });
    }

    private void AuthorizeAsCategoryManager()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(
            PolicyNames.Permission(PermissionCatalogue.Categories.View.Code),
            PolicyNames.Permission(PermissionCatalogue.Categories.Edit.Code),
            PolicyNames.Permission(PermissionCatalogue.Categories.Delete.Code));
    }

    private static PagedResult<CategoryListItemDto> BuildPage(
        PaginationRequest pagination, int totalCount, int itemCount)
    {
        var items = Enumerable.Range(1, itemCount)
            .Select(i => new CategoryListItemDto(
                Guid.NewGuid(), $"Category {i}", $"Description {i}", "https://example.com/image.webp", true, 0))
            .ToList();

        return new PagedResult<CategoryListItemDto>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    private void AuthorizeAsCategoryViewerOnly()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("viewer-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Categories.View.Code));
    }

    private void AuthorizeAsCategoryEditorOnly()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("editor-user");
        authContext.SetPolicies(
            PolicyNames.Permission(PermissionCatalogue.Categories.View.Code),
            PolicyNames.Permission(PermissionCatalogue.Categories.Edit.Code));
    }

    /// <summary>
    /// Configures <see cref="_dialogService"/> so the next <c>ShopConfirmDialog</c> it is asked to
    /// show resolves as either confirmed or cancelled/dismissed, without ever rendering the real
    /// dialog.
    /// </summary>
    private void SetUpConfirmDialogResult(bool confirmed)
    {
        var dialogReference = Substitute.For<IDialogReference>();
        dialogReference.Result.Returns(Task.FromResult<DialogResult?>(confirmed ? DialogResult.Ok(true) : DialogResult.Cancel()));
        _dialogService.ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>())
                      .Returns(Task.FromResult(dialogReference));
    }

    /// <summary>
    /// Makes every <see cref="GetCategoriesPageQuery"/> — the initial load and any subsequent
    /// reload — answer with the exact same, caller-known set of categories, so a mutation's
    /// outcome (e.g. which ids stay selected after a partial bulk delete, AC-29) can be asserted
    /// against ids the test itself chose rather than random ones.
    /// </summary>
    private void SetUpKnownCategoriesList(IReadOnlyList<CategoryListItemDto> items) =>
        _mediator.Send(Arg.Any<GetCategoriesPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetCategoriesPageQuery>();
                _receivedQueries.Add(query);
                return Task.FromResult(Result.Ok(new PagedResult<CategoryListItemDto>(
                    items, query.Pagination.Page, query.Pagination.PageSize, items.Count)));
            });

    private static CategoryListItemDto Item(string name, bool isActive = true, int productCount = 0) =>
        new(Guid.NewGuid(), name, $"{name} description", "https://example.com/image.webp", isActive, productCount);

    private async Task<IRenderedComponent<ManageCategories>> RenderListAsync()
    {
        AuthorizeAsCategoryManager();
        var cut = Render<ManageCategories>();
        await cut.InvokeAsync(() => { });
        _receivedQueries.Clear();
        return cut;
    }

    // =========================================================================
    // Initial load (AC-1 defaults half)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_OnInitialize_QueriesTheFirstPageWithNoStatusFilterAndNameAToZ()
    {
        AuthorizeAsCategoryManager();

        var cut = Render<ManageCategories>();
        await cut.InvokeAsync(() => { });

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Search.Should().BeNull();
        _receivedQueries[0].Status.Should().BeNull("no status selection means categories of every status");
        _receivedQueries[0].Sort.Should().Be(CategorySortOption.NameAToZ);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Criteria changes re-query the list (AC-3, AC-4, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task ChangeStatusFilter_WhenStaffFiltersByInactive_ReQueriesWithThatStatusFromPageOne()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "inactive")));

        _receivedQueries.Should().ContainSingle(
            "changing the status filter must reach the mediator without a page refresh");
        _receivedQueries[0].Status.Should().Be(CategoryStatusFilter.Inactive);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task ChangeStatusFilter_WhenStaffUnchecksTheSelectedStatus_ReQueriesWithNoStatusFilter()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "inactive")));
        _receivedQueries.Clear();

        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", null)));

        _receivedQueries.Should().ContainSingle(
            "clearing the status selection must re-query without a page refresh");
        _receivedQueries[0].Status.Should().BeNull(
            "the list offers no 'All' option — unselecting a status is what asks for every status (RULE-16)");
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task ChangeSort_WhenStaffReversesTheNameOrder_ReQueriesWithThatSortFromPageOne()
    {
        var cut = await RenderListAsync();

        var sortSelect = cut.FindComponent<ShopSortSelect<CategorySortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(CategorySortOption.NameZToA));

        _receivedQueries.Should().ContainSingle(
            "changing the sort order must reach the mediator without a page refresh");
        _receivedQueries[0].Sort.Should().Be(CategorySortOption.NameZToA);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task ChangeSort_WhenStaffChoosesNewest_ReQueriesWithNewestFromPageOne()
    {
        var cut = await RenderListAsync();

        var sortSelect = cut.FindComponent<ShopSortSelect<CategorySortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(CategorySortOption.NewestFirst));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Sort.Should().Be(CategorySortOption.NewestFirst);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Search_WhenStaffTypesATerm_ReQueriesWithThatTermFromPageOne()
    {
        var cut = await RenderListAsync();

        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("  DIS  "));

        _receivedQueries.Should().ContainSingle(
            "searching must reach the mediator without a page refresh");
        _receivedQueries[0].Search.Should().Be("DIS", "the term is trimmed before it is queried");
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Pagination preserves the active criteria (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task ChangePage_AfterFilteringAndSorting_KeepsBothCriteriaOnTheNewPage()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "active")));

        var sortSelect = cut.FindComponent<ShopSortSelect<CategorySortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(CategorySortOption.NameZToA));
        _receivedQueries.Clear();

        var pagination = cut.FindComponent<ShopPagination>();
        await cut.InvokeAsync(() => pagination.Instance.PageChanged.InvokeAsync(2));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Pagination.Page.Should().Be(2);
        _receivedQueries[0].Status.Should().Be(CategoryStatusFilter.Active);
        _receivedQueries[0].Sort.Should().Be(CategorySortOption.NameZToA);
    }

    // =========================================================================
    // Clearing the filters
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task ClearFilters_AfterSearchingAndFiltering_ReQueriesWithNoSearchAndNoStatusFilter()
    {
        var cut = await RenderListAsync();

        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("dis"));

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "inactive")));
        _receivedQueries.Clear();

        await cut.InvokeAsync(() => filterPanel.Instance.OnClearFilters.InvokeAsync());

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Search.Should().BeNull();
        _receivedQueries[0].Status.Should().BeNull();
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Selection is cleared by every criteria change (AC-27, RULE-14)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task ChangeSort_WithCategoriesSelected_ClearsTheSelectionAndHidesTheBulkBar()
    {
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 2);

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeTrue();

        var sortSelect = cut.FindComponent<ShopSortSelect<CategorySortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(CategorySortOption.NameZToA));

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeFalse();
    }

    // =========================================================================
    // Dismissing the bulk bar
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CloseBulkBar_WhenStaffDismissesIt_ClearsTheSelectionAndHidesTheBar()
    {
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 3);

        var bar = cut.FindComponent<ShopBulkActionBar>();
        bar.Instance.Visible.Should().BeTrue();
        bar.Instance.SelectedCount.Should().Be(3);

        await cut.InvokeAsync(() => bar.Instance.OnClose.InvokeAsync());

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeFalse(
            "dismissing the bar must drop the selection that put it up");
        cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.SelectedItems.Should().BeEmpty();
        _receivedQueries.Should().BeEmpty("dismissing a selection is local UI state, not a re-query");
    }

    private static async Task SelectCategoriesAsync(IRenderedComponent<ManageCategories> cut, int count)
    {
        var table = cut.FindComponent<MudTable<CategoryListItemDto>>();
        var selected = table.Instance.Items!.Take(count).ToHashSet();
        await cut.InvokeAsync(() => table.Instance.SelectedItemsChanged.InvokeAsync(selected));
    }

    // =========================================================================
    // Full row content (AC-1) and pagination controls (AC-1, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenCategoriesExist_ShowsEachCategorysNameAndDescription()
    {
        var cut = await RenderListAsync();

        var firstCategory = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        cut.Markup.Should().Contain(firstCategory.Name);
        cut.Markup.Should().Contain(firstCategory.Description!);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenCategoriesExist_ShowsEachCategorysImage()
    {
        var cut = await RenderListAsync();

        var firstCategory = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        cut.FindAll("img").Should().Contain(img => img.GetAttribute("src") == firstCategory.ImageUrl);
        cut.FindComponents<ShopImage>().Should().Contain(image =>
            image.Instance.Src == firstCategory.ImageUrl && image.Instance.Treatment == ShopImageTreatment.Thumbnail);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenCategoriesSpanMultiplePages_ShowsPaginationControls()
    {
        // The constructor's default page reports a total of 24 categories at 10 per page — 3 pages.
        var cut = await RenderListAsync();

        cut.FindComponent<ShopPagination>().Instance.TotalPages.Should().Be(3);
    }

    // =========================================================================
    // No categories exist yet (edge case, AC-22)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenNoCategoriesExistYet_ShowsTheEmptyStateMessage()
    {
        SetUpKnownCategoriesList([]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.ManageCategories_EmptyTitle);
        cut.Markup.Should().Contain(Strings.ManageCategories_EmptyDescription);
    }

    // =========================================================================
    // Search or filter matches nothing (edge case, AC-22)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenSearchMatchesNoCategories_ShowsTheNoMatchMessage()
    {
        SetUpKnownCategoriesList([]);
        var cut = await RenderListAsync();

        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("nonexistent"));

        cut.Markup.Should().Contain(Strings.ManageCategories_NoMatchTitle);
        cut.Markup.Should().Contain(Strings.ManageCategories_NoMatchDescription);
    }

    // =========================================================================
    // Access boundary (AC-21)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ManageCategories_Always_CarriesTheCategoriesViewAuthorizePolicy()
    {
        var attributes = typeof(ManageCategories).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Categories.View.Code),
            "the route-level policy is the only gate between a non-viewer and the list — " +
            "App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // View-only staff member sees no add/edit/status-flip/delete control (AC-20)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenUserHoldsOnlyCategoriesView_DoesNotShowTheAddCategoryButton()
    {
        AuthorizeAsCategoryViewerOnly();

        var cut = Render<ManageCategories>();
        await cut.InvokeAsync(() => { });

        cut.Markup.Should().NotContain(Strings.AddCategory_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenUserHoldsOnlyCategoriesView_DoesNotShowEditOrDeleteRowControls()
    {
        AuthorizeAsCategoryViewerOnly();

        var cut = Render<ManageCategories>();
        await cut.InvokeAsync(() => { });

        var category = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        cut.FindAll($"[aria-label='{string.Format(Strings.ManageCategories_EditAria, category.Name)}']").Should().BeEmpty();
        cut.FindAll($"[aria-label='{string.Format(Strings.ManageCategories_DeleteAria, category.Name)}']").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenUserHoldsOnlyCategoriesView_RendersStatusAsPlainTextNotAClickableToggle()
    {
        AuthorizeAsCategoryViewerOnly();

        var cut = Render<ManageCategories>();
        await cut.InvokeAsync(() => { });

        cut.FindComponents<MudChip<string>>().Should().BeEmpty(
            "a view-only staff member cannot flip a category's status, so it renders as text, not a clickable chip");
    }

    // =========================================================================
    // Edit-but-not-delete staff member sees Activate/Deactivate but no bulk Delete (AC-31)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_WhenUserHoldsEditButNotDelete_BulkBarOffersActivateAndDeactivateButNotDelete()
    {
        var items = new[] { Item("Disposables") };
        SetUpKnownCategoriesList(items);
        AuthorizeAsCategoryEditorOnly();
        var cut = Render<ManageCategories>();
        await cut.InvokeAsync(() => { });
        await SelectCategoriesAsync(cut, count: 1);

        cut.Markup.Should().Contain(Strings.ManageCategories_BulkSetActive);
        cut.Markup.Should().Contain(Strings.ManageCategories_BulkSetInactive);
        cut.Markup.Should().NotContain(Strings.ManageCategories_BulkDelete);
    }

    // =========================================================================
    // Edit link addresses the category by id (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Render_EditControl_LinksToTheCategorysIdKeyedEditRoute()
    {
        var cut = await RenderListAsync();

        var category = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        cut.Find($"[aria-label='{string.Format(Strings.ManageCategories_EditAria, category.Name)}']")
           .GetAttribute("href").Should().Be(Routes.Admin.EditCategory(category.Id));
    }

    // =========================================================================
    // Delete a single, unused category (AC-17, RULE-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteSingle_WhenConfirmed_SendsDeleteCategoriesCommandWithThatCategorysId()
    {
        var cut = await RenderListAsync();
        var category = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryDeletionOutcomeDto(1, [])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageCategories_DeleteAria, category.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _mediator.Received(1).Send(
            Arg.Is<DeleteCategoriesCommand>(c => c.CategoryIds.Count == 1 && c.CategoryIds[0] == category.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteSingle_WhenActivated_ShowsAConfirmDialogNamingTheCategory()
    {
        var cut = await RenderListAsync();
        var category = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryDeletionOutcomeDto(1, [])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageCategories_DeleteAria, category.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(
            Arg.Any<string>(),
            Arg.Is<DialogParameters>(p => p.Get<string>("BodyText")!.Contains(category.Name)));
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteSingle_WhenCancelled_DoesNotSendTheCommand()
    {
        var cut = await RenderListAsync();
        var category = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: false);

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageCategories_DeleteAria, category.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _mediator.DidNotReceive().Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Delete a single, in-use category — refused, named (AC-19)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteSingle_WhenTheCategoryIsInUse_ShowsTheInUseMessageNamingItAndItsProductCount()
    {
        var cut = await RenderListAsync();
        var category = cut.FindComponent<MudTable<CategoryListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryDeletionOutcomeDto(0, [new BlockedCategoryDto(category.Id, category.Name, 3)])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageCategories_DeleteAria, category.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        _snackbar.Received(1).Add(string.Format(Strings.Category_InUse, category.Name, 3), Severity.Warning);
    }

    // =========================================================================
    // Bulk delete — mixed and all-blocked outcomes (AC-29, AC-30)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDelete_WhenConfirmed_SendsDeleteCategoriesCommandWithEverySelectedId()
    {
        var items = new[] { Item("Disposables"), Item("Pod Systems"), Item("E-Liquids") };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryDeletionOutcomeDto(3, [])));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _mediator.Received(1).Send(
            Arg.Is<DeleteCategoriesCommand>(c => c.CategoryIds.Count == 3 && items.All(i => c.CategoryIds.Contains(i.Id))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDelete_WhenActivated_AsksForConfirmationStatingHowManyWillBeDeleted()
    {
        var items = new[] { Item("Disposables"), Item("Pod Systems") };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryDeletionOutcomeDto(2, [])));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(
            Arg.Any<string>(),
            Arg.Is<DialogParameters>(p => p.Get<string>("BodyText")!.Contains("2")));
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDelete_WhenCancelled_DoesNotSendTheCommandAndKeepsTheSelection()
    {
        var items = new[] { Item("Disposables"), Item("Pod Systems") };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: false);

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _mediator.DidNotReceive().Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>());
        cut.FindComponent<ShopBulkActionBar>().Instance.SelectedCount.Should().Be(2,
            "dismissing the confirmation must leave the selection untouched so it can be retried (AC-18)");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDelete_WithAMixedOutcome_ReportsBothCounts()
    {
        var deletable = new[] { Item("Disposables"), Item("E-Liquids"), Item("Coils") };
        var blocked = new[] { Item("Pod Systems"), Item("Starter Kits") };
        var items = deletable.Concat(blocked).ToArray();
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: items.Length);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new CategoryDeletionOutcomeDto(3, [.. blocked.Select(b => new BlockedCategoryDto(b.Id, b.Name, 2))]);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        _snackbar.Received(1).Add(string.Format(Strings.Category_BulkDeletePartial, 3, 2), Severity.Warning);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDelete_WithAMixedOutcome_KeepsOnlyTheBlockedCategoriesSelected()
    {
        var deletable = new[] { Item("Disposables"), Item("E-Liquids"), Item("Coils") };
        var blocked = new[] { Item("Pod Systems"), Item("Starter Kits") };
        var items = deletable.Concat(blocked).ToArray();
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: items.Length);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new CategoryDeletionOutcomeDto(3, [.. blocked.Select(b => new BlockedCategoryDto(b.Id, b.Name, 2))]);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        cut.FindComponent<ShopBulkActionBar>().Instance.SelectedCount.Should().Be(2,
            "the blocked categories stay selected so the staff member can act on them next (AC-29)");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDelete_WhenEverySelectedCategoryIsInUse_ShowsTheAllBlockedMessage()
    {
        var items = new[] { Item("Disposables"), Item("Pod Systems") };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new CategoryDeletionOutcomeDto(0, [.. items.Select(i => new BlockedCategoryDto(i.Id, i.Name, 1))]);
        _mediator.Send(Arg.Any<DeleteCategoriesCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        _snackbar.Received(1).Add(Strings.Category_BulkDeleteAllBlocked, Severity.Warning);
    }

    // =========================================================================
    // Inline status toggle — activate immediate, deactivate confirms (AC-16, RULE-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task StatusToggle_WhenActivatingAnInactiveCategory_SendsImmediatelyWithoutConfirmation()
    {
        var items = new[] { Item("Disposables", isActive: false) };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        _mediator.Send(Arg.Any<SetCategoryStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryStatusChangeDto(1)));

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _dialogService.DidNotReceive().ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(
            Arg.Is<SetCategoryStatusCommand>(c => c.CategoryIds.Count == 1 && c.CategoryIds[0] == items[0].Id && c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task StatusToggle_WhenDeactivatingAnActiveCategory_AsksForConfirmationBeforeSending()
    {
        var items = new[] { Item("Disposables", isActive: true) };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetCategoryStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryStatusChangeDto(1)));

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(Arg.Is<SetCategoryStatusCommand>(c => !c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task StatusToggle_WhenDeactivationIsCancelled_DoesNotSendTheCommand()
    {
        var items = new[] { Item("Disposables", isActive: true) };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        SetUpConfirmDialogResult(confirmed: false);

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _mediator.DidNotReceive().Send(Arg.Any<SetCategoryStatusCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Bulk activate/deactivate — activate immediate, deactivate confirms with a count (AC-28, RULE-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkActivate_SendsImmediatelyWithoutConfirmation()
    {
        var items = new[] { Item("Disposables", isActive: false), Item("Pod Systems", isActive: false) };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 2);
        _mediator.Send(Arg.Any<SetCategoryStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryStatusChangeDto(2)));

        var activateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkSetActive));
        await cut.InvokeAsync(() => activateButton.Instance.OnClick.InvokeAsync());

        await _dialogService.DidNotReceive().ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(
            Arg.Is<SetCategoryStatusCommand>(c => c.CategoryIds.Count == 2 && c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDeactivate_WhenConfirmed_SendsWithEverySelectedIdAndReportsHowManyChanged()
    {
        var items = new[] { Item("Disposables"), Item("Pod Systems"), Item("E-Liquids") };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetCategoryStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryStatusChangeDto(3)));

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _mediator.Received(1).Send(
            Arg.Is<SetCategoryStatusCommand>(c => c.CategoryIds.Count == 3 && !c.IsActive), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add(string.Format(Strings.ManageCategories_DeactivatedSuccess, 3), Severity.Success);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDeactivate_WhenActivated_AsksForConfirmationStatingHowManyWillBeHidden()
    {
        var items = new[] { Item("Disposables"), Item("Pod Systems"), Item("E-Liquids") };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetCategoryStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CategoryStatusChangeDto(3)));

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(
            Arg.Any<string>(),
            Arg.Is<DialogParameters>(p => p.Get<string>("BodyText")!.Contains("3")));
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task BulkDeactivate_WhenCancelled_DoesNotSendTheCommand()
    {
        var items = new[] { Item("Disposables"), Item("Pod Systems") };
        SetUpKnownCategoriesList(items);
        var cut = await RenderListAsync();
        await SelectCategoriesAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: false);

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageCategories_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _mediator.DidNotReceive().Send(Arg.Any<SetCategoryStatusCommand>(), Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (defaults half): Render_OnInitialize_QueriesTheFirstPageWithNoStatusFilterAndNameAToZ
// AC-1 (row content + pagination half): Render_WhenCategoriesExist_ShowsEachCategorysNameAndDescription,
//        Render_WhenCategoriesExist_ShowsEachCategorysImage, Render_WhenCategoriesSpanMultiplePages_ShowsPaginationControls
// AC-2: ChangePage_AfterFilteringAndSorting_KeepsBothCriteriaOnTheNewPage
// AC-3: Search_WhenStaffTypesATerm_ReQueriesWithThatTermFromPageOne
// AC-4: ChangeStatusFilter_WhenStaffFiltersByInactive_ReQueriesWithThatStatusFromPageOne,
//        ChangeStatusFilter_WhenStaffUnchecksTheSelectedStatus_ReQueriesWithNoStatusFilter
// AC-5: ChangeSort_WhenStaffReversesTheNameOrder_ReQueriesWithThatSortFromPageOne
// AC-16: StatusToggle_WhenActivatingAnInactiveCategory_SendsImmediatelyWithoutConfirmation,
//        StatusToggle_WhenDeactivatingAnActiveCategory_AsksForConfirmationBeforeSending,
//        StatusToggle_WhenDeactivationIsCancelled_DoesNotSendTheCommand
// AC-17: DeleteSingle_WhenConfirmed_SendsDeleteCategoriesCommandWithThatCategorysId,
//        DeleteSingle_WhenActivated_ShowsAConfirmDialogNamingTheCategory
// AC-18: DeleteSingle_WhenCancelled_DoesNotSendTheCommand, BulkDelete_WhenCancelled_DoesNotSendTheCommandAndKeepsTheSelection
// AC-19: DeleteSingle_WhenTheCategoryIsInUse_ShowsTheInUseMessageNamingItAndItsProductCount
// AC-20: Render_WhenUserHoldsOnlyCategoriesView_DoesNotShowTheAddCategoryButton,
//        Render_WhenUserHoldsOnlyCategoriesView_DoesNotShowEditOrDeleteRowControls,
//        Render_WhenUserHoldsOnlyCategoriesView_RendersStatusAsPlainTextNotAClickableToggle
// AC-21: ManageCategories_Always_CarriesTheCategoriesViewAuthorizePolicy
// AC-22 (empty state half): Render_WhenNoCategoriesExistYet_ShowsTheEmptyStateMessage
// AC-22 (no-match half): Render_WhenSearchMatchesNoCategories_ShowsTheNoMatchMessage
// AC-23: Render_EditControl_LinksToTheCategorysIdKeyedEditRoute
// AC-27: ChangeSort_WithCategoriesSelected_ClearsTheSelectionAndHidesTheBulkBar,
//        CloseBulkBar_WhenStaffDismissesIt_ClearsTheSelectionAndHidesTheBar
// AC-28: BulkActivate_SendsImmediatelyWithoutConfirmation,
//        BulkDeactivate_WhenConfirmed_SendsWithEverySelectedIdAndReportsHowManyChanged,
//        BulkDeactivate_WhenActivated_AsksForConfirmationStatingHowManyWillBeHidden,
//        BulkDeactivate_WhenCancelled_DoesNotSendTheCommand
// AC-29: BulkDelete_WhenConfirmed_SendsDeleteCategoriesCommandWithEverySelectedId,
//        BulkDelete_WhenActivated_AsksForConfirmationStatingHowManyWillBeDeleted,
//        BulkDelete_WithAMixedOutcome_ReportsBothCounts, BulkDelete_WithAMixedOutcome_KeepsOnlyTheBlockedCategoriesSelected
// AC-30: BulkDelete_WhenEverySelectedCategoryIsInUse_ShowsTheAllBlockedMessage
// AC-31: Render_WhenUserHoldsEditButNotDelete_BulkBarOffersActivateAndDeactivateButNotDelete
// AC-32: ChangeSort_WhenStaffChoosesNewest_ReQueriesWithNewestFromPageOne
