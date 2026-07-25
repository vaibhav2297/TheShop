using System.Reflection;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Admin;
using TheShop.Application.Features.Admin.DTOs;
using TheShop.Application.Features.Admin.Queries.GetAdminDashboard;
using TheShop.Web.Common;
using TheShop.Web.Components.Admin;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="AdminConsole"/> dashboard at <c>/admin</c> — one overview card per
/// governed module the query permits (Behaviors 1 &amp; 3; FR-2, FR-3; AC-1, AC-3), the
/// count-unavailable placeholder while the rest of the dashboard renders normally (FR-7; AC-5),
/// the zero-records and no-permitted-modules edge cases, the loading skeleton, and the structural
/// guarantee that the page is gated by the <c>PolicyNames.AdminArea</c> policy carried in from
/// <c>Pages/Admin/_Imports.razor</c> (RULE-1; AC-4 — the guest-redirect/access-denied experience
/// itself is the shared router scaffolding, already covered by the role-based-access-control
/// feature's own tests; this asserts the specific seam this page relies on, per plan Decision 6).
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public class AdminConsoleTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    public AdminConsoleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton(_mediator);
        Services.AddSingleton<BusyState>();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    private static AdminDashboardDto BuildDto(params (AdminModule Module, int? Count)[] entries) =>
        new(entries.Select(e => new AdminModuleCardDto(e.Module, e.Count)).ToList());

    private void StubDashboard(AdminDashboardDto dto) =>
        _mediator.Send(Arg.Any<GetAdminDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(dto));

    // =========================================================================
    // Happy path — all five modules permitted, each with its current count (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Render_WhenAllFiveModulesArePermitted_ShowsFiveModuleCardsWithTheirCounts()
    {
        StubDashboard(BuildDto(
            (AdminModule.Products, 42), (AdminModule.Categories, 7), (AdminModule.Brands, 12),
            (AdminModule.Users, 358), (AdminModule.Roles, 4)));

        var cut = Render<AdminConsole>();
        await cut.InvokeAsync(() => { });

        var cards = cut.FindComponents<AdminModuleCard>();
        cards.Should().HaveCount(5);
        cards.Select(c => (c.Instance.Module, c.Instance.Count)).Should().BeEquivalentTo(
        [
            (AdminModule.Products, (int?)42), (AdminModule.Categories, (int?)7), (AdminModule.Brands, (int?)12),
            (AdminModule.Users, (int?)358), (AdminModule.Roles, (int?)4),
        ]);
    }

    // =========================================================================
    // Limited admin — only the permitted cards appear (Behavior 3, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Render_WhenAdminHoldsOnlySomeModulePermissions_ShowsOnlyThePermittedCards()
    {
        StubDashboard(BuildDto((AdminModule.Products, 42), (AdminModule.Brands, 12)));

        var cut = Render<AdminConsole>();
        await cut.InvokeAsync(() => { });

        var cards = cut.FindComponents<AdminModuleCard>();
        cards.Should().HaveCount(2);
        cards.Select(c => c.Instance.Module).Should().BeEquivalentTo([AdminModule.Products, AdminModule.Brands]);
    }

    // =========================================================================
    // Count unavailable — placeholder on that card only, others render normally (FR-7, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Render_WhenOneModulesCountIsUnavailable_ShowsPlaceholderOnThatCardWhileOthersShowNumbers()
    {
        StubDashboard(BuildDto((AdminModule.Products, null), (AdminModule.Categories, 10)));

        var cut = Render<AdminConsole>();
        await cut.InvokeAsync(() => { });

        var cards = cut.FindComponents<AdminModuleCard>();
        cards.Should().HaveCount(2, "the module whose count failed must still render, alongside the others");
        cut.Markup.Should().Contain(Strings.AdminConsole_CountUnavailable);
        cut.Markup.Should().Contain("10");
    }

    // =========================================================================
    // Zero records — a real 0, not an empty or hidden card (spec Edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Render_WhenAModuleHasZeroRecords_ShowsTheCardWithCountZero()
    {
        StubDashboard(BuildDto((AdminModule.Roles, 0)));

        var cut = Render<AdminConsole>();
        await cut.InvokeAsync(() => { });

        cut.FindComponents<AdminModuleCard>().Should().ContainSingle(
            c => c.Instance.Module == AdminModule.Roles && c.Instance.Count == 0);
    }

    // =========================================================================
    // No permitted modules — empty-state message, not a blank or broken page (spec Edge case 1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Render_WhenNoModulesArePermitted_ShowsTheEmptyStateMessage()
    {
        StubDashboard(BuildDto());

        var cut = Render<AdminConsole>();
        await cut.InvokeAsync(() => { });

        cut.Markup.Should().Contain(Strings.AdminConsole_NoModules);
        cut.FindComponents<AdminModuleCard>().Should().BeEmpty();
    }

    // =========================================================================
    // Loading state
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Render_WhileTheQueryIsInFlight_ShowsFiveSkeletonPlaceholdersAndNoCards()
    {
        var tcs = new TaskCompletionSource<Result<AdminDashboardDto>>();
        _mediator.Send(Arg.Any<GetAdminDashboardQuery>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);

        var cut = Render<AdminConsole>();

        cut.FindComponents<MudSkeleton>().Should().HaveCount(5);
        cut.FindComponents<AdminModuleCard>().Should().BeEmpty();
    }

    // =========================================================================
    // Structural — the route is gated by PolicyNames.AdminArea via Pages/Admin/_Imports.razor
    // (RULE-1, AC-4; plan Decision 6 — no page-level AuthorizeView is added here, so this is the
    // only seam this specific page relies on. The redirect-to-sign-in / AccessDeniedView
    // experience the policy triggers is the shared router scaffolding, already covered by
    // ShopAuthorizationPolicyProviderTests/AccessDeniedViewTests in the role-based-access-control
    // feature.)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public void AdminConsole_Always_CarriesTheAdminAreaAuthorizePolicyFromTheFolderImports()
    {
        var attribute = typeof(AdminConsole).GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull(
            "AdminConsole has no internal AuthorizeView (Decision 6) — the folder-level " +
            "[Authorize(Policy = PolicyNames.AdminArea)] is the only protection guarding /admin");
        attribute!.Policy.Should().Be(PolicyNames.AdminArea);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Render_WhenAllFiveModulesArePermitted_ShowsFiveModuleCardsWithTheirCounts
// AC-3: Render_WhenAdminHoldsOnlySomeModulePermissions_ShowsOnlyThePermittedCards
// AC-4: AdminConsole_Always_CarriesTheAdminAreaAuthorizePolicyFromTheFolderImports
//        (structural seam only — see class summary for why the redirect/denied experience itself
//        isn't re-tested here)
// AC-5: Render_WhenOneModulesCountIsUnavailable_ShowsPlaceholderOnThatCardWhileOthersShowNumbers
// (spec Edge case — zero records): Render_WhenAModuleHasZeroRecords_ShowsTheCardWithCountZero
// (spec Edge case — no permitted modules): Render_WhenNoModulesArePermitted_ShowsTheEmptyStateMessage
