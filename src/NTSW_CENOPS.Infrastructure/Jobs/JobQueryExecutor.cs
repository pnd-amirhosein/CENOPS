using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NTSW_CENOPS.Infrastructure.Jobs;

/// <summary>Actual bounded query rows are saved as JSON; the UI history still keeps only a small preview.</summary>
public sealed record JobQueryResult(int RowsRead, string Summary, string RowsJson);

public sealed class JobQueryExecutor(IConfiguration configuration, JobSourceCatalog sources)
{
    public async Task<JobQueryResult> ExecuteAsync(
        AutomatedJob job, DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc,
        CancellationToken ct)
    {
        if (!MonitoringSqlPolicy.IsAllowed(job.SqlValue))
            throw new InvalidOperationException("Only a single read-only SELECT statement is permitted.");
        // The source catalog never falls back to CENOPS Default.
        var raw = sources.ResolveConnection(job.SourceKey);
        var builder = new SqlConnectionStringBuilder(raw)
        {
            ApplicationName = "CENOPS Automated Jobs",
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ConnectTimeout = 8
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = job.SqlValue;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = Math.Clamp(configuration.GetValue<int?>("Jobs:CommandTimeoutSeconds") ?? 20, 2, 60);

        // A query can optionally use these UTC parameters for exact, retryable windows.
        // If a query uses GETDATE() instead, its actual range is controlled by that SQL.
        command.Parameters.Add("@WindowStartUtc", SqlDbType.DateTime2).Value = windowStartUtc.UtcDateTime;
        command.Parameters.Add("@WindowEndUtc", SqlDbType.DateTime2).Value = windowEndUtc.UtcDateTime;
        // Logs with datetime2 CallTime stored in local operational time can use
        // @WindowStart / @WindowEnd instead of the explicitly UTC parameters.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Jobs:TimeZoneId"] ?? "Asia/Tehran");
        command.Parameters.Add("@WindowStart", SqlDbType.DateTime2).Value = TimeZoneInfo.ConvertTime(windowStartUtc, zone).DateTime;
        command.Parameters.Add("@WindowEnd", SqlDbType.DateTime2).Value = TimeZoneInfo.ConvertTime(windowEndUtc, zone).DateTime;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, ct);
        var limit = Math.Clamp(configuration.GetValue<int?>("Jobs:MaxRows") ?? 200, 1, 1000);
        if (reader.FieldCount > 64)
            throw new InvalidOperationException("Job SQL returns too many columns (maximum 64).");

        var names = new string[reader.FieldCount];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < names.Length; i++)
        {
            var original = reader.GetName(i);
            var basis = string.IsNullOrWhiteSpace(original) ? $"Column{i + 1}" : original;
            var unique = basis;
            for (var suffix = 2; !seen.Add(unique); suffix++) unique = $"{basis}_{suffix}";
            names[i] = unique;
        }

        var results = new List<Dictionary<string, object?>>(Math.Min(limit, 200));
        var preview = new StringBuilder();
        while (await reader.ReadAsync(ct))
        {
            // Never silently persist a truncated result set as though it were complete.
            if (results.Count == limit)
                throw new InvalidOperationException($"Job SQL returned more than {limit} rows. Aggregate or limit the query before saving.");

            var row = new Dictionary<string, object?>(names.Length, StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++)
            {
                var value = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                if (value is string text && text.Length > 64_000)
                    throw new InvalidOperationException("A query cell exceeds the 64,000-character safety limit.");
                if (value is byte[] bytes && bytes.Length > 48_000)
                    throw new InvalidOperationException("A binary query cell exceeds the size limit.");
                // Known SqlDataReader CLR primitives are handled directly by System.Text.Json.
                // Unknown provider-specific types fall back to their invariant string representation.
                row[names[i]] = ToJsonValue(value);
                if (results.Count >= 5 || i >= 12) continue;
                if (i > 0) preview.Append(", ");
                var safeValue = value is null ? "null" : value is byte or short or int or long or float or double or decimal or bool
                    ? Convert.ToString(value, CultureInfo.InvariantCulture) : "[non-numeric]";
                preview.Append(names[i].Length > 60 ? names[i][..60] : names[i]).Append('=').Append(safeValue);
            }
            if (results.Count < 5 && preview.Length > 0) preview.Append(" | ");
            results.Add(row);
        }
        var rowsJson = JsonSerializer.Serialize(results);
        if (rowsJson.Length > 1_000_000)
            throw new InvalidOperationException("Query JSON exceeds the 1,000,000-character daily-window limit.");
        var summary = results.Count == 0 ? "Query returned no rows." : preview.ToString().TrimEnd(' ', '|');
        return new JobQueryResult(results.Count, summary[..Math.Min(summary.Length, 2400)], rowsJson);
    }

    private static object? ToJsonValue(object? value) => value switch
    {
        null => null,
        byte or short or int or long or float or double or decimal or bool or string
            or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid or byte[] => value,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
}
