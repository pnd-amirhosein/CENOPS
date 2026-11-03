using System.Text.RegularExpressions;

namespace NTSW_CENOPS.Infrastructure.Jobs;

/// <summary>Defense-in-depth only. Dedicated SELECT-only SQL credentials are mandatory.</summary>
public static class MonitoringSqlPolicy
{
    private static readonly Regex Forbidden = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|DENY|BACKUP|RESTORE|DBCC|WAITFOR|INTO|BULK|OPENROWSET|OPENQUERY|OPENDATASOURCE|xp_\w+|sp_\w+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsAllowed(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || sql.Length > 16000) return false;
        var trimmed = sql.Trim();
        // Only a single SELECT; no comments, batches, temp table tricks or SQL scripting.
        if (!Regex.IsMatch(trimmed, @"^SELECT\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        if (trimmed.Contains(';') || trimmed.Contains("--") || trimmed.Contains("/*") || trimmed.Contains("*/")) return false;
        return !Forbidden.IsMatch(trimmed);
    }
}
