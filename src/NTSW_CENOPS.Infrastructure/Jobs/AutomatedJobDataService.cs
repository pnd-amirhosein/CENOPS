using System.Text.Json;
using System.Text.Json.Serialization;
using NTSW_CENOPS.Application.Jobs;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Jobs;

/// <summary>
/// Persists daily JSON arrays. Does not run SQL, schedule jobs, or update run history.
/// Concurrency is handled with rowversion and a unique job/day index.
/// </summary>
public sealed class AutomatedJobDataService(IDbContextFactory<AppDbContext> factory)
    : IAutomatedJobDataService
{
    private const int MaxAttempts = 5;
    // A defensive size ceiling: daily documents are intended to remain small.
    private const int MaxDailyJsonChars = 4_000_000;
    private const int MaxRowsJsonChars = 1_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class WindowEntry
    {
        public WindowEntry() { }

        [JsonPropertyName("windowStartUtc")]
        public DateTimeOffset WindowStartUtc { get; set; }

        [JsonPropertyName("windowEndUtc")]
        public DateTimeOffset WindowEndUtc { get; set; }

        [JsonPropertyName("collectedAtUtc")]
        public DateTimeOffset CollectedAtUtc { get; set; }

        [JsonPropertyName("rows")]
        public JsonElement Rows { get; set; }
    }

    public async Task<AutomatedJobDataModel?> GetDayAsync(
        long jobId, DateOnly date, CancellationToken ct = default)
    {
        EnsureJobId(jobId);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomatedJobData.AsNoTracking()
            .Where(x => x.AutomatedJobId == jobId && x.Date == date)
            .Select(x => new AutomatedJobDataModel(
                x.Id, x.AutomatedJobId, x.Date, x.Value, x.LastCalledAtUtc))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AutomatedJobDataDayModel>> ListDaysAsync(
        long jobId, DateOnly? from = null, DateOnly? through = null,
        int take = 90, CancellationToken ct = default)
    {
        EnsureJobId(jobId);
        if (take is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(take), "Use 1–365 days per request.");
        if (from.HasValue && through.HasValue && from.Value > through.Value)
            throw new ArgumentException("The start date must not be after the end date.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.AutomatedJobData.AsNoTracking().Where(x => x.AutomatedJobId == jobId);
        if (from.HasValue) query = query.Where(x => x.Date >= from.Value);
        if (through.HasValue) query = query.Where(x => x.Date <= through.Value);
        return await query.OrderByDescending(x => x.Date).Take(take)
            .Select(x => new AutomatedJobDataDayModel(
                x.Id, x.AutomatedJobId, x.Date, x.LastCalledAtUtc))
            .ToListAsync(ct);
    }

    public async Task<AutomatedJobDataModel> UpsertWindowAsync(
        long jobId, DateOnly date, DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc, string rowsJson,
        CancellationToken ct = default)
    {
        EnsureJobId(jobId);
        var start = windowStartUtc.ToUniversalTime();
        var end = windowEndUtc.ToUniversalTime();
        if (end <= start) throw new ArgumentException("WindowEndUtc must be after WindowStartUtc.");
        if (string.IsNullOrWhiteSpace(rowsJson))
            throw new ArgumentException("The returned rows must be a JSON array.", nameof(rowsJson));
        if (rowsJson.Length > MaxRowsJsonChars)
            throw new ArgumentException("The JSON result is too large for a daily document.", nameof(rowsJson));

        // Validate once, before entering the database retry loop.
        JsonElement rows;
        using (var document = JsonDocument.Parse(rowsJson))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("The returned rows must be a JSON array.", nameof(rowsJson));
            rows = document.RootElement.Clone();
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await using var db = await factory.CreateDbContextAsync(ct);
            var entity = await db.AutomatedJobData
                .SingleOrDefaultAsync(x => x.AutomatedJobId == jobId && x.Date == date, ct);

            if (entity is null)
            {
                // Do not create orphaned documents for unknown/soft-deleted jobs.
                if (!await db.AutomatedJobs.AnyAsync(x => x.Id == jobId, ct))
                    throw new KeyNotFoundException($"Automated job {jobId} does not exist.");
                entity = new AutomatedJobData
                {
                    AutomatedJobId = jobId,
                    Date = date
                };
                db.AutomatedJobData.Add(entity);
            }

            List<WindowEntry> windows;
            try
            {
                windows = JsonSerializer.Deserialize<List<WindowEntry>>(entity.Value, JsonOptions)
                    ?? throw new InvalidDataException("Daily job data must be a JSON array.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Existing daily job data contains invalid JSON.", ex);
            }
            if (windows.Any(x => x is null || x.Rows.ValueKind != JsonValueKind.Array))
                throw new InvalidDataException("Existing daily job data must contain valid window entries with row arrays.");

            var now = DateTimeOffset.UtcNow;
            // Idempotency: replace the exact same window, never append a duplicate.
            windows.RemoveAll(x => x.WindowStartUtc == start && x.WindowEndUtc == end);
            windows.Add(new WindowEntry
            {
                WindowStartUtc = start,
                WindowEndUtc = end,
                CollectedAtUtc = now,
                Rows = rows
            });
            windows.Sort((a, b) => a.WindowStartUtc.CompareTo(b.WindowStartUtc));
            var value = JsonSerializer.Serialize(windows, JsonOptions);
            if (value.Length > MaxDailyJsonChars)
                throw new InvalidOperationException("Daily job JSON exceeds the configured size limit.");

            entity.Value = value;
            entity.LastCalledAtUtc = now;

            try
            {
                await db.SaveChangesAsync(ct);
                return new AutomatedJobDataModel(
                    entity.Id, entity.AutomatedJobId, entity.Date,
                    entity.Value, entity.LastCalledAtUtc);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Another writer updated the same day's row: reload and merge.
            }
            catch (DbUpdateException ex) when (attempt < MaxAttempts && IsUniqueConstraint(ex))
            {
                // Another writer created the same day's row: reload and merge.
            }

            if (attempt < MaxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(attempt * 30), ct);
        }

        throw new InvalidOperationException("Could not save daily job data after concurrent updates. Retry the operation.");
    }

    public async Task<bool> DeleteDayAsync(long jobId, DateOnly date, CancellationToken ct = default)
    {
        EnsureJobId(jobId);
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.AutomatedJobData
            .SingleOrDefaultAsync(x => x.AutomatedJobId == jobId && x.Date == date, ct);
        if (entity is null) return false;
        db.AutomatedJobData.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void EnsureJobId(long jobId)
    {
        if (jobId <= 0) throw new ArgumentOutOfRangeException(nameof(jobId));
    }

    private static bool IsUniqueConstraint(DbUpdateException ex) =>
        ex.InnerException is SqlException sql &&
        sql.Number is 2601 or 2627;
}
