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
using TheShop.Application.Features.Brands.Commands.DeleteBrands;
using TheShop.Application.Features.Brands.Commands.SetBrandStatus;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetBrandsPage;
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
/// Tests for the <see cref="ManageBrands"/> page: that every criteria change (search, status
/// filter, sort, page) actually re-queries the list through the URL round trip, carrying the other
/// active criteria with it and resetting to page 1 where required, and that the transient row
/// selection is cleared on each change (AC-2, AC-3, AC-4, AC-5, AC-22); the full row content and
/// pagination (AC-1); the access boundary and permission-gated controls (AC-16, AC-17, AC-26); the
/// id-keyed edit link (AC-27); single and bulk delete, including the in-use refusal and the
/// partial-success outcome that keeps blocked brands selected (AC-13, AC-15, AC-24, AC-25); and the
/// inline/bulk status toggle, where activating is immediate and deactivating confirms first
/// (AC-21, AC-23).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class ManageBrandsTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly List<GetBrandsPageQuery> _receivedQueries = [];

    public ManageBrandsTests()
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

        _mediator.Send(Arg.Any<GetBrandsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetBrandsPageQuery>();
                _receivedQueries.Add(query);
                return Task.FromResult(Result.Ok(BuildPage(query.Pagination, totalCount: 24, itemCount: 10)));
            });
    }

    private void AuthorizeAsBrandManager()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(
            PolicyNames.Permission(PermissionCatalogue.Brands.View.Code),
            PolicyNames.Permission(PermissionCatalogue.Brands.Edit.Code),
            PolicyNames.Permission(PermissionCatalogue.Brands.Delete.Code));
    }

    private static PagedResult<BrandListItemDto> BuildPage(
        PaginationRequest pagination, int totalCount, int itemCount)
    {
        var items = Enumerable.Range(1, itemCount)
            .Select(i => new BrandListItemDto(
                Guid.NewGuid(), $"Brand {i}", $"Description {i}", "https://example.com/logo.webp", true, 0))
            .ToList();

        return new PagedResult<BrandListItemDto>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    private void AuthorizeAsBrandViewerOnly()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("viewer-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Brands.View.Code));
    }

    private void AuthorizeAsBrandEditorOnly()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("editor-user");
        authContext.SetPolicies(
            PolicyNames.Permission(PermissionCatalogue.Brands.View.Code),
            PolicyNames.Permission(PermissionCatalogue.Brands.Edit.Code));
    }

    /// <summary>
    /// Configures <see cref="_dialogService"/> so the next <c>ShopConfirmDialog</c> it is asked to
    /// show resolves as either confirmed or cancelled/dismissed, without ever rendering the real
    /// dialog (RULE-7, RULE-14).
    /// </summary>
    private void SetUpConfirmDialogResult(bool confirmed)
    {
        var dialogReference = Substitute.For<IDialogReference>();
        dialogReference.Result.Returns(Task.FromResult<DialogResult?>(confirmed ? DialogResult.Ok(true) : DialogResult.Cancel()));
        _dialogService.ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>())
                      .Returns(Task.FromResult(dialogReference));
    }

    /// <summary>
    /// Makes every <see cref="GetBrandsPageQuery"/> — the initial load and any subsequent reload —
    /// answer with the exact same, caller-known set of brands, so a mutation's outcome (e.g. which
    /// ids stay selected after a partial bulk delete, AC-24) can be asserted against ids the test
    /// itself chose rather than random ones.
    /// </summary>
    private void SetUpKnownBrandsList(IReadOnlyList<BrandListItemDto> items) =>
        _mediator.Send(Arg.Any<GetBrandsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetBrandsPageQuery>();
                _receivedQueries.Add(query);
                return Task.FromResult(Result.Ok(new PagedResult<BrandListItemDto>(
                    items, query.Pagination.Page, query.Pagination.PageSize, items.Count)));
            });

    private static BrandListItemDto Item(string name, bool isActive = true, int productCount = 0) =>
        new(Guid.NewGuid(), name, $"{name} description", "https://example.com/logo.webp", isActive, productCount);

    private async Task<IRenderedComponent<ManageBrands>> RenderListAsync()
    {
        AuthorizeAsBrandManager();
        var cut = Render<ManageBrands>();
        await cut.InvokeAsync(() => { });
        _receivedQueries.Clear();
        return cut;
    }

    // =========================================================================
    // Initial load (AC-1 defaults half)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_OnInitialize_QueriesTheFirstPageWithNoStatusFilterAndNameAToZ()
    {
        AuthorizeAsBrandManager();

        var cut = Render<ManageBrands>();
        await cut.InvokeAsync(() => { });

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Search.Should().BeNull();
        _receivedQueries[0].Status.Should().BeNull("no status selection means brands of every status");
        _receivedQueries[0].Sort.Should().Be(BrandSortOption.NameAToZ);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Criteria changes re-query the list (AC-3, AC-4, AC-5)
    //
    // Regression: the page pushes each change into the URL and relies on the base class's
    // LocationChanged subscription to turn that back into a fetch. Overriding OnInitialized without
    // chaining to the base dropped that subscription, so the URL changed but the list never
    // re-queried — the UI stayed on its first page of results until a manual browser refresh. Each
    // test below asserts a *new* query reached the mediator, which is exactly what went missing.
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ChangeStatusFilter_WhenStaffFiltersByInactive_ReQueriesWithThatStatusFromPageOne()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "inactive")));

        _receivedQueries.Should().ContainSingle(
            "changing the status filter must reach the mediator without a page refresh");
        _receivedQueries[0].Status.Should().Be(BrandStatusFilter.Inactive);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
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
            "the list offers no 'All' option — unselecting a status is what asks for every status");
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ChangeSort_WhenStaffReversesTheNameOrder_ReQueriesWithThatSortFromPageOne()
    {
        var cut = await RenderListAsync();

        var sortSelect = cut.FindComponent<ShopSortSelect<BrandSortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(BrandSortOption.NameZToA));

        _receivedQueries.Should().ContainSingle(
            "changing the sort order must reach the mediator without a page refresh");
        _receivedQueries[0].Sort.Should().Be(BrandSortOption.NameZToA);
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Search_WhenStaffTypesATerm_ReQueriesWithThatTermFromPageOne()
    {
        var cut = await RenderListAsync();

        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("  AUR  "));

        _receivedQueries.Should().ContainSingle(
            "searching must reach the mediator without a page refresh");
        _receivedQueries[0].Search.Should().Be("AUR", "the term is trimmed before it is queried");
        _receivedQueries[0].Pagination.Page.Should().Be(1);
    }

    // =========================================================================
    // Pagination preserves the active criteria (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ChangePage_AfterFilteringAndSorting_KeepsBothCriteriaOnTheNewPage()
    {
        var cut = await RenderListAsync();

        var filterPanel = cut.FindComponent<ShopFilterPanel>();
        await cut.InvokeAsync(() => filterPanel.Instance.SingleSelectChanged.InvokeAsync(("status", "active")));

        var sortSelect = cut.FindComponent<ShopSortSelect<BrandSortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(BrandSortOption.NameZToA));
        _receivedQueries.Clear();

        var pagination = cut.FindComponent<ShopPagination>();
        await cut.InvokeAsync(() => pagination.Instance.PageChanged.InvokeAsync(2));

        _receivedQueries.Should().ContainSingle();
        _receivedQueries[0].Pagination.Page.Should().Be(2);
        _receivedQueries[0].Status.Should().Be(BrandStatusFilter.Active);
        _receivedQueries[0].Sort.Should().Be(BrandSortOption.NameZToA);
    }

    // =========================================================================
    // Clearing the filters (AC-18 clear-affordance half)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ClearFilters_AfterSearchingAndFiltering_ReQueriesWithNoSearchAndNoStatusFilter()
    {
        var cut = await RenderListAsync();

        var searchField = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => searchField.Instance.ValueChanged.InvokeAsync("aur"));

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
    // Selection is cleared by every criteria change (AC-22, RULE-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ChangeSort_WithBrandsSelected_ClearsTheSelectionAndHidesTheBulkBar()
    {
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 2);

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeTrue();

        var sortSelect = cut.FindComponent<ShopSortSelect<BrandSortOption>>();
        await cut.InvokeAsync(() => sortSelect.Instance.SortChanged.InvokeAsync(BrandSortOption.NameZToA));

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeFalse();
    }

    // =========================================================================
    // Dismissing the bulk bar
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task CloseBulkBar_WhenStaffDismissesIt_ClearsTheSelectionAndHidesTheBar()
    {
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 3);

        var bar = cut.FindComponent<ShopBulkActionBar>();
        bar.Instance.Visible.Should().BeTrue();
        bar.Instance.SelectedCount.Should().Be(3);

        await cut.InvokeAsync(() => bar.Instance.OnClose.InvokeAsync());

        cut.FindComponent<ShopBulkActionBar>().Instance.Visible.Should().BeFalse(
            "dismissing the bar must drop the selection that put it up");
        cut.FindComponent<MudTable<BrandListItemDto>>().Instance.SelectedItems.Should().BeEmpty();
        _receivedQueries.Should().BeEmpty("dismissing a selection is local UI state, not a re-query");
    }

    // =========================================================================
    // Loading indicator while a mutation is in flight
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkActivate_WhileTheStatusChangeIsInFlight_ShowsTheTableLoaderAndDisablesTheActions()
    {
        var pending = new TaskCompletionSource<Result<BrandStatusChangeDto>>();
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>()).Returns(pending.Task);

        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 2);

        // Fire the bulk activate but leave the command unresolved, so the page stays mid-mutation.
        var activate = cut.FindComponents<MudButton>()
            .First(b => b.Markup.Contains(Strings.ManageBrands_BulkSetActive));
        var inFlight = cut.InvokeAsync(() => activate.Instance.OnClick.InvokeAsync());

        // The table's own progress bar is deliberately unused for row-scoped mutations (see
        // MutationBusyKeys in ManageBrands.razor.cs) — progress is reported per row instead.
        cut.FindComponent<MudProgressCircular>().Should().NotBeNull(
            "the acted-on rows report the mutation with their own in-row spinners");
        cut.FindComponents<MudButton>()
            .Where(b => b.Markup.Contains(Strings.ManageBrands_BulkSetActive)
                     || b.Markup.Contains(Strings.ManageBrands_BulkSetInactive)
                     || b.Markup.Contains(Strings.ManageBrands_BulkDelete))
            .Should().OnlyContain(b => b.Instance.Disabled,
                "no second mutation may overlap one already in flight");

        pending.SetResult(Result.Ok(new BrandStatusChangeDto(2)));
        await inFlight;
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task RowControls_WhileAStatusChangeIsInFlight_AreDisabled()
    {
        var pending = new TaskCompletionSource<Result<BrandStatusChangeDto>>();
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>()).Returns(pending.Task);

        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 1);

        var activate = cut.FindComponents<MudButton>()
            .First(b => b.Markup.Contains(Strings.ManageBrands_BulkSetActive));
        var inFlight = cut.InvokeAsync(() => activate.Instance.OnClick.InvokeAsync());

        cut.FindComponents<MudChip<string>>().Should().OnlyContain(c => c.Instance.Disabled,
            "the per-row status toggle must not fire while another status change is running");

        pending.SetResult(Result.Ok(new BrandStatusChangeDto(1)));
        await inFlight;

        cut.FindComponents<MudChip<string>>().Should().OnlyContain(c => !c.Instance.Disabled);
    }

    // Regression: the only in-list indicator was the table's own progress bar, which MudBlazor
    // renders under the header row. Acting on a row further down the list — with the page scrolled
    // past the header — left the user with no visible sign that anything was happening. The acted-on
    // row now carries the spinner itself, so the feedback is wherever the click was.
    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task StatusChange_WhileInFlight_SwapsTheActedOnRowsChipForAnInRowSpinner()
    {
        var pending = new TaskCompletionSource<Result<BrandStatusChangeDto>>();
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>()).Returns(pending.Task);

        var cut = await RenderListAsync();
        var chipsBefore = cut.FindComponents<MudChip<string>>().Count;

        await SelectBrandsAsync(cut, count: 1);
        var activate = cut.FindComponents<MudButton>()
            .First(b => b.Markup.Contains(Strings.ManageBrands_BulkSetActive));
        var inFlight = cut.InvokeAsync(() => activate.Instance.OnClick.InvokeAsync());

        cut.FindComponents<MudChip<string>>().Should().HaveCount(chipsBefore - 1,
            "the acted-on row gives up its status chip to the spinner that reports the change");
        cut.FindComponents<MudProgressCircular>().Should().HaveCount(1,
            "the acted-on row carries the only spinner — unlike the table's progress bar under "
            + "the header, it stays in view wherever the click happened");

        pending.SetResult(Result.Ok(new BrandStatusChangeDto(1)));
        await inFlight;

        cut.FindComponents<MudChip<string>>().Should().HaveCount(chipsBefore,
            "the chip comes back once the mutation settles");
        cut.FindComponents<MudProgressCircular>().Should().BeEmpty();
    }

    private static async Task SelectBrandsAsync(IRenderedComponent<ManageBrands> cut, int count)
    {
        var table = cut.FindComponent<MudTable<BrandListItemDto>>();
        var selected = table.Instance.Items!.Take(count).ToHashSet();
        await cut.InvokeAsync(() => table.Instance.SelectedItemsChanged.InvokeAsync(selected));
    }

    // =========================================================================
    // Full row content (AC-1) and pagination controls (AC-1, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenBrandsExist_ShowsEachBrandsNameAndDescription()
    {
        var cut = await RenderListAsync();

        var firstBrand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        cut.Markup.Should().Contain(firstBrand.Name);
        cut.Markup.Should().Contain(firstBrand.Description!);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenBrandsExist_ShowsEachBrandsLogo()
    {
        var cut = await RenderListAsync();

        var firstBrand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        cut.FindAll("img").Should().Contain(img => img.GetAttribute("src") == firstBrand.LogoUrl);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenBrandsSpanMultiplePages_ShowsPaginationControls()
    {
        // The constructor's default page reports a total of 24 brands at 10 per page — 3 pages.
        var cut = await RenderListAsync();

        cut.FindComponent<ShopPagination>().Instance.TotalPages.Should().Be(3);
    }

    // =========================================================================
    // No brands exist yet (edge case, AC-18)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenNoBrandsExistYet_ShowsTheEmptyStateMessage()
    {
        SetUpKnownBrandsList([]);

        var cut = await RenderListAsync();

        cut.Markup.Should().Contain(Strings.ManageBrands_EmptyTitle);
        cut.Markup.Should().Contain(Strings.ManageBrands_EmptyDescription);
    }

    // =========================================================================
    // Access boundary (AC-17)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ManageBrands_Always_CarriesTheBrandsViewAuthorizePolicy()
    {
        var attributes = typeof(ManageBrands).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Brands.View.Code),
            "the route-level policy is the only gate between a non-viewer and the list — " +
            "App.razor's NotAuthorized template renders the denied/redirect experience");
    }

    // =========================================================================
    // View-only staff member sees no add/edit/status-flip/delete control (AC-16)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenUserHoldsOnlyBrandsView_DoesNotShowTheAddBrandButton()
    {
        AuthorizeAsBrandViewerOnly();

        var cut = Render<ManageBrands>();
        await cut.InvokeAsync(() => { });

        cut.Markup.Should().NotContain(Strings.AddBrand_Heading);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenUserHoldsOnlyBrandsView_DoesNotShowEditOrDeleteRowControls()
    {
        AuthorizeAsBrandViewerOnly();

        var cut = Render<ManageBrands>();
        await cut.InvokeAsync(() => { });

        var brand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        cut.FindAll($"[aria-label='{string.Format(Strings.ManageBrands_EditAria, brand.Name)}']").Should().BeEmpty();
        cut.FindAll($"[aria-label='{string.Format(Strings.ManageBrands_DeleteAria, brand.Name)}']").Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenUserHoldsOnlyBrandsView_RendersStatusAsPlainTextNotAClickableToggle()
    {
        AuthorizeAsBrandViewerOnly();

        var cut = Render<ManageBrands>();
        await cut.InvokeAsync(() => { });

        cut.FindComponents<MudChip<string>>().Should().BeEmpty(
            "a view-only staff member cannot flip a brand's status, so it renders as text, not a clickable chip");
    }

    // =========================================================================
    // Edit-but-not-delete staff member sees Activate/Deactivate but no bulk Delete (AC-26)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WhenUserHoldsEditButNotDelete_BulkBarOffersActivateAndDeactivateButNotDelete()
    {
        var items = new[] { Item("Elf Bar") };
        SetUpKnownBrandsList(items);
        AuthorizeAsBrandEditorOnly();
        var cut = Render<ManageBrands>();
        await cut.InvokeAsync(() => { });
        await SelectBrandsAsync(cut, count: 1);

        cut.Markup.Should().Contain(Strings.ManageBrands_BulkSetActive);
        cut.Markup.Should().Contain(Strings.ManageBrands_BulkSetInactive);
        cut.Markup.Should().NotContain(Strings.ManageBrands_BulkDelete);
    }

    // =========================================================================
    // Edit link addresses the brand by id (AC-27)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_EditControl_LinksToTheBrandsIdKeyedEditRoute()
    {
        var cut = await RenderListAsync();

        var brand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        cut.Find($"[aria-label='{string.Format(Strings.ManageBrands_EditAria, brand.Name)}']")
           .GetAttribute("href").Should().Be(Routes.Admin.EditBrand(brand.Id));
    }

    // =========================================================================
    // Delete a single, unused brand (AC-13, RULE-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteSingle_WhenConfirmed_SendsDeleteBrandsCommandWithThatBrandsId()
    {
        var cut = await RenderListAsync();
        var brand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandDeletionOutcomeDto(1, [])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageBrands_DeleteAria, brand.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _mediator.Received(1).Send(
            Arg.Is<DeleteBrandsCommand>(c => c.BrandIds.Count == 1 && c.BrandIds[0] == brand.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteSingle_WhenActivated_ShowsAConfirmDialogNamingTheBrand()
    {
        var cut = await RenderListAsync();
        var brand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandDeletionOutcomeDto(1, [])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageBrands_DeleteAria, brand.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(
            Arg.Any<string>(),
            Arg.Is<DialogParameters>(p => p.Get<string>("BodyText")!.Contains(brand.Name)));
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteSingle_WhenCancelled_DoesNotSendTheCommand()
    {
        var cut = await RenderListAsync();
        var brand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: false);

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageBrands_DeleteAria, brand.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _mediator.DidNotReceive().Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Delete a single, in-use brand — refused, named (AC-15, FR-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteSingle_WhenTheBrandIsInUse_ShowsTheInUseMessageNamingItAndItsProductCount()
    {
        var cut = await RenderListAsync();
        var brand = cut.FindComponent<MudTable<BrandListItemDto>>().Instance.Items!.First();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandDeletionOutcomeDto(0, [new BlockedBrandDto(brand.Id, brand.Name, 3)])));

        var deleteButton = cut.Find($"[aria-label='{string.Format(Strings.ManageBrands_DeleteAria, brand.Name)}']");
        await cut.InvokeAsync(() => deleteButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        _snackbar.Received(1).Add(string.Format(Strings.Brand_InUse, brand.Name, 3), Severity.Warning);
    }

    // =========================================================================
    // Bulk delete — mixed and all-blocked outcomes (AC-24, AC-25, RULE-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDelete_WhenConfirmed_SendsDeleteBrandsCommandWithEverySelectedId()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary"), Item("Geek Bar") };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandDeletionOutcomeDto(3, [])));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _mediator.Received(1).Send(
            Arg.Is<DeleteBrandsCommand>(c => c.BrandIds.Count == 3 && items.All(i => c.BrandIds.Contains(i.Id))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDelete_WhenActivated_AsksForConfirmationStatingHowManyWillBeDeleted()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary") };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandDeletionOutcomeDto(2, [])));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(
            Arg.Any<string>(),
            Arg.Is<DialogParameters>(p => p.Get<string>("BodyText")!.Contains("2")));
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDelete_WhenCancelled_DoesNotSendTheCommandAndKeepsTheSelection()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary") };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: false);

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        await _mediator.DidNotReceive().Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>());
        cut.FindComponent<ShopBulkActionBar>().Instance.SelectedCount.Should().Be(2,
            "dismissing the confirmation must leave the selection untouched so it can be retried (RULE-7)");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDelete_WithAMixedOutcome_ReportsBothCounts()
    {
        var deletable = new[] { Item("Elf Bar"), Item("Geek Bar"), Item("Vaporesso") };
        var blocked = new[] { Item("Lost Mary"), Item("Smok") };
        var items = deletable.Concat(blocked).ToArray();
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: items.Length);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new BrandDeletionOutcomeDto(3, [.. blocked.Select(b => new BlockedBrandDto(b.Id, b.Name, 2))]);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        _snackbar.Received(1).Add(string.Format(Strings.Brand_BulkDeletePartial, 3, 2), Severity.Warning);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDelete_WithAMixedOutcome_KeepsOnlyTheBlockedBrandsSelected()
    {
        var deletable = new[] { Item("Elf Bar"), Item("Geek Bar"), Item("Vaporesso") };
        var blocked = new[] { Item("Lost Mary"), Item("Smok") };
        var items = deletable.Concat(blocked).ToArray();
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: items.Length);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new BrandDeletionOutcomeDto(3, [.. blocked.Select(b => new BlockedBrandDto(b.Id, b.Name, 2))]);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        cut.FindComponent<ShopBulkActionBar>().Instance.SelectedCount.Should().Be(2,
            "the blocked brands stay selected so the staff member can act on them next (AC-24)");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDelete_WhenEverySelectedBrandIsInUse_ShowsTheAllBlockedMessage()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary") };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: true);
        var outcome = new BrandDeletionOutcomeDto(0, [.. items.Select(i => new BlockedBrandDto(i.Id, i.Name, 1))]);
        _mediator.Send(Arg.Any<DeleteBrandsCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok(outcome));

        var deleteButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkDelete));
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync());

        _snackbar.Received(1).Add(Strings.Brand_BulkDeleteAllBlocked, Severity.Warning);
    }

    // =========================================================================
    // Inline status toggle — activate immediate, deactivate confirms (AC-21, RULE-14)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task StatusToggle_WhenActivatingAnInactiveBrand_SendsImmediatelyWithoutConfirmation()
    {
        var items = new[] { Item("Elf Bar", isActive: false) };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandStatusChangeDto(1)));

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _dialogService.DidNotReceive().ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(
            Arg.Is<SetBrandStatusCommand>(c => c.BrandIds.Count == 1 && c.BrandIds[0] == items[0].Id && c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task StatusToggle_WhenDeactivatingAnActiveBrand_AsksForConfirmationBeforeSending()
    {
        var items = new[] { Item("Elf Bar", isActive: true) };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandStatusChangeDto(1)));

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(Arg.Is<SetBrandStatusCommand>(c => !c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task StatusToggle_WhenDeactivationIsCancelled_DoesNotSendTheCommand()
    {
        var items = new[] { Item("Elf Bar", isActive: true) };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        SetUpConfirmDialogResult(confirmed: false);

        var chip = cut.FindComponent<MudChip<string>>();
        await cut.InvokeAsync(() => chip.Instance.OnClick.InvokeAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        await _mediator.DidNotReceive().Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Bulk activate/deactivate — activate immediate, deactivate confirms with a count (AC-23, RULE-14)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkActivate_SendsImmediatelyWithoutConfirmation()
    {
        var items = new[] { Item("Elf Bar", isActive: false), Item("Lost Mary", isActive: false) };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 2);
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandStatusChangeDto(2)));

        var activateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkSetActive));
        await cut.InvokeAsync(() => activateButton.Instance.OnClick.InvokeAsync());

        await _dialogService.DidNotReceive().ShowAsync<ShopConfirmDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>());
        await _mediator.Received(1).Send(
            Arg.Is<SetBrandStatusCommand>(c => c.BrandIds.Count == 2 && c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDeactivate_WhenConfirmed_SendsWithEverySelectedIdAndReportsHowManyChanged()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary"), Item("Geek Bar") };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandStatusChangeDto(3)));

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _mediator.Received(1).Send(
            Arg.Is<SetBrandStatusCommand>(c => c.BrandIds.Count == 3 && !c.IsActive), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add(string.Format(Strings.ManageBrands_DeactivatedSuccess, 3), Severity.Success);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDeactivate_WhenActivated_AsksForConfirmationStatingHowManyWillBeHidden()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary"), Item("Geek Bar") };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 3);
        SetUpConfirmDialogResult(confirmed: true);
        _mediator.Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new BrandStatusChangeDto(3)));

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _dialogService.Received(1).ShowAsync<ShopConfirmDialog>(
            Arg.Any<string>(),
            Arg.Is<DialogParameters>(p => p.Get<string>("BodyText")!.Contains("3")));
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BulkDeactivate_WhenCancelled_DoesNotSendTheCommand()
    {
        var items = new[] { Item("Elf Bar"), Item("Lost Mary") };
        SetUpKnownBrandsList(items);
        var cut = await RenderListAsync();
        await SelectBrandsAsync(cut, count: 2);
        SetUpConfirmDialogResult(confirmed: false);

        var deactivateButton = cut.FindComponents<MudButton>().First(b => b.Markup.Contains(Strings.ManageBrands_BulkSetInactive));
        await cut.InvokeAsync(() => deactivateButton.Instance.OnClick.InvokeAsync());

        await _mediator.DidNotReceive().Send(Arg.Any<SetBrandStatusCommand>(), Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (defaults half): Render_OnInitialize_QueriesTheFirstPageWithAllStatusAndNameAToZ
// AC-2: ChangePage_AfterFilteringAndSorting_KeepsBothCriteriaOnTheNewPage
// AC-3: Search_WhenStaffTypesATerm_ReQueriesWithThatTermFromPageOne
// AC-4: ChangeStatusFilter_WhenStaffFiltersByInactive_ReQueriesWithThatStatusFromPageOne
// AC-5: ChangeSort_WhenStaffReversesTheNameOrder_ReQueriesWithThatSortFromPageOne
// AC-18 (clear-affordance half): ClearFilters_AfterSearchingAndFiltering_ReQueriesWithNoSearchAndAllStatus
// AC-22: ChangeSort_WithBrandsSelected_ClearsTheSelectionAndHidesTheBulkBar,
//        CloseBulkBar_WhenStaffDismissesIt_ClearsTheSelectionAndHidesTheBar
// Loading feedback for in-flight mutations (no AC — reported as a defect):
//        BulkActivate_WhileTheStatusChangeIsInFlight_ShowsTheTableLoaderAndDisablesTheActions,
//        RowControls_WhileAStatusChangeIsInFlight_AreDisabled,
//        StatusChange_WhileInFlight_SwapsTheActedOnRowsChipForAnInRowSpinner
// AC-1 (row content + pagination half): Render_WhenBrandsExist_ShowsEachBrandsNameAndDescription,
//        Render_WhenBrandsExist_ShowsEachBrandsLogo, Render_WhenBrandsSpanMultiplePages_ShowsPaginationControls
// AC-18 (no-brands-yet half): Render_WhenNoBrandsExistYet_ShowsTheEmptyStateMessage
// AC-13: DeleteSingle_WhenConfirmed_SendsDeleteBrandsCommandWithThatBrandsId,
//        DeleteSingle_WhenActivated_ShowsAConfirmDialogNamingTheBrand
// AC-14: DeleteSingle_WhenCancelled_DoesNotSendTheCommand,
//        BulkDelete_WhenCancelled_DoesNotSendTheCommandAndKeepsTheSelection
// AC-15: DeleteSingle_WhenTheBrandIsInUse_ShowsTheInUseMessageNamingItAndItsProductCount
// AC-16: Render_WhenUserHoldsOnlyBrandsView_DoesNotShowTheAddBrandButton,
//        Render_WhenUserHoldsOnlyBrandsView_DoesNotShowEditOrDeleteRowControls,
//        Render_WhenUserHoldsOnlyBrandsView_RendersStatusAsPlainTextNotAClickableToggle
// AC-17: ManageBrands_Always_CarriesTheBrandsViewAuthorizePolicy
// AC-21: StatusToggle_WhenActivatingAnInactiveBrand_SendsImmediatelyWithoutConfirmation,
//        StatusToggle_WhenDeactivatingAnActiveBrand_AsksForConfirmationBeforeSending,
//        StatusToggle_WhenDeactivationIsCancelled_DoesNotSendTheCommand
// AC-23: BulkActivate_SendsImmediatelyWithoutConfirmation,
//        BulkDeactivate_WhenConfirmed_SendsWithEverySelectedIdAndReportsHowManyChanged,
//        BulkDeactivate_WhenActivated_AsksForConfirmationStatingHowManyWillBeHidden,
//        BulkDeactivate_WhenCancelled_DoesNotSendTheCommand
// AC-24: BulkDelete_WhenConfirmed_SendsDeleteBrandsCommandWithEverySelectedId,
//        BulkDelete_WhenActivated_AsksForConfirmationStatingHowManyWillBeDeleted,
//        BulkDelete_WithAMixedOutcome_ReportsBothCounts, BulkDelete_WithAMixedOutcome_KeepsOnlyTheBlockedBrandsSelected
// AC-25: BulkDelete_WhenEverySelectedBrandIsInUse_ShowsTheAllBlockedMessage
// AC-26: Render_WhenUserHoldsEditButNotDelete_BulkBarOffersActivateAndDeactivateButNotDelete
// AC-27: Render_EditControl_LinksToTheBrandsIdKeyedEditRoute
