using System.Security.Claims;

namespace NTSW_CENOPS.Application.SourceIntelligence;

/// <summary>Public, serializable contracts. Source paths are relative to a registered root.</summary>
public sealed record SourceRepository(Guid Id, string Name, string Folder, DateTimeOffset RegisteredUtc,
    DateTimeOffset? IndexedUtc = null, int FileCount = 0, int SymbolCount = 0, int RelationCount = 0,
    string? LastError = null, string? Fingerprint = null, string? ProjectFile = null);

public sealed record SourceSymbol(string Id, Guid RepositoryId, string Project, string Kind, string Name,
    string Signature, string Namespace, string File, int Line, int Column, string Snippet,
    string? ContainingSymbol = null);

public sealed record SourceRelation(string FromId, string ToId, string Kind, string File, int Line);

/// <summary>A source file discovered below a registered folder; paths are always relative.</summary>
public sealed record SourceProjectFile(string Path, string Name, string Kind);

/// <summary>An exact source usage of the selected declaration, with a snapshot code window.</summary>
public sealed record SourceUsage(string SymbolId, string Project, string File, string Container,
    int Line, int Column, string Kind, string Snippet, string SourceLine, string Namespace = "",
    string? MemberSymbolId = null, string? TypeSymbolId = null,
    SourceSymbol? ContainingDeclaration = null, bool OwnershipVerified = false);

/// <summary>One-hop, source-located references to a selected declaration. No recursive traversal.</summary>
public sealed record SourceReferenceResult(IReadOnlyList<SourceUsage> Items,
    bool MayBeIncomplete, int Total, int UnresolvedOwners = 0,
    bool HasVerifiedSymbolIdentity = true, int AmbiguousIndexBindings = 0);

/// <summary>The current contents of a previously indexed source file. Not an indexed snapshot.</summary>
public sealed record SourceFileResult(string File, string Content, int LineCount, bool MayDifferFromIndex);

public sealed record SourceSearchResult(SourceSymbol Symbol, string RepositoryName);
public sealed record SourceDeclarationCount(string Category, int Count);

/// <summary>Exact declaration matches; counts are computed before paging, even for large projects.</summary>
public sealed record SourceDeclarationSearchPage(
    IReadOnlyList<SourceSearchResult> Items, IReadOnlyList<SourceDeclarationCount> Categories,
    int Total, int FilteredTotal, int Page, int PageSize);


public sealed record SourceSymbolDetails(SourceSymbol Symbol, IReadOnlyList<SourceRelation> Incoming,
    IReadOnlyList<SourceRelation> Outgoing, IReadOnlyDictionary<string, SourceSymbol> Related,
    IReadOnlyList<SourceUsage> Usages, bool ReferencesMayBeIncomplete = false);

public interface ISourceIntelligenceService
{
    string RootPath { get; }
    Task<IReadOnlyList<string>> ListAvailableFoldersAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceProjectFile>> DiscoverProjectFilesAsync(ClaimsPrincipal actor, string folder, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceRepository>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<SourceRepository> RegisterAsync(ClaimsPrincipal actor, string name, string folder, string? projectFile = null, CancellationToken cancellationToken = default);
    Task<SourceRepository> IndexAsync(ClaimsPrincipal actor, Guid repositoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceSearchResult>> SearchAsync(ClaimsPrincipal actor, Guid? repositoryId, string query, int take = 80,
        CancellationToken cancellationToken = default);
    Task<SourceDeclarationSearchPage> ResolveDeclarationsAsync(ClaimsPrincipal actor, Guid? repositoryId,
        string exactName, string? category = null, int page = 1, int pageSize = 80,
        CancellationToken cancellationToken = default);
    Task<SourceSymbol?> GetDeclarationAsync(ClaimsPrincipal actor, Guid repositoryId, string symbolId,
        CancellationToken cancellationToken = default);
    Task<SourceReferenceResult> GetReferencesAsync(ClaimsPrincipal actor, Guid repositoryId,
        string symbolId, CancellationToken cancellationToken = default);
    /// <summary>Reads an indexed reference's .cs file, scoped to a selected symbol and the registered root.</summary>
    Task<SourceFileResult> GetSourceFileAsync(ClaimsPrincipal actor, Guid repositoryId,
        string symbolId, string relativeFile, CancellationToken cancellationToken = default);
    Task<SourceSymbolDetails?> GetSymbolAsync(ClaimsPrincipal actor, Guid repositoryId, string symbolId,
        CancellationToken cancellationToken = default);
}
