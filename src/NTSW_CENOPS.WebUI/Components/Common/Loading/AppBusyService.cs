namespace NTSW_CENOPS.Components.Common.Loading;

/// <summary>A scoped busy-state coordinator: overlapping operations own independent leases.</summary>
public sealed class AppBusyService
{
    private readonly Dictionary<long, string> _active = new();
    private long _nextId;
    public event Action? Changed;
    public bool IsBusy => _active.Count != 0;
    public string Label => _active.Count == 0 ? "در حال بارگذاری..." : _active.Last().Value;

    public IDisposable Begin(string label = "در حال بارگذاری...")
    {
        var id = ++_nextId;
        _active.Add(id, label);
        Changed?.Invoke();
        return new Lease(this, id);
    }

    private void End(long id)
    {
        if (_active.Remove(id)) Changed?.Invoke();
    }

    private sealed class Lease(AppBusyService owner, long id) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner.End(id);
        }
    }
}
