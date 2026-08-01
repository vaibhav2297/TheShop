using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Application.Features.Customers.Queries.GetCurrentCustomerProfile;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for the admin-console entry point in <see cref="ProfileMenu"/> — the account menu leads
/// to the <c>/admin</c> dashboard rather than opening a single module directly, and is present
/// only for a signed-in user who holds at least one admin-area permission (FR-6, AC-6; consistent
/// with FR-5's "hidden by omission" principle). <see cref="ProfileMenu"/>'s own identity/sign-out
/// behavior belongs to other features and is not re-tested here.
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public class ProfileMenuTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public ProfileMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddSingleton<BusyState>();
        Services.AddMudServices();
        // Uses the real IPopoverService (rather than the bare-substitute pattern other tests use)
        // because MudMenuItem only materializes as a component once a MudPopoverProvider actually
        // registers and renders the open menu's content — a substitute never does that.

        // The profile name/email fetch is irrelevant to the admin-console entry point; fail it
        // harmlessly so ProfileMenu's OnInitializedAsync completes without needing extra setup.
        _mediator.Send(Arg.Any<GetCurrentCustomerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CustomerProfileDto>("AccountNotFound"));
    }

    private async Task<IRenderedComponent<ContainerFragment>> RenderOpenProfileMenuAsync()
    {
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ProfileMenu>(1);
            builder.CloseComponent();
        });

        var menu = cut.FindComponent<MudMenu>();
        await cut.InvokeAsync(() => menu.Instance.OpenMenuAsync(EventArgs.Empty));

        return cut;
    }

    // =========================================================================
    // Staff holds the dashboard permission — entry present, targets /admin (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Render_WhenUserHoldsTheAdminDashboardPermission_ShowsAdminConsoleEntryLinkingToAdmin()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.AdminDashboard);

        var cut = await RenderOpenProfileMenuAsync();

        var adminItem = cut.FindComponents<MudMenuItem>().Should()
            .ContainSingle(item => item.Instance.Href == Routes.Admin.Console).Subject;
        adminItem.Markup.Should().Contain(Strings.Nav_AdminConsole);
    }

    // =========================================================================
    // No dashboard permission — entry absent entirely, not merely disabled (FR-5 principle)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public async Task Render_WhenUserLacksTheAdminDashboardPermission_DoesNotShowAdminConsoleEntry()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("customer-user"); // authenticated, but dashboard.view not granted

        var cut = await RenderOpenProfileMenuAsync();

        cut.FindComponents<MudMenuItem>().Should().NotContain(item => item.Instance.Href == Routes.Admin.Console);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: Render_WhenUserHoldsTheAdminDashboardPermission_ShowsAdminConsoleEntryLinkingToAdmin,
//        Render_WhenUserLacksTheAdminDashboardPermission_DoesNotShowAdminConsoleEntry
