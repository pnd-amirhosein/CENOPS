using NTSW_CENOPS.Application.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NTSW_CENOPS.Infrastructure.Jobs;

/// <summary>
/// Explicit allowlist of named connection strings available to the job engine.
/// Connections are selected by stable key and presented to the UI as server/database.
/// Never enumerate Default or arbitrary application connection strings.
/// </summary>
public sealed class JobSourceCatalog(IConfiguration configuration) : IJobSourceCatalog
{
    public IReadOnlyList<JobSourceOption> GetSources()
    {
        var result = new List<JobSourceOption>();
        var keys = configuration.GetSection("Jobs:SourceConnectionNames").Get<string[]>() ?? [];
        SqlConnectionStringBuilder? appDatabase = null;
        try
        {
            var appString = configuration.GetConnectionString("Default");
            if (!string.IsNullOrWhiteSpace(appString)) appDatabase = new SqlConnectionStringBuilder(appString);
        }
        catch (ArgumentException) { /* Invalid Default is handled by application startup. */ }
        foreach (var key in keys.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (key.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
            var connection = configuration.GetConnectionString(key);
            if (string.IsNullOrWhiteSpace(connection)) continue;
            try
            {
                var parsed = new SqlConnectionStringBuilder(connection);
                if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog)) continue;
                // Never expose the application database as an executable monitoring target.
                if (appDatabase is not null &&
                    parsed.DataSource.Equals(appDatabase.DataSource, StringComparison.OrdinalIgnoreCase) &&
                    parsed.InitialCatalog.Equals(appDatabase.InitialCatalog, StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new JobSourceOption(key, parsed.DataSource, parsed.InitialCatalog));
            }
            catch (ArgumentException) { /* Invalid connection: never offer it as an executable source. */ }
        }
        return result;
    }

    public bool IsConfigured(string key) =>
        !string.IsNullOrWhiteSpace(key) && GetSources().Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    internal string ResolveConnection(string key)
    {
        if (!IsConfigured(key)) throw new InvalidOperationException("منبع انتخاب‌شده در تنظیمات اتصال کارهای خودکار تعریف نشده است.");
        return configuration.GetConnectionString(key)!;
    }
}
