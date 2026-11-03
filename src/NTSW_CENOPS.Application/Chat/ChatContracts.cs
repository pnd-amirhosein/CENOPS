using System.Security.Claims;
using NTSW_CENOPS.Domain.Chat;

namespace NTSW_CENOPS.Application.Chat;

// Usernames are the *public* participant identifiers. Identity integer IDs stay server-side.
public sealed record CreateChatRoomRequest(IReadOnlyList<string> Usernames, string? Title = null);
public sealed record SendChatMessageRequest(string Content, long? RoomId = null, string? Username = null, long? ReplyToMessageId = null, IReadOnlyList<Guid>? AttachmentIds = null);
public sealed record ForwardChatMessageRequest(long MessageId, long? RoomId = null, string? Username = null);
public sealed record ChatParticipantDto(string Username, string DisplayName);
public sealed record ChatRoomDto(long Id, string? Title, ChatRoomKind Kind,
    string CreatedByUsername, DateTimeOffset CreatedAt, IReadOnlyList<ChatParticipantDto> Members,
    long? LastReadMessageId);
public sealed record ChatRoomSummaryDto(long Id, string? Title, ChatRoomKind Kind,
    DateTimeOffset CreatedAt, IReadOnlyList<ChatParticipantDto> Members,
    long? LastMessageId, DateTimeOffset? LastMessageAt, int UnreadCount);
public sealed record ChatMessageDto(long Id, long RoomId, string SenderUsername,
    string? Content, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt, DateTimeOffset? DeletedAt,
    string? SenderDisplayName = null, long? ReplyToMessageId = null, string? ReplySenderUsername = null,
    string? ReplySenderDisplayName = null, string? ReplyPreview = null,
    string? ForwardedFromDisplayName = null, string? ForwardedFromUsername = null,
    IReadOnlyList<Guid>? AttachmentIds = null);

public sealed class ChatException(string message) : Exception(message) { }

public interface IChatRoomService
{
    Task<ChatRoomDto> CreateAsync(CreateChatRoomRequest request, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatRoomDto> GetOrCreateDirectAsync(string username, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatRoomDto> GetAsync(long roomId, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<ChatRoomSummaryDto>> MyRoomsAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatRoomDto> RenameGroupAsync(long roomId, string title, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatRoomDto> AddMembersAsync(long roomId, IReadOnlyList<string> usernames, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatRoomDto> RemoveMemberAsync(long roomId, string username, ClaimsPrincipal actor, CancellationToken ct = default);
    Task LeaveAsync(long roomId, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> IsMemberAsync(long roomId, ClaimsPrincipal actor, CancellationToken ct = default);
}

public interface IChatMessageService
{
    Task<ChatMessageDto> SendAsync(SendChatMessageRequest request, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatMessageDto> ForwardAsync(ForwardChatMessageRequest request, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessageDto>> HistoryAsync(long roomId, long? beforeId, int take, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatMessageDto> EditAsync(long messageId, string content, ClaimsPrincipal actor, CancellationToken ct = default);
    Task DeleteAsync(long messageId, ClaimsPrincipal actor, CancellationToken ct = default);
    Task MarkReadAsync(long roomId, long messageId, ClaimsPrincipal actor, CancellationToken ct = default);
}

// Only the WebUI implementation knows about SignalR. Services publish after DB commits.
public interface IChatEventPublisher
{
    Task RoomUpdatedAsync(long roomId, IReadOnlyCollection<int> userIds, CancellationToken ct = default);
    Task MessageCreatedAsync(ChatMessageDto message, IReadOnlyCollection<int> userIds, CancellationToken ct = default);
    Task MessageUpdatedAsync(ChatMessageDto message, IReadOnlyCollection<int> userIds, CancellationToken ct = default);
    Task MessageDeletedAsync(long roomId, long messageId, IReadOnlyCollection<int> userIds, CancellationToken ct = default);
    Task ReadUpdatedAsync(long roomId, string username, long messageId, IReadOnlyCollection<int> userIds, CancellationToken ct = default);
}

// Search results never contain messages from rooms outside the caller's membership.
public sealed record ChatMessageSearchHit(ChatMessageDto Message, string RoomTitle);
public sealed record ChatSearchResult(
    IReadOnlyList<ChatRoomSummaryDto> Rooms,
    IReadOnlyList<ChatParticipantDto> Users,
    IReadOnlyList<ChatMessageSearchHit> Messages);
public interface IChatSearchService
{
    Task<IReadOnlyList<ChatParticipantDto>> SearchUsersAsync(string query, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ChatSearchResult> SearchAsync(string query, ClaimsPrincipal actor, CancellationToken ct = default);
}
