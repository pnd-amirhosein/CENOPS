using NTSW_CENOPS.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace NTSW_CENOPS.Chat;

/// <summary>Authenticated SignalR transport; all business and privacy checks live in application services.</summary>
[Authorize]
public sealed class ChatHub(IChatRoomService rooms, IChatMessageService messages,
    IDbContextFactory<AppDbContext> database) : Hub
{
    private System.Security.Claims.ClaimsPrincipal Actor => Context.User ?? throw new HubException("ورود به سیستم الزامی است.");
    private static string Channel(long id) => $"chat:room:{id}";

    public Task<IReadOnlyList<ChatRoomSummaryDto>> MyRooms() => Run(() => rooms.MyRoomsAsync(Actor));
    public Task<ChatRoomDto> GetRoom(long roomId) => Run(() => rooms.GetAsync(roomId, Actor));
    public Task<ChatRoomDto> CreateRoom(CreateChatRoomRequest request) => Run(() => rooms.CreateAsync(request, Actor));
    public Task<ChatRoomDto> GetOrCreateDirect(string username) => Run(() => rooms.GetOrCreateDirectAsync(username, Actor));
    public Task<ChatRoomDto> RenameGroup(long roomId, string title) => Run(() => rooms.RenameGroupAsync(roomId, title, Actor));
    public Task<ChatRoomDto> AddMembers(long roomId, IReadOnlyList<string> usernames) => Run(() => rooms.AddMembersAsync(roomId, usernames, Actor));
    public Task<ChatRoomDto> RemoveMember(long roomId, string username) => Run(() => rooms.RemoveMemberAsync(roomId, username, Actor));

    public async Task LeaveRoom(long roomId)
    {
        await Run(async () => { await rooms.LeaveAsync(roomId, Actor); return true; });
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Channel(roomId));
    }

    // Group membership authorizes live room subscription; user-targeted notifications
    // are delivered even when the browser hasn't joined a particular group.
    public async Task SubscribeRoom(long roomId)
    {
        if (!await Run(() => rooms.IsMemberAsync(roomId, Actor)))
            throw new HubException("این گفتگو در دسترس شما نیست.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Channel(roomId));
    }

    public Task UnsubscribeRoom(long roomId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Channel(roomId));

    /// <summary>Ephemeral typing hints for room members only, rate limited per connection.</summary>
    public async Task SetTyping(long roomId, bool isTyping)
    {
        if (roomId <= 0 || !await Run(() => rooms.IsMemberAsync(roomId, Actor)))
            throw new HubException("این گفتگو در دسترس شما نیست.");
        if (isTyping && Context.Items.TryGetValue("lastTyping", out var last) &&
            last is DateTimeOffset sent && DateTimeOffset.UtcNow - sent < TimeSpan.FromMilliseconds(900)) return;
        if (isTyping) Context.Items["lastTyping"] = DateTimeOffset.UtcNow;
        var userId = Actor.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out var id))
            throw new HubException("ورود به سیستم الزامی است.");
        await using var db = await database.CreateDbContextAsync();
        var memberIds = await db.ChatRoomMembers.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.UserId != id)
            .Select(x => x.UserId).ToArrayAsync();
        var ids = memberIds.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var displayName = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.DisplayName).FirstOrDefaultAsync() ?? Actor.Identity?.Name ?? "کاربر";
        await Clients.Users(ids).SendAsync("ChatTyping", roomId, Actor.Identity?.Name ?? "", displayName, isTyping);
    }


    public Task<ChatMessageDto> SendMessage(SendChatMessageRequest request) => Run(() => messages.SendAsync(request, Actor));
    public Task<ChatMessageDto> ForwardMessage(ForwardChatMessageRequest request) => Run(() => messages.ForwardAsync(request, Actor));
    public Task<IReadOnlyList<ChatMessageDto>> GetMessages(long roomId, long? beforeId = null, int take = 50) =>
        Run(() => messages.HistoryAsync(roomId, beforeId, take, Actor));
    public Task<ChatMessageDto> EditMessage(long messageId, string content) => Run(() => messages.EditAsync(messageId, content, Actor));
    public Task DeleteMessage(long messageId) => Run(async () =>
    {
        await messages.DeleteAsync(messageId, Actor);
        return true;
    });
    public Task MarkRead(long roomId, long messageId) => Run(async () =>
    {
        await messages.MarkReadAsync(roomId, messageId, Actor);
        return true;
    });
    private static async Task<T> Run<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (ChatException ex) { throw new HubException(ex.Message); }
    }
}
