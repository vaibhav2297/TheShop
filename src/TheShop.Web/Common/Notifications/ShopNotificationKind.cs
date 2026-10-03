namespace TheShop.Web.Common.Notifications;

/// <summary>The meaning of a notification, independent of its visual treatment.</summary>
public enum ShopNotificationKind
{
    /// <summary>Routine information.</summary>
    Info,
    /// <summary>An operation completed successfully.</summary>
    Success,
    /// <summary>An operation needs attention or completed partially.</summary>
    Warning,
    /// <summary>An operation failed.</summary>
    Error
}
