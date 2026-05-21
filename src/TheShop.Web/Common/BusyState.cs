namespace TheShop.Web.Common;

public sealed class BusyState
{
    private readonly Dictionary<string, int> _counts = [];
    private readonly object _gate = new();

    public event Action? Changed;

    public bool IsBusy(string key)
    {
        lock (_gate)
            return _counts.TryGetValue(key, out var c) && c > 0;
    }

    public bool IsAnyBusy
    {
        get
        {
            lock (_gate)
                return _counts.Values.Any(c => c > 0);
        }
    }

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
