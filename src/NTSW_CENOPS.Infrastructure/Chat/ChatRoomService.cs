using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Chat;
using NTSW_CENOPS.Domain.Chat;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Chat;

public sealed class ChatRoomService(
    IDbContextFactory<AppDbContext> factory, IAccessService access,
    IChatEventPublisher events) : IChatRoomService
{
    public async Task<ChatRoomDto> CreateAsync(CreateChatRoomRequest request, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        if (request.Usernames is null || request.Usernames.Count > 99)
            throw new ChatException("حداکثر ۱۰۰ عضو برای ایجاد گفتگو مجاز است.");
        var ids = new HashSet<int> { me };
        foreach (var username in request.Usernames)
            ids.Add((await ChatSecurity.ResolveUserAsync(db, username, ct)).Id);
        if (ids.Count < 2) throw new ChatException("برای ایجاد گفتگو دست‌کم یک مخاطب دیگر انتخاب کنید.");
        if (ids.Count == 2)
        {
            var otherId = ids.Single(x => x != me);
            return await GetOrCreateDirectCoreAsync(me, otherId, ct);
        }
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
            throw new ChatException("عنوان گروه باید بین ۱ تا ۱۶۰ نویسه باشد.");
        var room = new ChatRoom
        {
            Title = title, Kind = ChatRoomKind.Group, CreatedByUserId = me,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            db.ChatRooms.Add(room);
            await db.SaveChangesAsync(ct);
            db.ChatRoomMembers.AddRange(ids.Select(id => new ChatRoomMember { RoomId = room.Id, UserId = id }));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        await events.RoomUpdatedAsync(room.Id, ids.ToArray(), ct);
        return await ChatSecurity.RoomDtoAsync(db, room.Id, me, ct);
    }

    public async Task<ChatRoomDto> GetOrCreateDirectAsync(string username, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var other = await ChatSecurity.ResolveUserAsync(db, username, ct);
        if (other.Id == me) throw new ChatException("امکان گفتگوی مستقیم با خودتان وجود ندارد.");
        return await GetOrCreateDirectCoreAsync(me, other.Id, ct);
    }

    private async Task<ChatRoomDto> GetOrCreateDirectCoreAsync(int me, int otherId, CancellationToken ct)
    {
        var low = Math.Min(me, otherId);
        var high = Math.Max(me, otherId);
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.ChatRooms.AsNoTracking()
            .Where(x => x.Kind == ChatRoomKind.Direct && x.DirectLowUserId == low && x.DirectHighUserId == high)
            .Select(x => x.Id).FirstOrDefaultAsync(ct);
        if (existing > 0) return await ChatSecurity.RoomDtoAsync(db, existing, me, ct);
        var room = new ChatRoom
        {
            Kind = ChatRoomKind.Direct, CreatedByUserId = me, CreatedAt = DateTimeOffset.UtcNow,
            DirectLowUserId = low, DirectHighUserId = high
        };
        // Atomic insert; unique filtered database index enforces exactly one DM for this pair.
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.ChatRooms.Add(room);
            await db.SaveChangesAsync(ct);
            db.ChatRoomMembers.AddRange(
                new ChatRoomMember { RoomId = room.Id, UserId = low },
                new ChatRoomMember { RoomId = room.Id, UserId = high });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted the same pair. Check in a fresh context.
            await using var retry = await factory.CreateDbContextAsync(ct);
            var winner = await retry.ChatRooms.AsNoTracking()
                .Where(x => x.Kind == ChatRoomKind.Direct && x.DirectLowUserId == low && x.DirectHighUserId == high)
                .Select(x => x.Id).FirstOrDefaultAsync(ct);
            if (winner == 0) throw;
            return await ChatSecurity.RoomDtoAsync(retry, winner, me, ct);
        }
        await events.RoomUpdatedAsync(room.Id, [low, high], ct);
        return await ChatSecurity.RoomDtoAsync(db, room.Id, me, ct);
    }

    public async Task<ChatRoomDto> GetAsync(long roomId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        return await ChatSecurity.RoomDtoAsync(db, roomId, me, ct);
    }

    public async Task<bool> IsMemberAsync(long roomId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        return roomId > 0 && await db.ChatRoomMembers.AsNoTracking()
            .AnyAsync(x => x.RoomId == roomId && x.UserId == me, ct);
    }

    public async Task<IReadOnlyList<ChatRoomSummaryDto>> MyRoomsAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var membershipRows = await (from membership in db.ChatRoomMembers.AsNoTracking()
                                    join room in db.ChatRooms.AsNoTracking() on membership.RoomId equals room.Id
                                    where membership.UserId == me
                                    orderby room.Id descending
                                    select new { room.Id, room.Title, room.Kind, room.CreatedAt, membership.LastReadMessageId })
            .Take(100).ToListAsync(ct);
        var result = new List<ChatRoomSummaryDto>(membershipRows.Count);
        foreach (var room in membershipRows)
        {
            var participants = await (from m in db.ChatRoomMembers.AsNoTracking()
                                      join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                                      where m.RoomId == room.Id
                                      select new ChatParticipantDto(u.UserName ?? "", u.DisplayName)).ToListAsync(ct);
            var latest = await db.ChatMessages.AsNoTracking().Where(x => x.RoomId == room.Id)
                .OrderByDescending(x => x.Id).Select(x => new { x.Id, x.CreatedAt })
                .FirstOrDefaultAsync(ct);
            var unread = await db.ChatMessages.AsNoTracking().CountAsync(x => x.RoomId == room.Id &&
                x.SenderUserId != me && x.DeletedAt == null &&
                (!room.LastReadMessageId.HasValue || x.Id > room.LastReadMessageId.Value), ct);
            result.Add(new ChatRoomSummaryDto(room.Id, room.Title, room.Kind, room.CreatedAt,
                participants, latest?.Id, latest?.CreatedAt, unread));
        }
        return result;
    }

    public async Task<ChatRoomDto> RenameGroupAsync(long roomId, string title, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var room = await EditableGroupAsync(db, roomId, me, ct);
        title = title?.Trim() ?? string.Empty;
        if (title.Length is < 1 or > 160) throw new ChatException("عنوان گروه باید بین ۱ تا ۱۶۰ نویسه باشد.");
        room.Title = title;
        await db.SaveChangesAsync(ct);
        await events.RoomUpdatedAsync(roomId, await ChatSecurity.MembersAsync(db, roomId, ct), ct);
        return await ChatSecurity.RoomDtoAsync(db, roomId, me, ct);
    }

    public async Task<ChatRoomDto> AddMembersAsync(long roomId, IReadOnlyList<string> usernames, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var room = await EditableGroupAsync(db, roomId, me, ct);
        if (usernames is null || usernames.Count == 0 || usernames.Count > 100)
            throw new ChatException("فهرست اعضا معتبر نیست.");
        var current = await ChatSecurity.MembersAsync(db, roomId, ct);
        var desired = new HashSet<int>();
        foreach (var username in usernames)
            desired.Add((await ChatSecurity.ResolveUserAsync(db, username, ct)).Id);
        if (current.Union(desired).Distinct().Count() > 100)
            throw new ChatException("ظرفیت گفتگو حداکثر ۱۰۰ نفر است.");
        db.ChatRoomMembers.AddRange(desired.Except(current).Select(id =>
            new ChatRoomMember { RoomId = room.Id, UserId = id }));
        await db.SaveChangesAsync(ct);
        await events.RoomUpdatedAsync(roomId, current.Union(desired).ToArray(), ct);
        return await ChatSecurity.RoomDtoAsync(db, roomId, me, ct);
    }

    public async Task<ChatRoomDto> RemoveMemberAsync(long roomId, string username, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var room = await EditableGroupAsync(db, roomId, me, ct);
        var user = await ChatSecurity.ResolveUserAsync(db, username, ct);
        if (user.Id == room.CreatedByUserId) throw new ChatException("سازنده گروه قابل حذف نیست.");
        var membership = await db.ChatRoomMembers.FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == user.Id, ct);
        if (membership is null) throw new ChatException("کاربر عضو این گروه نیست.");
        var original = await ChatSecurity.MembersAsync(db, roomId, ct);
        db.ChatRoomMembers.Remove(membership);
        await db.SaveChangesAsync(ct);
        await events.RoomUpdatedAsync(roomId, original, ct);
        return await ChatSecurity.RoomDtoAsync(db, roomId, me, ct);
    }

    public async Task LeaveAsync(long roomId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var me = await ChatSecurity.AuthenticateAsync(db, access, actor, ct);
        var room = await db.ChatRooms.FirstOrDefaultAsync(x => x.Id == roomId, ct)
                   ?? throw new ChatException("گفتگو پیدا نشد.");
        if (room.Kind != ChatRoomKind.Group || room.CreatedByUserId == me)
            throw new ChatException("سازنده گروه نمی‌تواند بدون انتقال مدیریت خارج شود؛ گفتگوی مستقیم نیز قابل ترک نیست.");
        var member = await db.ChatRoomMembers.FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == me, ct)
                     ?? throw new ChatException("عضو گفتگو نیستید.");
        var members = await ChatSecurity.MembersAsync(db, roomId, ct);
        db.ChatRoomMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        await events.RoomUpdatedAsync(roomId, members, ct);
    }

    private static async Task<ChatRoom> EditableGroupAsync(AppDbContext db, long roomId, int userId, CancellationToken ct)
    {
        await ChatSecurity.EnsureMemberAsync(db, roomId, userId, ct);
        var room = await db.ChatRooms.FirstOrDefaultAsync(x => x.Id == roomId, ct)
                   ?? throw new ChatException("گفتگو پیدا نشد.");
        if (room.Kind != ChatRoomKind.Group || room.CreatedByUserId != userId)
            throw new ChatException("فقط سازنده گروه می‌تواند اعضا را مدیریت کند.");
        return room;
    }
}
