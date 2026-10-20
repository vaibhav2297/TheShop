using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.Dialogs;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Encoded confirmation copy with explicit confirm and safe cancel actions.</summary>
public partial class ShopConfirmDialog : ComponentBase
{
    /// <summary>Already-localized labels and the action's destructive intent.</summary>
    [Parameter, EditorRequired] public ShopConfirmationOptions Options { get; set; } = default!;

    /// <summary>True only for the confirm button; all dismissal paths return false.</summary>
    [Parameter] public EventCallback<bool> Completed { get; set; }

    private readonly string _bodyId = $"shop-dialog-body-{Guid.NewGuid():N}";
    private ShopColor ConfirmColor => Options.Destructive ? ShopColor.Error : ShopColor.Primary;
    private bool _completed;

    private Task ConfirmAsync() => CompleteAsync(true);
    private Task CancelAsync() => CompleteAsync(false);

    private Task CompleteAsync(bool confirmed)
    {
        if (_completed)
            return Task.CompletedTask;
        _completed = true;
        return Completed.InvokeAsync(confirmed);
    }
}
