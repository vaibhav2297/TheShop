using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopConfirmDialog"/> — the reusable confirm/cancel dialog behind every
/// deactivate and delete confirmation (RULE-7, RULE-14). Confirming closes the dialog with a
/// non-cancelled, truthy result; cancelling or dismissing leaves everything untouched (Behavior 7,
/// AC-14). All text is caller-supplied and already localized (plan §5 Decision 10) — the dialog
/// itself resolves no resource keys, so this class asserts wiring, not translation.
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
/// <remarks>
/// <see cref="MudDialog"/> resolves its container through an internal cascading parameter that only
/// a real <see cref="MudDialogProvider"/> + <see cref="IDialogService"/> pipeline supplies (mirroring
/// how <c>ManageBrands.razor.cs</c> shows this dialog) — a bare <see cref="IMudDialogInstance"/>
/// substitute cascaded by hand renders empty markup, so these tests go through the provider.
/// </remarks>
public class ShopConfirmDialogTests : TestContext
{
    public ShopConfirmDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Reference)> ShowDialogAsync(
        string titleText = "Delete Elf Bar?",
        string bodyText = "This cannot be undone.",
        string confirmLabel = "Delete",
        string? cancelLabel = null,
        Color confirmColor = Color.Error)
    {
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ShopConfirmDialog>
        {
            { x => x.TitleText, titleText },
            { x => x.BodyText, bodyText },
            { x => x.ConfirmLabel, confirmLabel },
            { x => x.ConfirmColor, confirmColor },
        };
        if (cancelLabel is not null)
            parameters.Add(x => x.CancelLabel, cancelLabel);

        IDialogReference reference = null!;
        await provider.InvokeAsync(async () => reference = await dialogService.ShowAsync<ShopConfirmDialog>(string.Empty, parameters));
        return (provider, reference);
    }

    // =========================================================================
    // Renders the caller-supplied, already-localized text (RULE-7, RULE-14)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_Always_ShowsTheSuppliedTitleAndBody()
    {
        var (provider, _) = await ShowDialogAsync(titleText: "Delete Elf Bar?", bodyText: "This cannot be undone.");

        provider.Markup.Should().Contain("Delete Elf Bar?");
        provider.Markup.Should().Contain("This cannot be undone.");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_Always_ShowsTheSuppliedConfirmLabel()
    {
        var (provider, _) = await ShowDialogAsync(confirmLabel: "Delete 3 brands");

        provider.Markup.Should().Contain("Delete 3 brands");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Render_WithoutAnExplicitCancelLabel_FallsBackToTheDefaultCancelText()
    {
        var (provider, _) = await ShowDialogAsync();

        provider.Markup.Should().Contain(Strings.Cancel);
    }

    // =========================================================================
    // Confirm — closes with a non-cancelled result the caller reads as "confirmed" (RULE-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ClickConfirm_WhenActivated_ClosesTheDialogWithANonCancelledResult()
    {
        var (provider, reference) = await ShowDialogAsync(confirmLabel: "Delete");

        await provider.InvokeAsync(() => provider.FindAll("button").First(b => b.TextContent.Trim() == "Delete").Click());

        var result = await reference.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
    }

    // =========================================================================
    // Cancel — nothing changes, the caller reads a cancelled result (RULE-7, AC-14)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task ClickCancel_WhenActivated_CancelsTheDialogWithoutClosingItAsConfirmed()
    {
        var (provider, reference) = await ShowDialogAsync();

        await provider.InvokeAsync(() => provider.FindAll("button").First(b => b.TextContent.Trim() == Strings.Cancel).Click());

        var result = await reference.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
    }
}
