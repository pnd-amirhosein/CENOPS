namespace NTSW_CENOPS.Domain.Chat;

// One conversation model for both direct conversations and groups.
// Kind is immutable: a group that later has two members must never become a DM.
public enum ChatRoomKind : byte { Direct = 1, Group = 2 }

public sealed class ChatRoom
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public ChatRoomKind Kind { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int? DirectLowUserId { get; set; }
    public int? DirectHighUserId { get; set; }
}

public sealed class ChatRoomMember
{
    public long RoomId { get; set; }
    public int UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public long? LastReadMessageId { get; set; }
}

public sealed class ChatMessage
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public int SenderUserId { get; set; }
    public string EncryptedContent { get; set; } = string.Empty;
    public long? ReplyToMessageId { get; set; }
    public string? ForwardedFromDisplayName { get; set; }
    public string? ForwardedFromUsername { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
