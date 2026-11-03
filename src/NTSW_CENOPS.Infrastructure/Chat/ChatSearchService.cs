using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Chat;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Chat;

/// <summary>
/// Encrypted messages cannot be filtered in SQL. Search decrypts a bounded recent slice
/// from the caller's OWN rooms only. Add a separate permission-aware search index later
/// if the application needs full-history searching.
/// </summary>
public sealed class ChatSearchService(
    IDbContextFactory<AppDbContext> factory, IAccessService access,
    IChatRoomService rooms, IDataProtectionProvider protection) : IChatSearchService
{
    private readonly IDataProtector _protector = protection.CreateProtector("NTSW.CENOPS.ChatMessages.v1");

    // Lightweight identity-only lookup for the recipient autocomplete.
    // Never touches (or decrypts) message history.
    public async Task<IReadOnlyList<ChatParticipantDto>> SearchUsersAsync(
        string query, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var term = query?.Trim().TrimStart('@') ?? "";
        if (term.Length == 0) return [];
        if (term.Length > 100) throw new ChatException("عبارت جستجو باید حداکثر ۱۰۰ نویسه باشد.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var viewerId = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        return await db.Users.AsNoTracking()
            .Where(u => u.Id != viewerId &&
                ((u.UserName != null && u.UserName.Contains(term)) || u.DisplayName.Contains(term)))
            .OrderBy(u => u.DisplayName)
            .Take(10)
            .Select(u => new ChatParticipantDto(u.UserName ?? "", u.DisplayName))
            .ToListAsync(ct);
    }

    public async Task<ChatSearchResult> SearchAsync(string query, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var term = query?.Trim() ?? "";
        if (term.Length < 2) return new ChatSearchResult([], [], []);
        if (term.Length > 100) throw new ChatException("عبارت جستجو باید حداکثر ۱۰۰ نویسه باشد.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var viewerId = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        // Search only rooms to which this user currently belongs. No admin override.
        var myRoomIds = await db.ChatRoomMembers.AsNoTracking()
            .Where(m => m.UserId == viewerId).Select(m => m.RoomId).ToListAsync(ct);
        var myRooms = await rooms.MyRoomsAsync(actor, ct);
        var matchingRooms = myRooms.Where(r =>
            (r.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            r.Members.Any(m => m.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                m.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Take(12).ToArray();
        var matchingUsers = await db.Users.AsNoTracking()
            .Where(u => u.Id != viewerId &&
                ((u.UserName != null && u.UserName.Contains(term)) || u.DisplayName.Contains(term)))
            .OrderBy(u => u.DisplayName)
            .Take(12)
            .Select(u => new ChatParticipantDto(u.UserName ?? "", u.DisplayName))
            .ToListAsync(ct);

        if (myRoomIds.Count == 0) return new ChatSearchResult(matchingRooms, matchingUsers, []);
        var candidates = await (from message in db.ChatMessages.AsNoTracking()
                                join sender in db.Users.IgnoreQueryFilters().AsNoTracking()
                                    on message.SenderUserId equals sender.Id
                                where myRoomIds.Contains(message.RoomId) && message.DeletedAt == null
                                orderby message.Id descending
                                select new { message.Id, message.RoomId, message.EncryptedContent,
                                    message.CreatedAt, message.EditedAt, message.DeletedAt, Sender = sender.UserName, SenderDisplay = sender.DisplayName })
            .Take(1200).ToListAsync(ct);
        var found = new List<ChatMessageSearchHit>();
        foreach (var item in candidates)
        {
            ct.ThrowIfCancellationRequested();
            // Do not send encrypted text to a client or leak inaccessible conversations.
            var text = _protector.Unprotect(item.EncryptedContent);
            if (!text.Contains(term, StringComparison.OrdinalIgnoreCase)) continue;
            var room = myRooms.FirstOrDefault(x => x.Id == item.RoomId);
            var label = room?.Kind == NTSW_CENOPS.Domain.Chat.ChatRoomKind.Direct
                ? room.Members.FirstOrDefault(x => x.Username != actor.Identity?.Name)?.DisplayName ?? "گفتگوی مستقیم"
                : room?.Title ?? "گفتگو";
            found.Add(new ChatMessageSearchHit(new ChatMessageDto(item.Id, item.RoomId,
                item.Sender ?? "", text, item.CreatedAt, item.EditedAt, item.DeletedAt, item.SenderDisplay), label));
            if (found.Count >= 20) break;
        }
        return new ChatSearchResult(matchingRooms, matchingUsers, found);
    }
}
