using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.Commands.SignOut;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Application.Features.Customers.Queries.GetCurrentCustomerProfile;
using TheShop.Web.Common;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ProfileDrawerTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public ProfileDrawerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);
        Services.AddSingleton<BusyState>();
        _mediator.Send(Arg.Any<GetCurrentCustomerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new CustomerProfileDto(Guid.NewGuid(), "John", "Doe", "john@example.com", new DateOnly(1990, 1, 1))));
        _localizer[nameof(Strings.Auth_SignedOut)].Returns(new LocalizedString(nameof(Strings.Auth_SignedOut), Strings.Auth_SignedOut));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Render_AdminPermissionControlsConsoleLink_WithoutVendorServices(bool admin)
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("customer");
        if (admin) auth.SetPolicies(PolicyNames.AdminDashboard);
        var cut = Render<ProfileDrawer>(p => p.Add(x => x.Open, true));
        cut.FindAll($"a[href='{Routes.Admin.Console}']").Count.Should().Be(admin ? 1 : 0);
        cut.Find(".shop-profile-drawer-name").TextContent.Should().Be("Hi, John Doe");
        cut.Find(".shop-profile-drawer-email").TextContent.Should().Be("john@example.com");
        cut.Find($"a[href='{Routes.Profile}']").GetAttribute("aria-label").Should().Be(Strings.Nav_MyProfile);
        cut.Find($"a[href='{Routes.Orders}']").TextContent.Should().Contain(Strings.Nav_Orders);
        cut.Find($"a[href='{Routes.Wishlist}']").TextContent.Should().Contain(Strings.Nav_Wishlist);
        cut.FindAll("footer").Should().BeEmpty();
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public void Navigation_RequestsClosure_AndPreservesDestination()
    {
        this.AddAuthorization().SetAuthorized("customer");
        var open = true;
        var cut = Render<ProfileDrawer>(p => p.Add(x => x.Open, true).Add(x => x.OpenChanged, value => open = value));
        cut.Find($"a[href='{Routes.Profile}']").Click();
        open.Should().BeFalse();
    }

    [Fact]
    public async Task Logout_ClosesDrawerDispatchesCommandAndNotifies_ThenNavigatesHome()
    {
        this.AddAuthorization().SetAuthorized("customer");
        _mediator.Send(Arg.Any<SignOutCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var open = true;
        var cut = Render<ProfileDrawer>(p => p.Add(x => x.Open, true).Add(x => x.OpenChanged, value => open = value));
        await cut.Find("button.shop-profile-drawer-link").ClickAsync(new());
        open.Should().BeFalse();
        await _mediator.Received(1).Send(Arg.Any<SignOutCommand>(), Arg.Any<CancellationToken>());
        _notifications.Received(1).Show(Strings.Auth_SignedOut, ShopNotificationKind.Success);
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Home);
        Services.GetRequiredService<BusyState>().IsBusy(BusyKeys.Global).Should().BeFalse();
    }

    [Fact]
    public void FailedProfileFetch_KeepsProfileDestinationAndFallbackName()
    {
        this.AddAuthorization().SetAuthorized("customer");
        _mediator.Send(Arg.Any<GetCurrentCustomerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CustomerProfileDto>("AccountNotFound"));
        var cut = Render<ProfileDrawer>();
        cut.Find(".shop-profile-drawer-name").TextContent.Should().Be(Strings.Nav_MyProfile);
        cut.FindAll(".shop-profile-drawer-email").Should().BeEmpty();
    }
}
