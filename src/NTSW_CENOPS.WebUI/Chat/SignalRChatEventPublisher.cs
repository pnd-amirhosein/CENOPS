using NTSW_CENOPS.Application.Chat;
using Microsoft.AspNetCore.SignalR;

namespace NTSW_CENOPS.Chat;

/// <summary>
/// Sends to *current member user IDs* across all their connected devices. Never to an admin
/// merely because of a role. A transient notification failure cannot roll back a saved message;
/// the client must resync room history after reconnect.
/// </summary>
public sealed class SignalRChatEventPublisher(
    IHubContext<ChatHub> hub, ILogger<SignalRChatEventPublisher> logger) : IChatEventPublisher
{
    private IClientProxy ForUsers(IReadOnlyCollection<int> ids) =>
        hub.Clients.Users(ids.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());

    private async Task DeliverAsync(string eventName, object?[] args, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        try { await ForUsers(ids).SendCoreAsync(eventName, args, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* DB operation is already complete. */ }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Chat SignalR event {EventName} delivery failed; clients should resync", eventName);
        }
    }

    public Task RoomUpdatedAsync(long roomId, IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
        DeliverAsync("ChatRoomUpdated", [roomId], userIds, ct);

    public Task MessageCreatedAsync(ChatMessageDto message, IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
        DeliverAsync("ChatMessageCreated", [message], userIds, ct);

    public Task MessageUpdatedAsync(ChatMessageDto message, IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
        DeliverAsync("ChatMessageUpdated", [message], userIds, ct);

    public Task MessageDeletedAsync(long roomId, long messageId, IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
        DeliverAsync("ChatMessageDeleted", [roomId, messageId], userIds, ct);

    public Task ReadUpdatedAsync(long roomId, string username, long messageId, IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
        DeliverAsync("ChatReadUpdated", [roomId, username, messageId], userIds, ct);
}
