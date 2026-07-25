using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Admin;
using TheShop.Application.Features.Admin.Queries.GetAdminDashboard;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Admin.Queries.GetAdminDashboard;

/// <summary>
/// Tests for <see cref="GetAdminDashboardHandler"/> — per-module permission gating (FR-5/RULE-2,
/// AC-3), per-module count-failure isolation (FR-7/RULE-3, AC-5), the all-permitted happy path
/// (FR-1..FR-3, AC-1), and the defense-in-depth behavior for a caller with no admin-area
/// permissions at all, including an unauthenticated caller reaching the handler directly
/// (Decision 3, AC-4).
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public class GetAdminDashboardHandlerTests
{
    private readonly IAdminDashboardRepository _repository = Substitute.For<IAdminDashboardRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private GetAdminDashboardHandler CreateSut() => new(_repository, _currentUser);

    private void GrantAllFiveModulePermissions()
    {
        _currentUser.HasPermission(PermissionCatalogue.Products.View.Code).Returns(true);
        _currentUser.HasPermission(PermissionCatalogue.Categories.View.Code).Returns(true);
        _currentUser.HasPermission(PermissionCatalogue.Brands.View.Code).Returns(true);
        _currentUser.HasPermission(PermissionCatalogue.AdminUsers.View.Code).Returns(true);
        _currentUser.HasPermission(PermissionCatalogue.Roles.View.Code).Returns(true);
    }

    private void StubCounts(int products = 1, int categories = 1, int brands = 1, int users = 1, int roles = 1)
    {
        _repository.CountModuleAsync(AdminModule.Products, Arg.Any<CancellationToken>()).Returns(products);
        _repository.CountModuleAsync(AdminModule.Categories, Arg.Any<CancellationToken>()).Returns(categories);
        _repository.CountModuleAsync(AdminModule.Brands, Arg.Any<CancellationToken>()).Returns(brands);
        _repository.CountModuleAsync(AdminModule.Users, Arg.Any<CancellationToken>()).Returns(users);
        _repository.CountModuleAsync(AdminModule.Roles, Arg.Any<CancellationToken>()).Returns(roles);
    }

    // =========================================================================
    // Happy path — all five modules permitted, each shows its current count (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Handle_WhenAllFiveModulesArePermitted_ReturnsAllFiveCardsWithTheirCounts()
    {
        GrantAllFiveModulePermissions();
        StubCounts(products: 42, categories: 7, brands: 12, users: 358, roles: 4);

        var result = await CreateSut().Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Modules.Should().HaveCount(5);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Products && m.Count == 42);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Categories && m.Count == 7);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Brands && m.Count == 12);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Users && m.Count == 358);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Roles && m.Count == 4);
    }

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Handle_WhenAllFiveModulesArePermitted_ReturnsThemInCatalogueDisplayOrder()
    {
        GrantAllFiveModulePermissions();
        StubCounts();

        var result = await CreateSut().Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.Value.Modules.Select(m => m.Module).Should().Equal(
            AdminModule.Products, AdminModule.Categories, AdminModule.Brands, AdminModule.Users, AdminModule.Roles);
    }

    // =========================================================================
    // Per-module permission gating — a module the admin can't view is omitted, never disabled
    // (FR-5, RULE-2, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Handle_WhenAdminHoldsOnlySomeModulePermissions_ReturnsOnlyThePermittedModules()
    {
        _currentUser.HasPermission(PermissionCatalogue.Products.View.Code).Returns(true);
        _currentUser.HasPermission(PermissionCatalogue.Brands.View.Code).Returns(true);
        _currentUser.HasPermission(PermissionCatalogue.Categories.View.Code).Returns(false);
        _currentUser.HasPermission(PermissionCatalogue.AdminUsers.View.Code).Returns(false);
        _currentUser.HasPermission(PermissionCatalogue.Roles.View.Code).Returns(false);
        StubCounts();

        var result = await CreateSut().Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.Value.Modules.Select(m => m.Module).Should().BeEquivalentTo(
            [AdminModule.Products, AdminModule.Brands]);
        await _repository.DidNotReceive().CountModuleAsync(AdminModule.Categories, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CountModuleAsync(AdminModule.Users, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CountModuleAsync(AdminModule.Roles, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // No permitted modules — empty list, not an error (spec Edge case 1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Handle_WhenAdminHoldsNoneOfTheFiveModulePermissions_ReturnsEmptyModulesList()
    {
        _currentUser.HasPermission(Arg.Any<string>()).Returns(false);

        var result = await CreateSut().Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Modules.Should().BeEmpty();
        await _repository.DidNotReceive().CountModuleAsync(Arg.Any<AdminModule>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Unauthenticated caller reaching the handler directly — no dashboard content leaks
    // (Decision 3, AC-4 defense-in-depth: the query itself carries no permission requirement, so
    // this is the handler's own guarantee that a guest/customer never sees a count)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Handle_WhenCallerIsUnauthenticated_ReturnsEmptyModulesListAndNeverCounts()
    {
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.HasPermission(Arg.Any<string>()).Returns(false); // per ICurrentUserService contract

        var result = await CreateSut().Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Modules.Should().BeEmpty();
        await _repository.DidNotReceive().CountModuleAsync(Arg.Any<AdminModule>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Count failure isolation — one module's placeholder, the rest render normally
    // (FR-7, RULE-3, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Handle_WhenOneModulesCountThrows_ReturnsNullCountForThatModuleWhileOthersSucceed()
    {
        GrantAllFiveModulePermissions();
        _repository.CountModuleAsync(AdminModule.Products, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("RPC unreachable"));
        _repository.CountModuleAsync(AdminModule.Categories, Arg.Any<CancellationToken>()).Returns(7);
        _repository.CountModuleAsync(AdminModule.Brands, Arg.Any<CancellationToken>()).Returns(12);
        _repository.CountModuleAsync(AdminModule.Users, Arg.Any<CancellationToken>()).Returns(358);
        _repository.CountModuleAsync(AdminModule.Roles, Arg.Any<CancellationToken>()).Returns(4);

        var result = await CreateSut().Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a count failure is a placeholder, not a failed result");
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Products && m.Count == null);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Categories && m.Count == 7);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Brands && m.Count == 12);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Users && m.Count == 358);
        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Roles && m.Count == 4);
    }

    // =========================================================================
    // Zero records — a real count of 0, not omitted and not a placeholder (spec Edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Handle_WhenAModuleHasZeroRecords_ReturnsThatModuleWithCountZero()
    {
        _currentUser.HasPermission(PermissionCatalogue.Roles.View.Code).Returns(true);
        _repository.CountModuleAsync(AdminModule.Roles, Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateSut().Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.Value.Modules.Should().ContainSingle(m => m.Module == AdminModule.Roles && m.Count == 0);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Handle_WhenAllFiveModulesArePermitted_ReturnsAllFiveCardsWithTheirCounts,
//        Handle_WhenAllFiveModulesArePermitted_ReturnsThemInCatalogueDisplayOrder
// AC-3: Handle_WhenAdminHoldsOnlySomeModulePermissions_ReturnsOnlyThePermittedModules
// AC-4: Handle_WhenCallerIsUnauthenticated_ReturnsEmptyModulesListAndNeverCounts
//        (defense-in-depth at the handler; the page-level redirect/AccessDeniedView experience
//        is covered in the Web layer)
// AC-5: Handle_WhenOneModulesCountThrows_ReturnsNullCountForThatModuleWhileOthersSucceed
// (spec Edge case — no permitted modules): Handle_WhenAdminHoldsNoneOfTheFiveModulePermissions_ReturnsEmptyModulesList
// (spec Edge case — zero records): Handle_WhenAModuleHasZeroRecords_ReturnsThatModuleWithCountZero
