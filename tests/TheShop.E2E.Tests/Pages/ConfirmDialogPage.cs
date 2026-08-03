using Microsoft.Playwright;
using TheShop.Web.Resources;

namespace TheShop.E2E.Tests.Pages;

/// <summary>
/// Page object for <c>ShopConfirmDialog</c> — the shared confirm/cancel prompt behind every
/// deactivate and delete confirmation. Scoped to MudBlazor's <c>.mud-dialog</c> surface, the same
/// pragmatic exception <see cref="ShopPage"/> already makes for <c>.mud-layout</c> and
/// <c>.mud-snackbar</c>: <c>MudDialog</c> exposes no project-owned class or test id to hang a
/// locator on. Scoping matters here rather than being merely tidy — the dialog's confirm button
/// carries the same label ("Delete") as the bulk-action-bar button still rendered behind it.
/// </summary>
public sealed class ConfirmDialogPage(IPage page)
{
    private IPage Page { get; } = page;

    /// <summary>The dialog surface itself — locate for open/closed and count assertions.</summary>
    public ILocator Surface => Page.Locator(".mud-dialog");

    /// <summary>Locator for the dialog's title text, exact match.</summary>
    public ILocator Title(string text) => Surface.GetByText(text, new() { Exact = true });

    /// <summary>Locator for the dialog's body text, exact match.</summary>
    public ILocator Body(string text) => Surface.GetByText(text, new() { Exact = true });

    /// <summary>Waits for the dialog to be shown.</summary>
    public Task WaitForOpenAsync() => Surface.WaitForAsync(new() { Timeout = 15_000 });

    /// <summary>Waits for the dialog to be torn down after a confirm or cancel.</summary>
    public Task WaitForClosedAsync() =>
        Surface.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 15_000 });

    /// <summary>
    /// Confirms the dialog. The confirm label is caller-supplied per prompt (Deactivate, Delete),
    /// so the journey passes the same <c>Strings</c> key the page passed to the dialog.
    /// </summary>
    public Task ConfirmAsync(string confirmLabel) =>
        Surface.GetByRole(AriaRole.Button, new() { Name = confirmLabel }).ClickAsync();

    /// <summary>Dismisses the dialog through its Cancel button.</summary>
    public Task CancelAsync() =>
        Surface.GetByRole(AriaRole.Button, new() { Name = Strings.Cancel }).ClickAsync();
}
