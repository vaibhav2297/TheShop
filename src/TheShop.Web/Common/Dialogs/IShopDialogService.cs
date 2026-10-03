namespace TheShop.Web.Common.Dialogs;

/// <summary>Web-only confirmation requests independent of a UI vendor.</summary>
public interface IShopDialogService
{
    /// <summary>Returns true only after explicit confirmation; dismissal, cancellation, or host teardown returns false.</summary>
    Task<bool> ConfirmAsync(ShopConfirmationOptions options, CancellationToken cancellationToken = default);
}
