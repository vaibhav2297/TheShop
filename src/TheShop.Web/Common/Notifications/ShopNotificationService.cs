namespace TheShop.Web.Common.Notifications;

/// <summary>Scoped notification state that survives navigation and expires messages after five seconds of inactivity.</summary>
public sealed class ShopNotificationService(TimeProvider clock) : IShopNotificationService, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];
    private object? _host;
    private bool _disposed;

    /// <summary>Raised after visible messages or host ownership changes.</summary>
    public event Action? Changed;

    /// <summary>A snapshot in oldest-first order.</summary>
    public IReadOnlyList<ShopNotificationMessage> Messages
    {
        get { lock (_gate) return _entries.Select(entry => entry.Message).ToArray(); }
    }

    /// <inheritdoc/>
    public void Show(string message, ShopNotificationKind kind = ShopNotificationKind.Info)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        lock (_gate)
        {
            if (_disposed) return;
            if (_entries.Any(entry => entry.Message.Text == message && entry.Message.Kind == kind)) return;
            if (_entries.Count == 5)
            {
                var oldest = _entries.FirstOrDefault(entry => !entry.Paused);
                if (oldest is null) return;
                Remove(oldest);
            }
            var entry = new Entry(new(Guid.NewGuid(), message, kind));
            _entries.Add(entry);
            entry.Due = clock.GetUtcNow() + Lifetime;
            entry.Timer = clock.CreateTimer(_ => Expire(entry), null, Lifetime, Timeout.InfiniteTimeSpan);
        }
        Changed?.Invoke();
    }

    /// <summary>Removes the matching message; stale or repeated dismissals are harmless.</summary>
    public void Dismiss(Guid id)
    {
        lock (_gate)
        {
            var entry = _entries.Find(entry => entry.Message.Id == id);
            if (entry is null) return;
            Remove(entry);
        }
        Changed?.Invoke();
    }

    /// <summary>Transfers rendering to one host without clearing messages across layout navigation.</summary>
    public void AttachHost(object host)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _host = host;
            ResumeAll();
        }
        Changed?.Invoke();
    }

    /// <summary>Returns whether this host currently owns rendering and interaction.</summary>
    public bool IsHost(object host) { lock (_gate) return ReferenceEquals(_host, host); }

    /// <summary>Releases the current host; remaining lifetimes continue while no host is mounted.</summary>
    public void DetachHost(object host)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_host, host)) return;
            _host = null;
            ResumeAll();
        }
        Changed?.Invoke();
    }

    /// <summary>Pauses on hover/focus; leaving both grants five fresh seconds. Stale hosts cannot change timers.</summary>
    public void SetPaused(object host, Guid id, bool paused)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_host, host)) return;
            var entry = _entries.Find(entry => entry.Message.Id == id);
            if (entry is null || entry.Paused == paused) return;
            entry.Paused = paused;
            if (paused) entry.Timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            else Restart(entry);
        }
    }

    private void Expire(Entry entry)
    {
        lock (_gate)
        {
            if (_disposed || entry.Paused || !_entries.Contains(entry)) return;
            var remaining = entry.Due - clock.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                entry.Timer?.Change(remaining, Timeout.InfiniteTimeSpan);
                return;
            }
            Remove(entry);
        }
        Changed?.Invoke();
    }

    private void ResumeAll()
    {
        foreach (var entry in _entries.Where(entry => entry.Paused))
        {
            entry.Paused = false;
            Restart(entry);
        }
    }

    private void Restart(Entry entry)
    {
        entry.Due = clock.GetUtcNow() + Lifetime;
        entry.Timer?.Change(Lifetime, Timeout.InfiniteTimeSpan);
    }

    private void Remove(Entry entry)
    {
        _entries.Remove(entry);
        entry.Timer?.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var entry in _entries) entry.Timer?.Dispose();
            _entries.Clear();
            _host = null;
            Changed = null;
        }
    }

    private sealed class Entry(ShopNotificationMessage message)
    {
        public ShopNotificationMessage Message { get; } = message;
        public ITimer? Timer { get; set; }
        public DateTimeOffset Due { get; set; }
        public bool Paused { get; set; }
    }
}
