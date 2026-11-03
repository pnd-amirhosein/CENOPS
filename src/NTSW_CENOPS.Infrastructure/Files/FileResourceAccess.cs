using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Files;
using NTSW_CENOPS.Application.Notes;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;

namespace NTSW_CENOPS.Infrastructure.Files;

/// <summary>One central parent-aware guard for all HTTP file delivery and preview requests.</summary>
public sealed class FileResourceAccess(IDbContextFactory<AppDbContext> factory, IAccessService access,
    IAppFileGateway files, INoteService notes) : IFileResourceAccess
{
    public async Task<bool> CanDeleteUnattachedAsync(Guid fileId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (fileId == Guid.Empty || actor.Identity?.IsAuthenticated != true ||
            !int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId) || actorId <= 0)
            return false;
        var file = await files.FindAsync(fileId, ct);
        if (file?.OwnerKey != actorId.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.ChatMessageAttachments.AsNoTracking().AnyAsync(x => x.FileId == fileId, ct)) return false;
        var key = fileId.ToString("D");
        var blocks = await db.NoteBlocks.AsNoTracking()
            .Where(x => x.Kind == NTSW_CENOPS.Application.Notes.NoteBlockKind.File && x.Content.Contains(key))
            .Select(x => x.Content).ToListAsync(ct);
        return !blocks.Any(x => FileReferenceCodec.Parse(x).Contains(fileId));
    }

    public async Task<bool> CanReadAsync(Guid fileId, long? messageId, int? noteId,
        ClaimsPrincipal actor, CancellationToken ct = default)
    {
        // An ambiguous/missing parent never grants file access; knowing a UUID is not permission.
        if (fileId == Guid.Empty || (messageId.HasValue == noteId.HasValue) || actor.Identity?.IsAuthenticated != true ||
            !int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
            return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct)) return false;

        if (messageId is > 0)
        {
            if (!await access.HasAccessAsync(actor, AccessKeys.Chat, ct)) return false;
            return await (from link in db.ChatMessageAttachments.AsNoTracking()
                          join message in db.ChatMessages.AsNoTracking() on link.MessageId equals message.Id
                          join member in db.ChatRoomMembers.AsNoTracking() on message.RoomId equals member.RoomId
                          where link.MessageId == messageId.Value && link.FileId == fileId &&
                                message.DeletedAt == null && member.UserId == userId
                          select link.FileId).AnyAsync(ct);
        }
        if (noteId is not > 0) return false;
        // Defer visibility/manager/access rules to the existing Note service.
        // This avoids a second, potentially divergent implementation of private-note permissions.
        var note = await notes.FindAsync(noteId.Value, actor, ct);
        return note is not null && note.Blocks
            .Where(block => block.Kind == NoteBlockKind.File)
            .Any(block => FileReferenceCodec.Parse(block.Content).Contains(fileId));
    }
}
