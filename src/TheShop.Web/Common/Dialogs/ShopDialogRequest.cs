namespace TheShop.Web.Common.Dialogs;

/// <summary>An immutable identity and copy snapshot for one queued confirmation.</summary>
public sealed class ShopDialogRequest(ShopConfirmationOptions options)
{
    /// <summary>Identifies the request so stale UI events cannot complete its successor.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Localized presentation values supplied by the caller.</summary>
    public ShopConfirmationOptions Options { get; } = options;

    internal TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
