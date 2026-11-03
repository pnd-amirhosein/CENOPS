using System.Security.Claims;
using NTSW_CENOPS.Application.Knowledge;

namespace NTSW_CENOPS.Application.Announcements;

public sealed class AnnouncementModel
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public interface IAnnouncementService
{
    Task<IReadOnlyList<AnnouncementModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<AnnouncementModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeAuthorModel>> AuthorsAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanChooseAuthorAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> SaveAsync(AnnouncementModel item, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
}
