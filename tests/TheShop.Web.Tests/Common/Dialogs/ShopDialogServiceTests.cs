using FluentAssertions;
using TheShop.Web.Common.Dialogs;
using Xunit;

namespace TheShop.Web.Tests.Common.Dialogs;

public class ShopDialogServiceTests
{
    private static readonly ShopConfirmationOptions Options = new("Title", "Body", "Confirm");

    [Fact]
    public async Task Confirm_WithoutHost_ReturnsFalseImmediately()
    {
        var service = new ShopDialogService();
        (await service.ConfirmAsync(Options, Xunit.TestContext.Current.CancellationToken)).Should().BeFalse();
        service.Current.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_TwoRequests_SerializesAndIgnoresStaleCompletion()
    {
        var service = new ShopDialogService();
        service.AttachHost(this);
        var first = service.ConfirmAsync(Options, Xunit.TestContext.Current.CancellationToken);
        var firstId = service.Current!.Id;
        var second = service.ConfirmAsync(Options with { Title = "Second" }, Xunit.TestContext.Current.CancellationToken);
        service.Current.Id.Should().Be(firstId);
        second.IsCompleted.Should().BeFalse();
        service.Complete(firstId, true);
        (await first).Should().BeTrue();
        service.Current!.Options.Title.Should().Be("Second");
        service.Complete(firstId, false);
        second.IsCompleted.Should().BeFalse();
        service.Complete(service.Current.Id, false);
        (await second).Should().BeFalse();
        service.Current.Should().BeNull();
    }

    [Fact]
    public async Task Cancellation_ActiveRequest_AdvancesQueue()
    {
        var service = new ShopDialogService();
        service.AttachHost(this);
        using var cancellation = new CancellationTokenSource();
        var first = service.ConfirmAsync(Options, cancellation.Token);
        var second = service.ConfirmAsync(Options with { Title = "Second" }, Xunit.TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        (await first).Should().BeFalse();
        service.Current!.Options.Title.Should().Be("Second");
        service.Complete(service.Current.Id, true);
        (await second).Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_QueuedRequest_DoesNotDismissActiveRequest()
    {
        var service = new ShopDialogService();
        service.AttachHost(this);
        var first = service.ConfirmAsync(Options, Xunit.TestContext.Current.CancellationToken);
        var firstId = service.Current!.Id;
        using var cancellation = new CancellationTokenSource();
        var second = service.ConfirmAsync(Options, cancellation.Token);
        await cancellation.CancelAsync();
        (await second).Should().BeFalse();
        service.Current.Id.Should().Be(firstId);
        first.IsCompleted.Should().BeFalse();
        service.CancelAll();
        (await first).Should().BeFalse();
    }

    [Fact]
    public async Task Cancellation_PreCancelled_DoesNotEnqueue()
    {
        var service = new ShopDialogService();
        service.AttachHost(this);
        (await service.ConfirmAsync(Options, new CancellationToken(true))).Should().BeFalse();
        service.Current.Should().BeNull();
    }

    [Fact]
    public async Task Host_LayoutHandoff_CancelsOldRequestsAndIgnoresOldHostDisposal()
    {
        var service = new ShopDialogService();
        service.AttachHost(this);
        var result = service.ConfirmAsync(Options, Xunit.TestContext.Current.CancellationToken);
        service.DetachHost(new object());
        result.IsCompleted.Should().BeFalse();
        var nextHost = new object();
        service.AttachHost(nextHost);
        (await result).Should().BeFalse();
        service.IsHost(this).Should().BeFalse();
        var next = service.ConfirmAsync(Options, Xunit.TestContext.Current.CancellationToken);
        service.DetachHost(this);
        next.IsCompleted.Should().BeFalse();
        service.DetachHost(nextHost);
        (await next).Should().BeFalse();
    }
}
