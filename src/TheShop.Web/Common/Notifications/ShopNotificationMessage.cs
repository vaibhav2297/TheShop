namespace TheShop.Web.Common.Notifications;

/// <summary>Already-localized plain text with a stable identity for dismissal.</summary>
public sealed record ShopNotificationMessage(Guid Id, string Text, ShopNotificationKind Kind);
