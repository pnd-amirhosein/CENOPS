using System.Data;
using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.DatabaseInspector;
using NTSW_CENOPS.Domain.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NTSW_CENOPS.Infrastructure.DatabaseInspector;

/// <summary>
/// SQL Server catalog reader. Reads only sys.* metadata; never queries application rows.
/// Every call rechecks both actor permission and the server-side connection allowlist.
/// </summary>
public sealed class SqlServerDatabaseInspectorService(IConfiguration configuration, IAccessService access)
    : IDatabaseInspectorService
{
    private const int CommandTimeout = 12;

    private async Task AuthorizeAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!await access.HasAccessAsync(actor, AccessKeys.DatabaseInspector, ct))
            throw new UnauthorizedAccessException("دسترسی به Database Inspector مجاز نیست.");
    }

    private IReadOnlyList<(DatabaseSource Source, string Connection)> Configured()
    {
        var names = configuration.GetSection("DatabaseInspector:ConnectionNames").Get<string[]>();
        var candidates = names is { Length: > 0 }
            ? names
            : configuration.GetSection("ConnectionStrings").GetChildren().Select(x => x.Key).ToArray();
        var result = new List<(DatabaseSource, string)>();
        SqlConnectionStringBuilder? application = null;
        try
        {
            var appConnection = configuration.GetConnectionString("Default");
            if (!string.IsNullOrWhiteSpace(appConnection)) application = new SqlConnectionStringBuilder(appConnection);
        }
        catch (ArgumentException) { /* Invalid Default is handled at application startup. */ }
        foreach (var name in candidates.Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (name.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
            var value = configuration.GetConnectionString(name);
            if (string.IsNullOrWhiteSpace(value)) continue;
            try
            {
                var parsed = new SqlConnectionStringBuilder(value);
                if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog)) continue;
                if (application is not null &&
                    parsed.DataSource.Equals(application.DataSource, StringComparison.OrdinalIgnoreCase) &&
                    parsed.InitialCatalog.Equals(application.InitialCatalog, StringComparison.OrdinalIgnoreCase)) continue;
                result.Add((new DatabaseSource(name, parsed.DataSource, parsed.InitialCatalog), value));
            }
            catch (ArgumentException) { /* Omit malformed connections without leaking configuration. */ }
        }
        return result;
    }

    private async Task<SqlConnection> OpenAsync(string key, CancellationToken ct)
    {
        var entry = Configured().FirstOrDefault(x => x.Source.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (entry.Source is null) throw new InvalidOperationException("پایگاه داده انتخاب‌شده در منابع خارجی مجاز نیست.");
        var builder = new SqlConnectionStringBuilder(entry.Connection)
        {
            ConnectTimeout = 5,
            ApplicationName = "NTSW CENOPS Database Inspector"
        };
        var sql = new SqlConnection(builder.ConnectionString);
        try { await sql.OpenAsync(ct); return sql; }
        catch { await sql.DisposeAsync(); throw; }
    }

    private static SqlCommand Command(SqlConnection connection, string sql, int? tableId = null)
    {
        var command = new SqlCommand(sql, connection) { CommandType = CommandType.Text, CommandTimeout = CommandTimeout };
        if (tableId is int id) command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        return command;
    }

    public async Task<IReadOnlyList<DatabaseSource>> GetAvailableSourcesAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AuthorizeAsync(actor, ct);
        var discovered = new List<DatabaseSource>();
        foreach (var source in Configured())
        {
            try
            {
                await using var sql = await OpenAsync(source.Source.Key, ct);
                // Empty/metadata-invisible databases have nothing meaningful to inspect.
                await using var command = Command(sql, @"SELECT TOP (1) t.object_id FROM sys.tables t
                    INNER JOIN sys.columns c ON c.object_id=t.object_id WHERE t.is_ms_shipped = 0");
                if (await command.ExecuteScalarAsync(ct) is not null) discovered.Add(source.Source);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (SqlException) { /* Inaccessible connection: not offered in the dropdown. */ }
            catch (Exception error) when (error is InvalidOperationException or TimeoutException or ArgumentException)
            { /* Invalid or timed-out source: not offered. */ }
        }
        return discovered;
    }

    public async Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(ClaimsPrincipal actor, string source, CancellationToken ct = default)
    {
        await AuthorizeAsync(actor, ct);
        await using var sql = await OpenAsync(source, ct);
        await using var command = Command(sql, @"SELECT t.object_id, s.name, t.name FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name");
        await using var reader = await command.ExecuteReaderAsync(ct);
        var tables = new List<DatabaseTable>();
        while (await reader.ReadAsync(ct)) tables.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return tables;
    }

    private const string ForeignKeySelectSql = @"
SELECT fk.object_id, fk.name, child.object_id, cs.name, child.name,
       parent.object_id, ps.name, parent.name, cc.name, pc.name, fkc.constraint_column_id,
       fk.delete_referential_action_desc, fk.update_referential_action_desc
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
JOIN sys.tables child ON child.object_id=fk.parent_object_id
JOIN sys.schemas cs ON cs.schema_id=child.schema_id
JOIN sys.tables parent ON parent.object_id=fk.referenced_object_id
JOIN sys.schemas ps ON ps.schema_id=parent.schema_id
JOIN sys.columns cc ON cc.object_id=child.object_id AND cc.column_id=fkc.parent_column_id
JOIN sys.columns pc ON pc.object_id=parent.object_id AND pc.column_id=fkc.referenced_column_id
";
    private const string ForeignKeySql = ForeignKeySelectSql +
        "WHERE fk.parent_object_id=@id OR fk.referenced_object_id=@id ORDER BY fk.object_id, fkc.constraint_column_id";
    private const string AllForeignKeySql = ForeignKeySelectSql +
        "ORDER BY fk.object_id, fkc.constraint_column_id";

    private static async Task<IReadOnlyList<DatabaseForeignKey>> ReadRelationsAsync(SqlConnection sql, int? id, CancellationToken ct)
    {
        await using var command = Command(sql, id.HasValue ? ForeignKeySql : AllForeignKeySql, id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new Dictionary<int, (string Name, DatabaseTable Child, DatabaseTable Parent,
            string Delete, string Update, List<DatabaseForeignKeyColumn> Columns)>();
        while (await reader.ReadAsync(ct))
        {
            int fkId = reader.GetInt32(0);
            if (!rows.TryGetValue(fkId, out var group))
            {
                group = (reader.GetString(1),
                    new DatabaseTable(reader.GetInt32(2), reader.GetString(3), reader.GetString(4)),
                    new DatabaseTable(reader.GetInt32(5), reader.GetString(6), reader.GetString(7)),
                    reader.GetString(11), reader.GetString(12), []);
                rows.Add(fkId, group);
            }
            group.Columns.Add(new(reader.GetString(8), reader.GetString(9), reader.GetInt32(10)));
        }
        return rows.Select(x => new DatabaseForeignKey(x.Key, x.Value.Name, x.Value.Child, x.Value.Parent,
            x.Value.Columns.OrderBy(y => y.Ordinal).ToArray(), x.Value.Delete, x.Value.Update)).ToArray();
    }

    private static async Task<bool> TableExistsAsync(SqlConnection sql, int tableId, CancellationToken ct)
    {
        await using var command = Command(sql, "SELECT TOP 1 1 FROM sys.tables WHERE object_id=@id AND is_ms_shipped=0", tableId);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<IReadOnlyList<DatabaseForeignKey>> GetAllRelationsAsync(ClaimsPrincipal actor, string source,
        CancellationToken ct = default)
    {
        await AuthorizeAsync(actor, ct);
        await using var sql = await OpenAsync(source, ct);
        return await ReadRelationsAsync(sql, null, ct);
    }

    public async Task<IReadOnlyList<DatabaseForeignKey>> GetRelationsAsync(ClaimsPrincipal actor, string source,
        int tableId, CancellationToken ct = default)
    {
        await AuthorizeAsync(actor, ct);
        await using var sql = await OpenAsync(source, ct);
        if (!await TableExistsAsync(sql, tableId, ct)) throw new InvalidOperationException("جدول انتخاب‌شده قابل مشاهده نیست.");
        return await ReadRelationsAsync(sql, tableId, ct);
    }

    public async Task<DatabaseTableDesign> GetDesignAsync(ClaimsPrincipal actor, string source, int tableId,
        CancellationToken ct = default)
    {
        await AuthorizeAsync(actor, ct);
        await using var sql = await OpenAsync(source, ct);
        await using var header = Command(sql, @"SELECT t.object_id, s.name, t.name FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.object_id=@id AND t.is_ms_shipped=0", tableId);
        DatabaseTable table;
        await using (var reader = await header.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("جدول انتخاب‌شده قابل مشاهده نیست.");
            table = new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
        }
        var relations = await ReadRelationsAsync(sql, tableId, ct);
        var fkColumns = relations.Where(x => x.Child.Id == tableId)
            .SelectMany(x => x.Columns.Select(y => (y.ChildColumn, x.Name)))
            .GroupBy(x => x.ChildColumn, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
        const string columnSql = @"
SELECT c.column_id,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable,
       c.is_identity,c.is_computed,dc.definition,
       CONVERT(bit, CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END), computed.definition, c.collation_name
FROM sys.columns c JOIN sys.types ty ON c.user_type_id=ty.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
LEFT JOIN sys.computed_columns computed ON computed.object_id=c.object_id AND computed.column_id=c.column_id
LEFT JOIN (SELECT ic.object_id,ic.column_id FROM sys.indexes i
           JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
           WHERE i.is_primary_key=1) pk ON pk.object_id=c.object_id AND pk.column_id=c.column_id
WHERE c.object_id=@id ORDER BY c.column_id";
        var columns = new List<DatabaseColumn>();
        await using (var command = Command(sql, columnSql, tableId))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(1);
                var type = FormatType(reader.GetString(2), reader.GetInt16(3), reader.GetByte(4), reader.GetByte(5));
                columns.Add(new(reader.GetInt32(0), name, type, reader.GetBoolean(6), reader.GetBoolean(10),
                    reader.GetBoolean(7), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetBoolean(8),
                    fkColumns.TryGetValue(name, out var fks) ? fks : [],
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12)));
            }
        }
        const string indexSql = @"
SELECT i.name,i.is_unique,i.is_primary_key,i.type_desc,ic.key_ordinal,c.name
FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=@id AND i.name IS NOT NULL AND ic.is_included_column=0
ORDER BY i.index_id,ic.key_ordinal";
        var indexRows = new Dictionary<string, (bool Unique, bool Primary, string Type, List<string> Columns)>();
        await using (var command = Command(sql, indexSql, tableId))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(0);
                if (!indexRows.TryGetValue(name, out var row))
                {
                    row = (reader.GetBoolean(1), reader.GetBoolean(2), reader.GetString(3), []);
                    indexRows.Add(name, row);
                }
                row.Columns.Add(reader.GetString(5));
            }
        }
        var indexes = indexRows.Select(x => new DatabaseIndex(x.Key, x.Value.Unique, x.Value.Primary, x.Value.Type,
            x.Value.Columns.ToArray())).ToArray();
        return new(table, columns, relations.Where(x => x.Child.Id == tableId).ToArray(), indexes);
    }

    private static string FormatType(string type, short maxLength, byte precision, byte scale)
    {
        var lower = type.ToLowerInvariant();
        if (lower is "nvarchar" or "nchar") return $"{type}({(maxLength < 0 ? "MAX" : (maxLength / 2).ToString())})";
        if (lower is "varchar" or "char" or "varbinary" or "binary") return $"{type}({(maxLength < 0 ? "MAX" : maxLength.ToString())})";
        if (lower is "decimal" or "numeric") return $"{type}({precision},{scale})";
        if (lower is "datetime2" or "datetimeoffset" or "time") return $"{type}({scale})";
        return type;
    }
}
