using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.Notifications;

namespace TheShop.Web.Components.Common;

/// <summary>Encoded notification copy and a dismiss action; hover and focus independently pause expiry.</summary>
public partial class ShopNotification : ComponentBase
{
    /// <summary>Localized plain text and notification meaning.</summary>
    [Parameter, EditorRequired] public ShopNotificationMessage Message { get; set; } = default!;
    /// <summary>Raised when the dismiss button is activated.</summary>
    [Parameter] public EventCallback OnDismiss { get; set; }
    /// <summary>True while either pointer hover or keyboard focus is inside this notification.</summary>
    [Parameter] public EventCallback<bool> OnPauseChanged { get; set; }

    private bool _hovered;
    private bool _focused;

    private Task SetHoveredAsync(bool value)
    {
        _hovered = value;
        return OnPauseChanged.InvokeAsync(_hovered || _focused);
    }

    private Task SetFocusedAsync(bool value)
    {
        _focused = value;
        return OnPauseChanged.InvokeAsync(_hovered || _focused);
    }
}
