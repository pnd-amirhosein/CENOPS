namespace NTSW_CENOPS.Application.Stories;

/// <summary>Read-only release stories plus per-user read receipts.</summary>
public interface IReleaseStoryService
{
    Task<IReadOnlyList<ReleaseStorySummary>> ListAsync(int userId, CancellationToken cancellationToken = default);
    Task<ReleaseStoryDetail?> OpenAsync(int storyId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReleaseStoryViewerInfo>> ViewersAsync(int storyId, CancellationToken cancellationToken = default);
}

public sealed record ReleaseStorySummary(int Id, string Title, string Version, DateTimeOffset CreatedAt,
    bool Viewed, int ViewCount);
public sealed record ReleaseStoryDetail(int Id, string Title, string Version, string Markdown, DateTimeOffset CreatedAt);
public sealed record ReleaseStoryViewerInfo(int UserId, string DisplayName, string Username, DateTimeOffset ViewedAt);
