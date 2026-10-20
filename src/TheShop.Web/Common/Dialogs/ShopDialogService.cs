namespace TheShop.Web.Common.Dialogs;

/// <summary>Serializes confirmation requests and cancels pending callers when the active host leaves.</summary>
public sealed class ShopDialogService : IShopDialogService
{
    private readonly object _gate = new();
    private readonly List<ShopDialogRequest> _requests = [];
    private object? _host;

    /// <summary>Raised when the active request changes; hosts marshal rendering onto their dispatcher.</summary>
    public event Action? Changed;

    /// <summary>The first pending request, or null when no confirmation is waiting.</summary>
    public ShopDialogRequest? Current
    {
        get { lock (_gate) return _requests.FirstOrDefault(); }
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(ShopConfirmationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var request = new ShopDialogRequest(options);
        lock (_gate)
        {
            if (_host is null || cancellationToken.IsCancellationRequested)
                return false;
            _requests.Add(request);
        }

        using var registration = cancellationToken.Register(() => Complete(request.Id, false));
        Changed?.Invoke();
        return await request.Completion.Task;
    }

    /// <summary>Completes only the identified pending request. Repeated or stale completion is harmless.</summary>
    public void Complete(Guid id, bool confirmed)
    {
        lock (_gate)
        {
            var request = _requests.FirstOrDefault(x => x.Id == id);
            if (request is null || (confirmed && request != _requests[0]))
                return;
            _requests.Remove(request);
            request.Completion.TrySetResult(confirmed);
        }
        Changed?.Invoke();
    }

    /// <summary>Cancels every outstanding request, including queued ones, on completed navigation.</summary>
    public void CancelAll()
    {
        lock (_gate)
        {
            foreach (var request in _requests)
                request.Completion.TrySetResult(false);
            _requests.Clear();
        }
        Changed?.Invoke();
    }

    /// <summary>Reports whether this is the current layout's host.</summary>
    public bool IsHost(object host)
    {
        lock (_gate) return ReferenceEquals(_host, host);
    }

    /// <summary>Transfers ownership to a new layout, cancelling the previous layout's requests.</summary>
    public void AttachHost(object host)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_host, host))
                return;
            foreach (var request in _requests)
                request.Completion.TrySetResult(false);
            _requests.Clear();
            _host = host;
        }
        Changed?.Invoke();
    }

    /// <summary>Releases the matching host and resolves all its pending callers as cancelled.</summary>
    public void DetachHost(object host)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_host, host))
                return;
            _host = null;
            foreach (var request in _requests)
                request.Completion.TrySetResult(false);
            _requests.Clear();
        }
        Changed?.Invoke();
    }
}
