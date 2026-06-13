using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Render-prop component that exposes the busy state of a named <see cref="BusyState"/> key
/// to its child content. Use this to conditionally show spinners or disable buttons scoped
/// to a specific operation rather than the global overlay.
/// </summary>
public partial class BusyFor : ComponentBase, IDisposable
{
    [Inject] private BusyState BusyState { get; set; } = default!;

    [Parameter, EditorRequired] public string Key { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment<bool> ChildContent { get; set; } = default!;

    private bool _isBusy;

    protected override void OnInitialized()
    {
        BusyState.Changed += OnChanged;
        _isBusy = BusyState.IsBusy(Key);
    }

    private void OnChanged()
    {
        var next = BusyState.IsBusy(Key);
        if (next == _isBusy) return;
        _isBusy = next;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() => BusyState.Changed -= OnChanged;
}
