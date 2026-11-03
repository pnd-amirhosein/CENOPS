using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Chat;
using NTSW_CENOPS.Application.Files;
using NTSW_CENOPS.Domain.Chat;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Chat;

public sealed class ChatMessageService(
    IDbContextFactory<AppDbContext> factory, IAccessService access,
    IChatRoomService rooms, IDataProtectionProvider dataProtection,
    IChatEventPublisher events, IAppFileGateway fileGateway) : IChatMessageService
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("NTSW.CENOPS.ChatMessages.v1");

    public async Task<ChatMessageDto> SendAsync(SendChatMessageRequest request, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ids = ValidateAttachments(request.AttachmentIds);
        var content = ValidContent(request.Content, ids.Count > 0);
        if (request.RoomId.HasValue && request.RoomId.Value <= 0)
            throw new ChatException("شناسه گفتگو معتبر نیست.");
        var hasUsername = !string.IsNullOrWhiteSpace(request.Username);
        if ((request.RoomId is > 0) == hasUsername)
            throw new ChatException("فقط یکی از شناسه گفتگو یا نام کاربری گیرنده را وارد کنید.");
        var roomId = request.RoomId is > 0 ? request.RoomId.Value :
            (await rooms.GetOrCreateDirectAsync(request.Username!, actor, ct)).Id;

        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        await ChatSecurity.EnsureMemberAsync(db, roomId, me, ct);
        // The sender may attach only uploads they own, never arbitrary guessed file IDs.
        foreach (var id in ids)
        {
            var file = await fileGateway.FindAsync(id, ct);
            if (file?.OwnerKey != me.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new ChatException("فایل انتخاب‌شده متعلق به شما نیست یا پیدا نشد.");
        }
        ChatMessage? replied = null;
        string? replyUsername = null, replyDisplay = null, replyPreview = null;
        if (request.ReplyToMessageId is long replyId)
        {
            replied = await db.ChatMessages.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == replyId && x.RoomId == roomId && x.DeletedAt == null, ct)
                ?? throw new ChatException("پیام مورد پاسخ در این گفتگو پیدا نشد.");
            var replyAuthor = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Id == replied.SenderUserId).Select(x => new { x.UserName, x.DisplayName }).SingleAsync(ct);
            replyUsername = replyAuthor.UserName;
            replyDisplay = replyAuthor.DisplayName;
            replyPreview = _protector.Unprotect(replied.EncryptedContent);
        }
        var msg = new ChatMessage
        {
            RoomId = roomId, SenderUserId = me, ReplyToMessageId = replied?.Id,
            EncryptedContent = _protector.Protect(content), CreatedAt = DateTimeOffset.UtcNow
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync(ct);
        foreach (var (fileId, position) in ids.Select((id, i) => (id, i)))
            db.ChatMessageAttachments.Add(new ChatMessageAttachment { MessageId = msg.Id, FileId = fileId, Position = position });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var sender = await db.Users.AsNoTracking().Where(x => x.Id == me)
            .Select(x => new { x.UserName, x.DisplayName }).SingleAsync(ct);
        var dto = new ChatMessageDto(msg.Id, msg.RoomId, sender.UserName ?? "", content, msg.CreatedAt, null, null,
            sender.DisplayName, replied?.Id, replyUsername, replyDisplay, replyPreview,
            AttachmentIds: ids);
        await events.MessageCreatedAsync(dto, await ChatSecurity.MembersAsync(db, roomId, ct), ct);
        return dto;
    }

    public async Task<ChatMessageDto> ForwardAsync(ForwardChatMessageRequest request, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MessageId <= 0) throw new ChatException("پیام معتبر نیست.");
        if ((request.RoomId is > 0) == !string.IsNullOrWhiteSpace(request.Username))
            throw new ChatException("فقط یک مقصد برای ارسال انتخاب کنید.");
        // Read source only with membership authorization. Admin/Owner have no bypass.
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var source = await db.ChatMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.MessageId && x.DeletedAt == null, ct)
            ?? throw new ChatException("پیام برای انتقال پیدا نشد.");
        await ChatSecurity.EnsureMemberAsync(db, source.RoomId, me, ct);
        var sourceAuthor = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == source.SenderUserId).Select(x => new { x.UserName, x.DisplayName }).SingleAsync(ct);
        var content = _protector.Unprotect(source.EncryptedContent);
        var destination = request.RoomId is > 0 ? request.RoomId.Value :
            (await rooms.GetOrCreateDirectAsync(request.Username!, actor, ct)).Id;
        await ChatSecurity.EnsureMemberAsync(db, destination, me, ct);
        var forward = new ChatMessage
        {
            RoomId = destination, SenderUserId = me, CreatedAt = DateTimeOffset.UtcNow,
            EncryptedContent = _protector.Protect(content),
            ForwardedFromDisplayName = sourceAuthor.DisplayName,
            ForwardedFromUsername = sourceAuthor.UserName
        };
        var forwardedFiles = await db.ChatMessageAttachments.AsNoTracking()
            .Where(x => x.MessageId == source.Id).OrderBy(x => x.Position)
            .Select(x => x.FileId).ToListAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.ChatMessages.Add(forward);
        await db.SaveChangesAsync(ct);
        foreach (var (fileId, position) in forwardedFiles.Select((id, i) => (id, i)))
            db.ChatMessageAttachments.Add(new ChatMessageAttachment { MessageId = forward.Id, FileId = fileId, Position = position });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var sender = await db.Users.AsNoTracking().Where(x => x.Id == me)
            .Select(x => new { x.UserName, x.DisplayName }).SingleAsync(ct);
        var dto = new ChatMessageDto(forward.Id, destination, sender.UserName ?? "", content, forward.CreatedAt, null, null,
            sender.DisplayName, ForwardedFromDisplayName: forward.ForwardedFromDisplayName,
            ForwardedFromUsername: forward.ForwardedFromUsername, AttachmentIds: forwardedFiles);
        await events.MessageCreatedAsync(dto, await ChatSecurity.MembersAsync(db, destination, ct), ct);
        return dto;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> HistoryAsync(long roomId, long? beforeId, int take, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        await ChatSecurity.EnsureMemberAsync(db, roomId, me, ct);
        take = Math.Clamp(take, 1, 100);
        var rows = await (from msg in db.ChatMessages.AsNoTracking()
                          join author in db.Users.IgnoreQueryFilters().AsNoTracking() on msg.SenderUserId equals author.Id
                          where msg.RoomId == roomId && msg.DeletedAt == null && (!beforeId.HasValue || msg.Id < beforeId.Value)
                          orderby msg.Id descending
                          select new { Message = msg, Sender = author.UserName, SenderDisplay = author.DisplayName })
            .Take(take).ToListAsync(ct);
        var replyIds = rows.Where(x => x.Message.ReplyToMessageId.HasValue)
            .Select(x => x.Message.ReplyToMessageId!.Value).Distinct().ToArray();
        ReplyReference[] replies = replyIds.Length == 0 ? [] : await (from reply in db.ChatMessages.AsNoTracking()
                         join author in db.Users.IgnoreQueryFilters().AsNoTracking() on reply.SenderUserId equals author.Id
                         where replyIds.Contains(reply.Id) && reply.RoomId == roomId && reply.DeletedAt == null
                         select new ReplyReference(reply.Id, author.UserName ?? "", author.DisplayName, reply.EncryptedContent))
            .ToArrayAsync(ct);
        var replyLookup = replies.ToDictionary(x => x.Id);
        var messageIds = rows.Select(x => x.Message.Id).ToArray();
        var links = await db.ChatMessageAttachments.AsNoTracking().Where(x => messageIds.Contains(x.MessageId))
            .OrderBy(x => x.Position).ToListAsync(ct);
        var grouped = links.GroupBy(x => x.MessageId).ToDictionary(x => x.Key,
            x => (IReadOnlyList<Guid>)x.Select(y => y.FileId).ToArray());
        return rows.AsEnumerable().Reverse().Select(x =>
        {
            replyLookup.TryGetValue(x.Message.ReplyToMessageId ?? -1, out var reply);
            return ToDto(x.Message, x.Sender ?? "", x.SenderDisplay, reply,
                grouped.GetValueOrDefault(x.Message.Id, Array.Empty<Guid>()));
        }).ToList();
    }

    public async Task<ChatMessageDto> EditAsync(long messageId, string content, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var text = ValidContent(content);
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var msg = await db.ChatMessages.FirstOrDefaultAsync(x => x.Id == messageId, ct)
                  ?? throw new ChatException("پیام پیدا نشد.");
        await ChatSecurity.EnsureMemberAsync(db, msg.RoomId, me, ct);
        if (msg.SenderUserId != me || msg.DeletedAt.HasValue)
            throw new ChatException("فقط فرستنده می‌تواند پیام موجود را ویرایش کند.");
        msg.EncryptedContent = _protector.Protect(text);
        msg.EditedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        var sender = await db.Users.AsNoTracking().Where(x => x.Id == me)
            .Select(x => new { x.UserName, x.DisplayName }).SingleAsync(ct);
        var existingFiles = await db.ChatMessageAttachments.AsNoTracking().Where(x => x.MessageId == msg.Id)
            .OrderBy(x => x.Position).Select(x => x.FileId).ToListAsync(ct);
        var dto = new ChatMessageDto(msg.Id, msg.RoomId, sender.UserName ?? "", text, msg.CreatedAt, msg.EditedAt, null,
            sender.DisplayName, msg.ReplyToMessageId,
            ForwardedFromDisplayName: msg.ForwardedFromDisplayName, ForwardedFromUsername: msg.ForwardedFromUsername,
            AttachmentIds: existingFiles);
        await events.MessageUpdatedAsync(dto, await ChatSecurity.MembersAsync(db, msg.RoomId, ct), ct);
        return dto;
    }

    public async Task DeleteAsync(long messageId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var msg = await db.ChatMessages.FirstOrDefaultAsync(x => x.Id == messageId, ct)
                  ?? throw new ChatException("پیام پیدا نشد.");
        await ChatSecurity.EnsureMemberAsync(db, msg.RoomId, me, ct);
        if (msg.SenderUserId != me) throw new ChatException("فقط فرستنده می‌تواند پیام را حذف کند.");
        if (msg.DeletedAt.HasValue) return;
        msg.DeletedAt = DateTimeOffset.UtcNow;
        msg.EncryptedContent = string.Empty; // Delete ciphertext too; keep tombstone for history.
        await db.SaveChangesAsync(ct);
        await events.MessageDeletedAsync(msg.RoomId, msg.Id, await ChatSecurity.MembersAsync(db, msg.RoomId, ct), ct);
    }

    public async Task MarkReadAsync(long roomId, long messageId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var member = await db.ChatRoomMembers.FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == me, ct)
                     ?? throw new ChatException("این گفتگو در دسترس شما نیست.");
        if (!await db.ChatMessages.AsNoTracking().AnyAsync(x => x.Id == messageId && x.RoomId == roomId, ct))
            throw new ChatException("پیام متعلق به این گفتگو نیست.");
        if (member.LastReadMessageId.HasValue && member.LastReadMessageId.Value >= messageId) return;
        member.LastReadMessageId = messageId;
        await db.SaveChangesAsync(ct);
        var username = await db.Users.AsNoTracking().Where(x => x.Id == me).Select(x => x.UserName).SingleAsync(ct);
        await events.ReadUpdatedAsync(roomId, username ?? "", messageId,
            await ChatSecurity.MembersAsync(db, roomId, ct), ct);
    }

    private static IReadOnlyList<Guid> ValidateAttachments(IReadOnlyList<Guid>? ids)
    {
        ids ??= [];
        if (ids.Count > 40 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new ChatException("فهرست پیوست‌های پیام معتبر نیست.");
        return ids;
    }

    private static string ValidContent(string? raw, bool allowEmpty = false)
    {
        var text = raw?.Trim();
        if ((!allowEmpty && string.IsNullOrWhiteSpace(text)) || text?.Length > 5000)
            throw new ChatException("متن پیام باید بین ۱ تا ۵۰۰۰ نویسه باشد.");
        return text ?? string.Empty;
    }

    private sealed record ReplyReference(long Id, string Username, string DisplayName, string EncryptedContent);

    private ChatMessageDto ToDto(ChatMessage msg, string username, string displayName, ReplyReference? reply,
        IReadOnlyList<Guid> files) =>
        new(msg.Id, msg.RoomId, username,
            msg.DeletedAt.HasValue ? null : _protector.Unprotect(msg.EncryptedContent),
            msg.CreatedAt, msg.EditedAt, msg.DeletedAt, displayName, msg.ReplyToMessageId,
            reply?.Username, reply?.DisplayName,
            reply is null ? null : _protector.Unprotect(reply.EncryptedContent),
            msg.ForwardedFromDisplayName, msg.ForwardedFromUsername, files);
}
