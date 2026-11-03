namespace NTSW_CENOPS.Components.Chat;

/// <summary>One modal chat workspace per interactive Blazor circuit; app route remains mounted.</summary>
public sealed class ChatWorkspaceState
{
    public bool HasOpened { get; private set; }
    public long? ActiveRoomId { get; private set; }
    public long? RequestedRoomId { get; private set; }
    public string? RequestedUsername { get; private set; }
    public event Action? Changed;

    public void Open() { HasOpened = true; Changed?.Invoke(); }
    public void OpenDirect(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        RequestedUsername = username.Trim();
        HasOpened = true;
        Changed?.Invoke();
    }
    public string? TakeRequestedUsername() { var username = RequestedUsername; RequestedUsername = null; return username; }
    public void OpenRoom(long roomId) { RequestedRoomId = roomId; HasOpened = true; Changed?.Invoke(); }
    public void SetActiveRoom(long? roomId) { ActiveRoomId = roomId; }
    public long? TakeRequestedRoom() { var id = RequestedRoomId; RequestedRoomId = null; return id; }
    public void Close() { HasOpened = false; ActiveRoomId = null; RequestedRoomId = null; RequestedUsername = null; Changed?.Invoke(); }
}
