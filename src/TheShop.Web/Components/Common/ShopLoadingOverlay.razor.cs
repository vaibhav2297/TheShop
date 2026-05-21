using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Full-page loading overlay that appears whenever the <c>"global"</c> busy key is active
/// in <see cref="BusyState"/>. Mounted once in <c>MainLayout</c>.
/// </summary>
public partial class ShopLoadingOverlay : ComponentBase, IDisposable
{
    [Inject] private BusyState BusyState { get; set; } = default!;

    private bool _isBusy;

    protected override void OnInitialized()
    {
        BusyState.Changed += OnChanged;
        _isBusy = BusyState.IsBusy(BusyKeys.Global);
    }

    private void OnChanged()
    {
        var next = BusyState.IsBusy(BusyKeys.Global);
        if (next == _isBusy) return;
        _isBusy = next;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() => BusyState.Changed -= OnChanged;
}
