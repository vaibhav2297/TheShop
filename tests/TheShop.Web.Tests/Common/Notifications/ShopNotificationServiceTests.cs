using FluentAssertions;
using TheShop.Web.Common.Notifications;
using Xunit;

namespace TheShop.Web.Tests.Common.Notifications;

public class ShopNotificationServiceTests
{
    [Fact]
    public void Show_ExpiresAfterFiveSecondsAndDisposesTimer()
    {
        var clock = new ManualNotificationClock();
        using var service = new ShopNotificationService(clock);
        var changes = 0;
        service.Changed += () => changes++;
        service.Show("Saved", ShopNotificationKind.Success);
        clock.Advance(TimeSpan.FromSeconds(4));
        service.Messages.Should().ContainSingle();
        clock.Advance(TimeSpan.FromSeconds(1));
        service.Messages.Should().BeEmpty();
        clock.ActiveTimerCount.Should().Be(0);
        changes.Should().Be(2);
    }

    [Fact]
    public void Show_CoalescesExactDuplicatesButNotDifferentKinds()
    {
        using var service = new ShopNotificationService(new ManualNotificationClock());
        service.Show("Example", ShopNotificationKind.Info);
        var id = service.Messages.Single().Id;
        service.Show("Example", ShopNotificationKind.Info);
        service.Messages.Should().ContainSingle().Which.Id.Should().Be(id);
        service.Show("Example", ShopNotificationKind.Error);
        service.Messages.Should().HaveCount(2);
    }

    [Fact]
    public void Show_BoundedStorageEvictsOldestUnpausedMessage()
    {
        var clock = new ManualNotificationClock();
        using var service = new ShopNotificationService(clock);
        var host = new object();
        service.AttachHost(host);
        service.Show("Protected");
        service.SetPaused(host, service.Messages[0].Id, true);
        for (var i = 0; i < 10; i++) service.Show($"Message {i}");
        service.Messages.Should().HaveCount(5);
        service.Messages[0].Text.Should().Be("Protected");
        service.Messages[^1].Text.Should().Be("Message 9");
        clock.ActiveTimerCount.Should().Be(5);
    }

    [Fact]
    public void Pause_ResumingGrantsFreshLifetimeAndIgnoresStaleHosts()
    {
        var clock = new ManualNotificationClock();
        using var service = new ShopNotificationService(clock);
        var host = new object();
        service.AttachHost(host);
        service.Show("Example");
        var id = service.Messages[0].Id;
        clock.Advance(TimeSpan.FromSeconds(4));
        service.SetPaused(host, id, true);
        clock.Advance(TimeSpan.FromMinutes(1));
        service.Messages.Should().ContainSingle();
        service.SetPaused(new object(), id, false);
        clock.Advance(TimeSpan.FromMinutes(1));
        service.Messages.Should().ContainSingle();
        service.SetPaused(host, id, false);
        clock.Advance(TimeSpan.FromSeconds(4));
        service.Messages.Should().ContainSingle();
        clock.Advance(TimeSpan.FromSeconds(1));
        service.Messages.Should().BeEmpty();
    }

    [Fact]
    public void HostTransfer_PreservesMessageAndLateDetachCannotReleaseNewOwner()
    {
        var clock = new ManualNotificationClock();
        using var service = new ShopNotificationService(clock);
        var oldHost = new object();
        var newHost = new object();
        service.AttachHost(oldHost);
        service.Show("Signed in", ShopNotificationKind.Success);
        service.SetPaused(oldHost, service.Messages[0].Id, true);
        service.AttachHost(newHost);
        service.DetachHost(oldHost);
        service.IsHost(newHost).Should().BeTrue();
        service.Messages.Should().ContainSingle();
        clock.Advance(TimeSpan.FromSeconds(5));
        service.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Detach_PreservesPendingNavigationMessageButDoesNotLeakPausedTimers()
    {
        var clock = new ManualNotificationClock();
        using var service = new ShopNotificationService(clock);
        var host = new object();
        service.AttachHost(host);
        service.Show("Example");
        service.SetPaused(host, service.Messages[0].Id, true);
        service.DetachHost(host);
        service.Messages.Should().ContainSingle();
        clock.Advance(TimeSpan.FromSeconds(5));
        service.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Dismiss_RepeatedOrStaleIdentityCannotRemoveNewMessages()
    {
        using var service = new ShopNotificationService(new ManualNotificationClock());
        service.Show("First");
        var id = service.Messages[0].Id;
        service.Dismiss(id);
        service.Show("Second");
        service.Dismiss(id);
        service.Messages.Should().ContainSingle().Which.Text.Should().Be("Second");
    }

    [Fact]
    public void Dispose_ClearsTimersSubscriptionsAndRejectsLaterWork()
    {
        var clock = new ManualNotificationClock();
        var service = new ShopNotificationService(clock);
        service.Show("Example");
        var changes = 0;
        service.Changed += () => changes++;
        service.Dispose();
        service.Dispose();
        service.Show("Ignored");
        clock.Advance(TimeSpan.FromMinutes(1));
        changes.Should().Be(0);
        service.Messages.Should().BeEmpty();
        clock.ActiveTimerCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Show_BlankTextFailsClearly(string? message)
    {
        using var service = new ShopNotificationService(new ManualNotificationClock());
        var act = () => service.Show(message!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Show_UndefinedKindFailsClearly()
    {
        using var service = new ShopNotificationService(new ManualNotificationClock());
        var act = () => service.Show("Example", (ShopNotificationKind)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
