namespace TheShop.Web.Tests.Common.Notifications;

internal sealed class ManualNotificationClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
    private readonly List<ManualTimer> _timers = [];
    public override DateTimeOffset GetUtcNow() => _now;
    public int ActiveTimerCount => _timers.Count(timer => !timer.Disposed);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan duration)
    {
        _now += duration;
        foreach (var timer in _timers.Where(timer => !timer.Disposed && timer.Due <= _now).ToArray())
            timer.Fire();
    }

    private sealed class ManualTimer(ManualNotificationClock clock, TimerCallback callback, object? state) : ITimer
    {
        public bool Disposed { get; private set; }
        public DateTimeOffset Due { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (Disposed) return false;
            Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.GetUtcNow() + dueTime;
            return true;
        }
        public void Fire()
        {
            Due = DateTimeOffset.MaxValue;
            callback(state);
        }
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
