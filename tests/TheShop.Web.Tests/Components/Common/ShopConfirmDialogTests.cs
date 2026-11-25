using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using TheShop.Web.Common.Dialogs;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopConfirmDialogTests : TestContext
{
    private readonly ShopDialogService _dialogs = new();

    public ShopConfirmDialogTests()
    {
        Services.AddSingleton(_dialogs);
        JSInterop.SetupModule("./js/shopDialog.js").Mode = JSRuntimeMode.Loose;
    }

    private async Task<(IRenderedComponent<ShopDialogHost> Host, Task<bool> Result)> ShowAsync(
        ShopConfirmationOptions? options = null)
    {
        var host = Render<ShopDialogHost>();
        Task<bool> result = null!;
        await host.InvokeAsync(() => { result = _dialogs.ConfirmAsync(options ?? new("Delete brand?", "Cannot be undone.", "Delete", true)); });
        host.WaitForElement("dialog");
        return (host, result);
    }

    [Fact]
    public async Task Render_LocalizedCopy_UsesAccessibleEncodedNativeMarkup()
    {
        var (host, _) = await ShowAsync(new("<img src=x onerror=alert(1)>", "Body <script>bad()</script>", "Remove"));
        var dialog = host.Find("dialog");
        host.Find("#" + dialog.GetAttribute("aria-labelledby")).TextContent.Should().Contain("<img");
        host.Find("#" + dialog.GetAttribute("aria-describedby")).TextContent.Should().Contain("<script>");
        host.FindAll("img,script,form").Should().BeEmpty();
        dialog.HasAttribute("open").Should().BeFalse("showModal owns modal state, not a rendered open attribute");
        host.FindAll("button").Should().OnlyContain(button => button.GetAttribute("type") == "button");
        host.Find("[data-testid='dialog-close']").GetAttribute("aria-label").Should().Be(Strings.Close);
        host.Find("[data-testid='dialog-cancel']").TextContent.Should().Be(Strings.Cancel);
        host.Find("[data-testid='dialog-cancel']").HasAttribute("data-dialog-initial-focus").Should().BeTrue();
        host.Find("[data-testid='dialog-confirm']").ClassList.Should().Contain("shop-button-primary")
            .And.NotContain("shop-button-error");
    }

    [Fact]
    public async Task Confirm_ExplicitActivation_CompletesTrueAndRemovesDialog()
    {
        var (host, result) = await ShowAsync();
        host.Find("[data-testid='dialog-confirm']").ClassList.Should().Contain("shop-button-error");
        await host.Find("[data-testid='dialog-confirm']").ClickAsync(new());
        (await result).Should().BeTrue();
        host.WaitForAssertion(() => host.FindAll("dialog").Should().BeEmpty());
    }

    [Theory]
    [InlineData("dialog-cancel")]
    [InlineData("dialog-close")]
    public async Task Dismiss_ButtonActivation_CompletesFalse(string testId)
    {
        var (host, result) = await ShowAsync();
        await host.Find($"[data-testid='{testId}']").ClickAsync(new());
        (await result).Should().BeFalse();
    }

    [Fact]
    public async Task Dismiss_BrowserCallback_CompletesFalse()
    {
        var (host, result) = await ShowAsync();
        await host.InvokeAsync(() => host.FindComponent<ShopDialog>().Instance.DismissAsync());
        (await result).Should().BeFalse();
    }

    [Fact]
    public async Task Navigation_CompletedRouteChange_CancelsActiveAndQueuedRequests()
    {
        var (host, first) = await ShowAsync();
        var second = _dialogs.ConfirmAsync(new("Second", "Body", "Confirm"), Xunit.TestContext.Current.CancellationToken);
        await host.InvokeAsync(() => Services.GetRequiredService<NavigationManager>().NavigateTo("/next"));
        (await first).Should().BeFalse();
        (await second).Should().BeFalse();
        host.WaitForAssertion(() => host.FindAll("dialog").Should().BeEmpty());
    }

    [Fact]
    public async Task Disposal_HostRemoved_CancelsAllRequestsAndAllowsNewHost()
    {
        var (host, first) = await ShowAsync();
        var second = _dialogs.ConfirmAsync(new("Second", "Body", "Confirm"), Xunit.TestContext.Current.CancellationToken);
        await DisposeComponentsAsync();
        (await first.WaitAsync(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken)).Should().BeFalse();
        (await second.WaitAsync(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken)).Should().BeFalse();
        _dialogs.Current.Should().BeNull();
        var replacement = Render<ShopDialogHost>();
        replacement.Markup.Should().BeEmpty();
    }

    [Fact]
    public async Task Render_InitializationFails_CancelsRequestInsteadOfStrandingCaller()
    {
        var module = JSInterop.SetupModule("./js/shopDialog.js");
        module.Mode = JSRuntimeMode.Loose;
        module.SetupVoid("show", _ => true).SetException(new JSException("showModal failed"));
        var host = Render<ShopDialogHost>();
        Task<bool> result = null!;
        await host.InvokeAsync(() => { result = _dialogs.ConfirmAsync(new("Title", "Body", "Confirm")); });
        host.WaitForAssertion(() => result.IsCompleted.Should().BeTrue());
        (await result).Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_RepeatedCallback_CompletesOnlyOnce()
    {
        var calls = new List<bool>();
        var cut = Render<ShopConfirmDialog>(p => p
            .Add(x => x.Options, new("Title", "Body", "Confirm"))
            .Add(x => x.Completed, value => calls.Add(value)));
        await cut.Find("[data-testid='dialog-confirm']").ClickAsync(new());
        await cut.Find("[data-testid='dialog-cancel']").ClickAsync(new());
        calls.Should().Equal(true);
    }
}
