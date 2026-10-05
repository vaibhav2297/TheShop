using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopLoadingOverlayTests : TestContext
{
    private readonly BusyState _busy = new();

    public ShopLoadingOverlayTests() => Services.AddSingleton(_busy);

    [Fact]
    public void Render_Idle_HasNoOverlayAndPrimedEmptyStatus()
    {
        var cut = Render<ShopLoadingOverlay>();

        cut.FindAll("[data-testid=loading-overlay]").Should().BeEmpty();
        var status = cut.Find("[role=status]");
        status.ClassName.Should().Be("shop-visually-hidden");
        status.TextContent.Should().BeEmpty();
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public async Task GlobalBusy_ShowsDecorativeOverlayAndAnnouncesUntilReleased()
    {
        var cut = Render<ShopLoadingOverlay>();
        var release = new TaskCompletionSource();

        var run = cut.InvokeAsync(() => _busy.RunAsync(BusyKeys.Global, () => release.Task));

        cut.WaitForAssertion(() =>
        {
            var overlay = cut.Find("[data-testid=loading-overlay]");
            overlay.ClassName.Should().Be("shop-loading-overlay");
            overlay.GetAttribute("aria-hidden").Should().Be("true");
            overlay.QuerySelector(".shop-spinner.shop-loading-overlay-spinner").Should().NotBeNull();
            cut.Find("[role=status]").TextContent.Should().Be(Strings.Loading);
        });

        release.SetResult();
        await run;

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid=loading-overlay]").Should().BeEmpty();
            cut.Find("[role=status]").TextContent.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task GlobalBusy_FailedOperationStillReleasesOverlay()
    {
        var cut = Render<ShopLoadingOverlay>();

        var act = () => cut.InvokeAsync(() => _busy.RunAsync(BusyKeys.Global,
            () => Task.FromException(new InvalidOperationException())));

        await act.Should().ThrowAsync<InvalidOperationException>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=loading-overlay]").Should().BeEmpty());
    }

    [Fact]
    public async Task OtherBusyKey_DoesNotBlockPage()
    {
        var cut = Render<ShopLoadingOverlay>();
        var release = new TaskCompletionSource();

        var run = cut.InvokeAsync(() => _busy.RunAsync(BusyKeys.Auth.SignIn, () => release.Task));

        cut.FindAll("[data-testid=loading-overlay]").Should().BeEmpty();
        release.SetResult();
        await run;
    }

    [Fact]
    public void Render_AlreadyBusy_ShowsOverlayImmediately()
    {
        var release = new TaskCompletionSource();
        _ = _busy.RunAsync(BusyKeys.Global, () => release.Task);

        var cut = Render<ShopLoadingOverlay>();

        cut.Find("[data-testid=loading-overlay]").Should().NotBeNull();
        release.SetResult();
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromBusyState()
    {
        var cut = Render<ShopLoadingOverlay>();
        var renders = cut.RenderCount;
        await DisposeComponentsAsync();

        var act = () => _busy.RunAsync(BusyKeys.Global, () => Task.CompletedTask);

        await act.Should().NotThrowAsync();
        cut.RenderCount.Should().Be(renders);
    }
}
