namespace NTSW_CENOPS.Components.Common.Notices;

public sealed record AppNotice(string Message, bool IsSuccess, long Id,
    string Title = "", bool IsChat = false, long? RoomId = null);

/// <summary>One bottom-left notification system for operations and chat. Newest is expanded.</summary>
public sealed class AppNoticeService : IDisposable
{
    public event Action? Changed;
    private readonly object _lock = new();
    private readonly List<AppNotice> _items = [];
    private readonly Dictionary<long, CancellationTokenSource> _timers = [];
    private long _nextId;
    private bool _disposed;
    public IReadOnlyList<AppNotice> Items { get { lock (_lock) return _items.ToArray(); } }
    public AppNotice? Current { get { lock (_lock) return _items.LastOrDefault(); } }

    public void Success(string text, TimeSpan? duration = null) =>
        Add(text, true, "عملیات با موفقیت انجام شد", false, null, duration ?? TimeSpan.FromSeconds(4));
    public void Info(string text, TimeSpan? duration = null) =>
        Add(text, true, "اطلاع نمودار", false, null, duration ?? TimeSpan.FromSeconds(5));
    public void Error(string text, TimeSpan? duration = null) =>
        Add(text, false, "عملیات انجام نشد", false, null, duration ?? TimeSpan.FromSeconds(7));
    public void Chat(string author, string preview, long roomId) =>
        Add(preview, true, author, true, roomId, TimeSpan.FromSeconds(10));

    private void Add(string message, bool success, string title, bool chat, long? roomId, TimeSpan duration)
    {
        CancellationTokenSource timer;
        long id;
        lock (_lock)
        {
            if (_disposed) return;
            id = ++_nextId;
            _items.Add(new AppNotice(message, success, id, title, chat, roomId));
            timer = new CancellationTokenSource();
            _timers.Add(id, timer);
            while (_items.Count > 6)
            {
                var oldest = _items[0]; _items.RemoveAt(0);
                if (_timers.Remove(oldest.Id, out var oldTimer)) { oldTimer.Cancel(); oldTimer.Dispose(); }
            }
        }
        Changed?.Invoke();
        _ = ExpireAsync(id, duration, timer.Token);
    }
    private async Task ExpireAsync(long id, TimeSpan delay, CancellationToken token)
    {
        try { await Task.Delay(delay, token); if (!token.IsCancellationRequested) Dismiss(id); }
        catch (OperationCanceledException) { }
    }
    public void Dismiss() { var current = Current; if (current is not null) Dismiss(current.Id); }
    public void Dismiss(long id)
    {
        bool changed;
        lock (_lock)
        {
            changed = _items.RemoveAll(n => n.Id == id) > 0;
            if (_timers.Remove(id, out var timer)) { timer.Cancel(); timer.Dispose(); }
        }
        if (changed) Changed?.Invoke();
    }
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            foreach (var timer in _timers.Values) { timer.Cancel(); timer.Dispose(); }
            _timers.Clear(); _items.Clear();
        }
    }
}
