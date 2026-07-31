using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Render-prop component that exposes the busy state of one or more named <see cref="BusyState"/>
/// keys to its child content. Use this to conditionally show spinners or disable buttons scoped
/// to a specific operation rather than the global overlay. Set <see cref="Key"/> for a single
/// operation, or <see cref="Keys"/> when several operations should drive the same indicator.
/// </summary>
public partial class BusyFor : ComponentBase, IDisposable
{
    [Inject] private BusyState BusyState { get; set; } = default!;

    /// <summary>
    /// The single busy key to watch. Ignored when <see cref="Keys"/> is supplied; exactly one of
    /// the two must be set.
    /// </summary>
    [Parameter] public string? Key { get; set; }

    /// <summary>
    /// Busy keys to watch together — the child content is told it is busy while <em>any</em> of
    /// them has an operation in flight. Use this when one indicator covers several mutations (e.g.
    /// a list that both deletes items and changes their status), instead of nesting a
    /// <see cref="BusyFor"/> per key.
    /// </summary>
    [Parameter] public IReadOnlyList<string>? Keys { get; set; }

    /// <summary>The content to render, receiving whether any watched key is currently busy.</summary>
    [Parameter, EditorRequired] public RenderFragment<bool> ChildContent { get; set; } = default!;

    private bool _isBusy;
    private string[] _watchedKeys = [];

    /// <inheritdoc/>
    protected override void OnInitialized() => BusyState.Changed += OnChanged;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        _watchedKeys = Keys is { Count: > 0 } keys ? [.. keys] : Key is not null ? [Key] : [];

        if (_watchedKeys.Length == 0)
            throw new InvalidOperationException(
                $"{nameof(BusyFor)} requires either {nameof(Key)} or a non-empty {nameof(Keys)}.");

        // Recomputed on every parameter set rather than once on init, so swapping the watched keys
        // re-reads the current state instead of keeping the previous keys' verdict.
        _isBusy = IsAnyWatchedKeyBusy();
    }

    private bool IsAnyWatchedKeyBusy() => _watchedKeys.Any(BusyState.IsBusy);

    private void OnChanged()
    {
        var next = IsAnyWatchedKeyBusy();
        if (next == _isBusy) return;
        _isBusy = next;
        InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc/>
    public void Dispose() => BusyState.Changed -= OnChanged;
}
