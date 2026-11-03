using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NTSW_CENOPS.Application.Stories;
using NTSW_CENOPS.Infrastructure.Persistence;

namespace NTSW_CENOPS.Infrastructure.Stories;

/// <summary>
/// Imports each GLASSWORK Markdown file exactly once. The database snapshot survives
/// subsequent deployments or deletion of the original file. Does not mutate old stories.
/// </summary>
public sealed partial class ReleaseStoryService(
    IDbContextFactory<AppDbContext> contexts, IHostEnvironment environment,
    IConfiguration configuration, ILogger<ReleaseStoryService> logger) : IReleaseStoryService
{
    // One service per request/circuit, and a unique database index also enforces
    // idempotency across web-server instances.
    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    public async Task<IReadOnlyList<ReleaseStorySummary>> ListAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) return [];
        await ImportNewAsync(cancellationToken);
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var stories = await db.ReleaseStories.AsNoTracking()
            .Select(story => new ReleaseStorySummary(story.Id, story.Title, story.Version,
                story.SourceCreatedAt,
                db.ReleaseStoryViews.Any(v => v.StoryId == story.Id && v.UserId == userId),
                db.ReleaseStoryViews.Count(v => v.StoryId == story.Id)))
            .ToListAsync(cancellationToken);
        return stories.OrderBy(s => ParseVersion(s.Version)).ThenBy(s => s.CreatedAt)
            .ThenBy(s => s.Id).ToArray();
    }

    public async Task<ReleaseStoryDetail?> OpenAsync(int storyId, int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || storyId <= 0) return null;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var story = await db.ReleaseStories.AsNoTracking().Where(s => s.Id == storyId)
            .Select(s => new ReleaseStoryDetail(s.Id, s.Title, s.Version, s.Markdown, s.SourceCreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        if (story is null) return null;

        // Unique PK guarantees a user only counts once even when they reopen a story.
        // Serializable insert is unnecessary; concurrent clicks are tolerated by checking
        // the unique constraint after a race.
        if (!await db.ReleaseStoryViews.AnyAsync(v => v.StoryId == storyId && v.UserId == userId, cancellationToken))
        {
            db.ReleaseStoryViews.Add(new ReleaseStoryView
            {
                StoryId = storyId, UserId = userId, ViewedAt = DateTimeOffset.UtcNow
            });
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                await using var verify = await contexts.CreateDbContextAsync(cancellationToken);
                if (!await verify.ReleaseStoryViews.AnyAsync(v => v.StoryId == storyId && v.UserId == userId, cancellationToken))
                    throw;
            }
        }
        return story;
    }

    public async Task<IReadOnlyList<ReleaseStoryViewerInfo>> ViewersAsync(int storyId, CancellationToken cancellationToken = default)
    {
        if (storyId <= 0) return [];
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await (from view in db.ReleaseStoryViews.AsNoTracking()
                join user in db.Users.AsNoTracking() on view.UserId equals user.Id
                where view.StoryId == storyId && !user.IsDeleted
                orderby view.ViewedAt descending
                select new ReleaseStoryViewerInfo(user.Id, user.DisplayName, user.UserName ?? "", view.ViewedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task ImportNewAsync(CancellationToken cancellationToken)
    {
        var directory = configuration["ReleaseStories:DocumentsDirectory"];
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(environment.ContentRootPath, "story-docs");
            if (!Directory.Exists(directory)) directory = Path.Combine(AppContext.BaseDirectory, "story-docs");
        }
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("Release story documents directory does not exist: {Directory}", directory);
            return;
        }
        await ImportLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            var existing = (await db.ReleaseStories.AsNoTracking()
                .Select(s => s.SourceFile).ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateFiles(directory, "GLASSWORK*.md", SearchOption.TopDirectoryOnly)
                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var filename = Path.GetFileName(path);
                if (existing.Contains(filename)) continue;
                var info = new FileInfo(path);
                // Bound accidental huge files; docs are authored in source control, not uploaded.
                if (info.Length > 1024 * 1024) { logger.LogWarning("Story doc is too large: {File}", filename); continue; }
                var markdown = await File.ReadAllTextAsync(path, cancellationToken);
                var version = ExtractVersion(filename);
                var heading = markdown.Split('\n').Select(s => s.Trim()).FirstOrDefault(s => s.StartsWith("# "));
                var title = heading is null ? $"GLASSWORK {version}" : heading[2..].Trim();
                if (title.Length > 300) title = title[..300];
                db.ReleaseStories.Add(new ReleaseStory
                {
                    SourceFile = filename,
                    Title = title,
                    Version = version,
                    Markdown = markdown,
                    SourceCreatedAt = new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero),
                    ImportedAt = DateTimeOffset.UtcNow
                });
                existing.Add(filename);
            }
            if (db.ChangeTracker.HasChanges())
            {
                try { await db.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateException)
                {
                    // In a multi-node deployment another node may have imported the same
                    // filename. Read back known names before deciding to fail.
                    await using var verify = await contexts.CreateDbContextAsync(cancellationToken);
                    var imported = (await verify.ReleaseStories.AsNoTracking().Select(s => s.SourceFile)
                        .ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (!existing.IsSubsetOf(imported)) throw;
                }
            }
        }
        finally { ImportLock.Release(); }
    }

    private static string ExtractVersion(string filename)
    {
        var m = VersionPattern().Match(filename);
        return m.Success ? m.Groups[1].Value.Replace('_', '.') : "0";
    }

    private static Version ParseVersion(string version) =>
        Version.TryParse(version, out var parsed) ? parsed : new Version(0, 0);

    [GeneratedRegex(@"GLASSWORK_(\d+(?:_\d+)*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
