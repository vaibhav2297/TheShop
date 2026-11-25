using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheShop.Web.Common.Notifications;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.Tests.Common.Notifications;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopNotificationHostTests : TestContext
{
    private readonly ManualNotificationClock _clock = new();
    private readonly ShopNotificationService _service;

    public ShopNotificationHostTests()
    {
        _service = new(_clock);
        Services.AddSingleton(_service);
    }

    [Theory]
    [InlineData(ShopNotificationKind.Info)]
    [InlineData(ShopNotificationKind.Success)]
    [InlineData(ShopNotificationKind.Warning)]
    [InlineData(ShopNotificationKind.Error)]
    public async Task Render_AllKindsUseSameEncodedBarAndKeepMeaning(ShopNotificationKind kind)
    {
        var cut = Render<ShopNotificationHost>();
        cut.Find("[role=status]").GetAttribute("aria-live").Should().Be("polite");
        await cut.InvokeAsync(() => _service.Show("<script>unsafe</script>", kind));
        cut.Find("[data-testid=notification]").ClassName.Should().Be("shop-notification");
        cut.Find(".shop-notification-message").TextContent.Should().Be("<script>unsafe</script>");
        cut.FindAll(".shop-notification-kind").Should().BeEmpty();
        _service.Messages.Single().Kind.Should().Be(kind);
        cut.FindAll("script").Should().BeEmpty();
        var dismiss = cut.Find("[data-testid=notification-dismiss]");
        dismiss.GetAttribute("aria-label").Should().Be(Strings.Notification_Dismiss);
        dismiss.GetAttribute("type").Should().Be("button");
        dismiss.Click();
        _service.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HoverAndFocus_IndependentPauseSourcesPreventEarlyExpiry()
    {
        var cut = Render<ShopNotificationHost>();
        await cut.InvokeAsync(() => _service.Show("Example"));
        var notification = cut.Find(".shop-notification");
        notification.MouseEnter();
        notification.TriggerEvent("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        notification.MouseLeave();
        await cut.InvokeAsync(() => _clock.Advance(TimeSpan.FromMinutes(1)));
        cut.FindAll(".shop-notification").Should().ContainSingle();
        notification.TriggerEvent("onfocusout", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        await cut.InvokeAsync(() => _clock.Advance(TimeSpan.FromSeconds(5)));
        cut.FindAll(".shop-notification").Should().BeEmpty();
    }

    [Fact]
    public async Task LayoutTransfer_RendersOnlyCurrentHostAndKeepsPendingMessage()
    {
        var oldHost = Render<ShopNotificationHost>();
        await oldHost.InvokeAsync(() => _service.Show("Saved before navigation"));
        var newHost = Render<ShopNotificationHost>();
        oldHost.WaitForAssertion(() => oldHost.FindAll("[role=status]").Should().BeEmpty());
        newHost.FindAll(".shop-notification").Should().ContainSingle();
        oldHost.Dispose();
        await newHost.InvokeAsync(() => _service.Show("Another message"));
        newHost.FindAll(".shop-notification").Should().HaveCount(2);
        newHost.Dispose();
        _clock.Advance(TimeSpan.FromSeconds(5));
        _service.Messages.Should().BeEmpty();
    }
}
