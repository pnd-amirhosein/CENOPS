using System.Security.Claims;

namespace NTSW_CENOPS.Application.DatabaseInspector;

/// <summary>Metadata-only SQL Server schema inspection. Credentials never cross the service boundary.</summary>
public sealed record DatabaseSource(string Key, string Server, string Database);
public sealed record DatabaseTable(int Id, string Schema, string Name)
{
    public string FullName => $"{Schema}.{Name}";
}
public sealed record DatabaseColumn(int Ordinal, string Name, string DataType, bool Nullable,
    bool PrimaryKey, bool Identity, string? DefaultValue, bool Computed, IReadOnlyList<string> ForeignKeys,
    string? ComputedExpression = null, string? Collation = null);
public sealed record DatabaseForeignKey(int Id, string Name, DatabaseTable Child, DatabaseTable Parent,
    IReadOnlyList<DatabaseForeignKeyColumn> Columns, string OnDelete, string OnUpdate);
public sealed record DatabaseForeignKeyColumn(string ChildColumn, string ParentColumn, int Ordinal);
public sealed record DatabaseIndex(string Name, bool Unique, bool PrimaryKey, string Type, IReadOnlyList<string> Columns);
public sealed record DatabaseTableDesign(DatabaseTable Table, IReadOnlyList<DatabaseColumn> Columns,
    IReadOnlyList<DatabaseForeignKey> ForeignKeys, IReadOnlyList<DatabaseIndex> Indexes);

public interface IDatabaseInspectorService
{
    Task<IReadOnlyList<DatabaseSource>> GetAvailableSourcesAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(ClaimsPrincipal actor, string source, CancellationToken ct = default);
    Task<DatabaseTableDesign> GetDesignAsync(ClaimsPrincipal actor, string source, int tableId, CancellationToken ct = default);
    /// <summary>Reads the complete FK topology in one catalog query, including tables outside the current diagram.</summary>
    Task<IReadOnlyList<DatabaseForeignKey>> GetAllRelationsAsync(ClaimsPrincipal actor, string source, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseForeignKey>> GetRelationsAsync(ClaimsPrincipal actor, string source, int tableId,
        CancellationToken ct = default);
}
