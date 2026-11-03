using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Chat;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Identity;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Chat;

internal static class ChatSecurity
{
    public static int ActorId(ClaimsPrincipal actor) =>
        actor.Identity?.IsAuthenticated == true &&
        int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    public static async Task<int> AuthenticateAsync(AppDbContext db, IAccessService access,
        ClaimsPrincipal actor, CancellationToken ct)
    {
        var id = ActorId(actor);
        if (id == 0 || !await db.Users.AsNoTracking().AnyAsync(x => x.Id == id, ct) ||
            !await access.HasAccessAsync(actor, AccessKeys.Chat, ct))
            throw new ChatException("شما مجاز به استفاده از گفتگو نیستید.");
        return id;
    }

    public static async Task EnsureMemberAsync(AppDbContext db, long roomId, int actorId, CancellationToken ct)
    {
        if (roomId <= 0 || !await db.ChatRoomMembers.AsNoTracking()
                .AnyAsync(x => x.RoomId == roomId && x.UserId == actorId, ct))
            throw new ChatException("این گفتگو در دسترس شما نیست.");
    }

    public static async Task<ApplicationUser> ResolveUserAsync(AppDbContext db, string? username, CancellationToken ct)
    {
        username = username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 256)
            throw new ChatException("نام کاربری معتبر نیست.");
        var normalized = username.ToUpperInvariant();
        return await db.Users.FirstOrDefaultAsync(x => x.NormalizedUserName == normalized, ct)
               ?? throw new ChatException("کاربر مورد نظر پیدا نشد.");
    }

    public static async Task<List<int>> MembersAsync(AppDbContext db, long roomId, CancellationToken ct) =>
        await db.ChatRoomMembers.AsNoTracking().Where(x => x.RoomId == roomId)
            .Select(x => x.UserId).ToListAsync(ct);

    public static async Task<ChatRoomDto> RoomDtoAsync(AppDbContext db, long roomId, int viewerId, CancellationToken ct)
    {
        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roomId, ct)
            ?? throw new ChatException("گفتگو پیدا نشد.");
        await EnsureMemberAsync(db, roomId, viewerId, ct);
        var author = await db.Users.AsNoTracking().Where(x => x.Id == room.CreatedByUserId)
            .Select(x => x.UserName).FirstOrDefaultAsync(ct) ?? "";
        var members = await (from m in db.ChatRoomMembers.AsNoTracking()
                             join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                             where m.RoomId == roomId
                             orderby u.DisplayName
                             select new ChatParticipantDto(u.UserName ?? "", u.DisplayName)).ToListAsync(ct);
        var lastRead = await db.ChatRoomMembers.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.UserId == viewerId)
            .Select(x => x.LastReadMessageId).FirstOrDefaultAsync(ct);
        return new ChatRoomDto(room.Id, room.Title, room.Kind, author, room.CreatedAt, members, lastRead);
    }
}
