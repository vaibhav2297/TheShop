namespace TheShop.Web.Common.Notifications;

/// <summary>Publishes transient, encoded notifications without owning feature state or localization.</summary>
public interface IShopNotificationService
{
    /// <summary>Shows localized plain text. Identical active messages are coalesced; at most five messages are retained.</summary>
    void Show(string message, ShopNotificationKind kind = ShopNotificationKind.Info);
}
