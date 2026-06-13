namespace TheShop.Web.Common;

/// <summary>
/// Scoped service that tracks in-flight async operations by named key. Components
/// use this to show/hide loading indicators without coupling to individual async methods.
/// Multiple concurrent callers for the same key are reference-counted — the busy state
/// clears only when the last caller finishes.
/// </summary>
public sealed class BusyState
{
    private readonly Dictionary<string, int> _counts = [];
    private readonly object _gate = new();

    /// <summary>
    /// Fires when any key transitions into or out of the busy state.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Returns <c>true</c> when at least one in-flight operation is registered under <paramref name="key"/>.
    /// </summary>
    public bool IsBusy(string key)
    {
        lock (_gate)
            return _counts.TryGetValue(key, out var c) && c > 0;
    }

    /// <summary>
    /// Returns <c>true</c> when any key is currently busy.
    /// </summary>
    public bool IsAnyBusy
    {
        get
        {
            lock (_gate)
                return _counts.Values.Any(c => c > 0);
        }
    }

    /// <summary>
    /// Increments the busy counter for <paramref name="key"/>, runs <paramref name="action"/>,
    /// then decrements the counter. Fires <see cref="Changed"/> on transitions.
    /// </summary>
    public async Task RunAsync(string key, Func<Task> action)
    {
        var transitioned = Enter(key);
        if (transitioned) Changed?.Invoke();
        try
        {
            await action();
        }
        finally
        {
            transitioned = Exit(key);
            if (transitioned) Changed?.Invoke();
        }
    }

    /// <summary>
    /// Increments the busy counter for <paramref name="key"/>, runs <paramref name="action"/>,
    /// then decrements the counter. Returns the action's result.
    /// </summary>
    public async Task<T> RunAsync<T>(string key, Func<Task<T>> action)
    {
        var transitioned = Enter(key);
        if (transitioned) Changed?.Invoke();
        try
        {
            return await action();
        }
        finally
        {
            transitioned = Exit(key);
            if (transitioned) Changed?.Invoke();
        }
    }

    private bool Enter(string key)
    {
        lock (_gate)
        {
            var prior = _counts.TryGetValue(key, out var c) ? c : 0;
            _counts[key] = prior + 1;
            return prior == 0;
        }
    }

    private bool Exit(string key)
    {
        lock (_gate)
        {
            if (!_counts.TryGetValue(key, out var c) || c <= 0)
                return false;
            var next = c - 1;
            if (next == 0)
            {
                _counts.Remove(key);
                return true;
            }
            _counts[key] = next;
            return false;
        }
    }
}
