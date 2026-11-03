namespace NTSW_CENOPS.Domain.Chat;

/// <summary>A persistent message-to-file link. Authorization verifies this association before serving bytes.</summary>
public sealed class ChatMessageAttachment
{
    public long MessageId { get; set; }
    public Guid FileId { get; set; }
    public int Position { get; set; }
}
