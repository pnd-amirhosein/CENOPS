using NTSW_CENOPS.Application.Chat;

namespace NTSW_CENOPS.Components.Chat;

/// <summary>Scoped, circuit-wide unread/connection state. Does not own a websocket or a room's messages.</summary>
public sealed class ChatInboxState
{
    public bool Connected { get; private set; }
    public int ConnectionEpoch { get; private set; }
    public int UnreadCount { get; private set; }
    public event Action? Changed;
    public event Action<string, long?>? EventReceived;
    public event Action<long, string, string, bool>? TypingReceived;
    public event Action? RefreshRequested;

    public void SetConnection(bool connected)
    {
        if (Connected == connected) return;
        Connected = connected;
        if (connected) ConnectionEpoch++;
        Changed?.Invoke();
    }
    public void SetUnread(IReadOnlyList<ChatRoomSummaryDto> rooms)
    {
        var count = rooms.Sum(room => room.UnreadCount);
        if (UnreadCount == count) return;
        UnreadCount = count;
        Changed?.Invoke();
    }
    public void Publish(string name, long? roomId) => EventReceived?.Invoke(name, roomId);
    public void PublishTyping(long roomId, string username, string displayName, bool typing) =>
        TypingReceived?.Invoke(roomId, username, displayName, typing);
    public void RequestRefresh() => RefreshRequested?.Invoke();
}
