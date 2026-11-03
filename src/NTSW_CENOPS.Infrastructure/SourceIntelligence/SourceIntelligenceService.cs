using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.SourceIntelligence;
using NTSW_CENOPS.Domain.Authorization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NTSW_CENOPS.Infrastructure.SourceIntelligence;

public sealed class SourceIntelligenceOptions
{
    public string RootPath { get; set; } = string.Empty;
    public string IndexPath { get; set; } = string.Empty;
    public int MaxFiles { get; set; } = 3000;
    public int MaxFileBytes { get; set; } = 2_000_000;
    public long MaxTotalBytes { get; set; } = 60_000_000;
    public int MaxRelations { get; set; } = 150_000;
}

/// <summary>
/// Read-only, bounded source index. Never uses MSBuildWorkspace: loading arbitrary csproj
/// through MSBuild can execute repository-controlled targets. No restore/build/code execution.
/// The service uses a synthetic Roslyn workspace, so semantic results may be partial when
/// external packages, generated files or build-specific compilation symbols are unavailable.
/// </summary>
public sealed class SourceIntelligenceService : ISourceIntelligenceService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private static readonly string[] Excluded = [".git", ".vs", "bin", "obj", "node_modules", "packages", "TestResults", "dist"];
    private readonly SourceIntelligenceOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, SourceIndex> _indexCache = new();

    public SourceIntelligenceService(IOptions<SourceIntelligenceOptions> options, IServiceScopeFactory scopes)
    { _options = options.Value; _scopes = scopes; }

    public string RootPath => _options.RootPath;
    private string SourceRoot => Root(_options.RootPath, "SourceIntelligence:RootPath");
    private string StoreRoot
    {
        get
        {
            var store = Root(_options.IndexPath, "SourceIntelligence:IndexPath");
            var source = SourceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(store.TrimEnd(Path.DirectorySeparatorChar), SourceRoot.TrimEnd(Path.DirectorySeparatorChar), comparison) ||
                store.StartsWith(source, comparison))
                throw new InvalidOperationException("IndexPath must be separate from RootPath.");
            return store;
        }
    }
    private string CatalogFile => Path.Combine(StoreRoot, "repositories.json");

    private static string Root(string configured, string name)
    {
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException($"Configure {name} before using Source Intelligence.");
        var path = Environment.ExpandEnvironmentVariables(configured);
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidOperationException($"{name} must be an absolute server filesystem path.");
        var full = Path.GetFullPath(path);
        if (full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{name} must not be inside wwwroot.");
        return full;
    }

    private async Task AssertAccessAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IAccessService>();
        if (!await access.HasAccessAsync(actor, AccessKeys.SourceIntelligence, ct))
            throw new UnauthorizedAccessException("Source Intelligence access is required.");
    }

    private static bool SafeName(string value) => value.Length is > 0 and <= 120 &&
        value is not "." and not ".." && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ' ');

    private string ResolveFolder(string name)
    {
        if (!SafeName(name)) throw new ArgumentException("Invalid source directory name.");
        var root = SourceRoot;
        var folder = Path.GetFullPath(Path.Combine(root, name));
        if (!string.Equals(Path.GetDirectoryName(folder)?.TrimEnd(Path.DirectorySeparatorChar),
            root.TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The source must be a direct child of the configured root.");
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Source directory was not found.");
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("Linked source directories are not allowed.");
        return folder;
    }

    public async Task<IReadOnlyList<string>> ListAvailableFoldersAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        if (!Directory.Exists(SourceRoot)) return [];
        return Directory.EnumerateDirectories(SourceRoot).Where(p => SafeName(Path.GetFileName(p)) &&
            (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0)
            .Select(Path.GetFileName).Where(x => x is not null).Select(x => x!)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<SourceProjectFile>> DiscoverProjectFilesAsync(
        ClaimsPrincipal actor, string folder, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        var root = ResolveFolder(folder);
        // Discovering a path reads only directory entries: never evaluate project files or build targets.
        return EnumerateSafeFiles(root, cancellationToken)
            .Where(IsProjectOrSolution)
            .Select(path => new SourceProjectFile(Relative(root, path),
                Path.GetFileNameWithoutExtension(path), Path.GetExtension(path).TrimStart('.').ToUpperInvariant()))
            .OrderBy(x => x.Kind == "SLN" || x.Kind == "SLNX" ? 0 : 1)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsProjectOrSolution(string path) =>
        path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    public async Task<IReadOnlyList<SourceRepository>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try { return (await LoadCatalogAsync(cancellationToken)).OrderBy(x => x.Name).ToList(); }
        finally { _gate.Release(); }
    }

    public async Task<SourceRepository> RegisterAsync(ClaimsPrincipal actor, string name, string folder,
        string? projectFile = null, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            throw new ArgumentException("A name between 1 and 120 characters is required.");
        var root = ResolveFolder(folder);
        projectFile = string.IsNullOrWhiteSpace(projectFile) ? null : projectFile.Replace('\\', '/');
        if (projectFile is not null && !EnumerateSafeFiles(root, cancellationToken)
                .Where(IsProjectOrSolution).Any(p => string.Equals(Relative(root, p), projectFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("The selected solution/project was not found inside this source folder.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var repos = await LoadCatalogAsync(cancellationToken);
            if (repos.Any(x => string.Equals(x.Folder, folder, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ProjectFile, projectFile, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("This directory and project scope are already registered.");
            var repo = new SourceRepository(Guid.NewGuid(), name.Trim(), folder, DateTimeOffset.UtcNow,
                ProjectFile: projectFile);
            repos.Add(repo);
            await WriteJsonAsync(CatalogFile, repos, cancellationToken);
            return repo;
        }
        finally { _gate.Release(); }
    }

    public async Task<SourceRepository> IndexAsync(ClaimsPrincipal actor, Guid repositoryId, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var repos = await LoadCatalogAsync(cancellationToken);
            var i = repos.FindIndex(x => x.Id == repositoryId);
            if (i < 0) throw new KeyNotFoundException("Repository not registered.");
            var repo = repos[i];
            try
            {
                var root = ResolveFolder(repo.Folder);
                // This is CPU intensive. Do not block the Blazor circuit thread.
                var result = await Task.Run(() => BuildIndex(repo, root, cancellationToken), cancellationToken);
                await WriteJsonAsync(IndexFile(repositoryId), result, cancellationToken);
                _indexCache[repositoryId] = result;
                repo = repo with { IndexedUtc = DateTimeOffset.UtcNow, FileCount = result.FileCount,
                    SymbolCount = result.Symbols.Count, RelationCount = result.Relations.Count,
                    Fingerprint = result.Fingerprint, LastError = null };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                repo = repo with { LastError = ex.Message.Length > 350 ? ex.Message[..350] : ex.Message };
                repos[i] = repo;
                await WriteJsonAsync(CatalogFile, repos, cancellationToken);
                throw;
            }
            repos[i] = repo;
            await WriteJsonAsync(CatalogFile, repos, cancellationToken);
            return repo;
        }
        finally { _gate.Release(); }
    }

    // Compatibility adapter for callers of the original search contract. All search entry
    // points now obey exact declaration-name semantics (never substring matching).
    public async Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        ClaimsPrincipal actor, Guid? repositoryId, string query, int take = 80,
        CancellationToken cancellationToken = default)
    {
        var page = await ResolveDeclarationsAsync(actor, repositoryId, query,
            pageSize: take, cancellationToken: cancellationToken);
        return page.Items;
    }

    /// <summary>
    /// Resolve declarations by their own simple names, with ordinal case-sensitive equality.
    /// Counts precede pagination so no category silently disappears after the first 80 hits.
    /// References and symbol signatures are deliberately not searched.
    /// </summary>
    public async Task<SourceDeclarationSearchPage> ResolveDeclarationsAsync(
        ClaimsPrincipal actor, Guid? repositoryId, string exactName, string? category = null,
        int page = 1, int pageSize = 80, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        var q = exactName.Trim();
        if (q.Length is < 1 or > 256)
            return new SourceDeclarationSearchPage([], [], 0, 0, 1, Math.Clamp(pageSize, 1, 150));
        if (category is not null && !SourceDeclarationCategories.Order.Contains(category, StringComparer.Ordinal))
            throw new ArgumentException("Invalid declaration category.", nameof(category));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var repos = await LoadCatalogAsync(cancellationToken);
            var hits = new List<SourceSearchResult>();
            foreach (var repo in repos.Where(r => !repositoryId.HasValue || r.Id == repositoryId.Value))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var index = await LoadIndexAsync(repo.Id, cancellationToken);
                if (index is null) continue;
                hits.AddRange(index.Symbols.Where(s => string.Equals(s.Name, q, StringComparison.Ordinal))
                    .Select(s => new SourceSearchResult(s, repo.Name)));
            }
            var counts = hits.GroupBy(h => SourceDeclarationCategories.ForKind(h.Symbol.Kind))
                .Select(g => new SourceDeclarationCount(g.Key, g.Count())).ToArray();
            var selected = category is null ? hits : hits.Where(h =>
                SourceDeclarationCategories.ForKind(h.Symbol.Kind) == category).ToList();
            var size = Math.Clamp(pageSize, 1, 150);
            var pages = Math.Max(1, (selected.Count + size - 1) / size);
            var actualPage = Math.Clamp(page, 1, pages);
            var ordered = selected.OrderBy(h => CategoryOrder(SourceDeclarationCategories.ForKind(h.Symbol.Kind)))
                .ThenBy(h => h.Symbol.Kind, StringComparer.Ordinal)
                .ThenBy(h => h.RepositoryName, StringComparer.Ordinal)
                .ThenBy(h => h.Symbol.Project, StringComparer.Ordinal)
                .ThenBy(h => h.Symbol.File, StringComparer.Ordinal)
                .ThenBy(h => h.Symbol.Line)
                .ThenBy(h => h.Symbol.Column)
                .Skip((actualPage - 1) * size).Take(size).ToArray();
            return new SourceDeclarationSearchPage(ordered, counts, hits.Count, selected.Count,
                actualPage, size);
        }
        finally { _gate.Release(); }
    }

    private static int CategoryOrder(string category)
    {
        for (var index = 0; index < SourceDeclarationCategories.Order.Count; index++)
            if (SourceDeclarationCategories.Order[index] == category) return index;
        return SourceDeclarationCategories.Order.Count;
    }

    // Versions before v8 may contain colliding persisted IDs. They are not safe
    // for semantic graph expansion; omit ambiguous IDs from lookup dictionaries.
    // Tree occurrences remain available so users can inspect and then reindex.
    private static Dictionary<string, SourceSymbol> UnambiguousSymbolsById(
        IEnumerable<SourceSymbol> symbols) => symbols
        .GroupBy(s => s.Id, StringComparer.Ordinal)
        .Where(group => group.Count() == 1)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>Definition only: does not traverse the relationships or reference index.</summary>
    public async Task<SourceSymbol?> GetDeclarationAsync(ClaimsPrincipal actor, Guid repositoryId,
        string symbolId, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = await LoadIndexAsync(repositoryId, cancellationToken);
            return index?.Symbols.FirstOrDefault(s => s.Id == symbolId && s.RepositoryId == repositoryId);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Direct usages only. A reference has a concrete file/line and never recursively follows
    /// callers. Interface implementations and overrides are represented by their declaration
    /// locations when no ordinary syntax reference exists.
    /// </summary>
    public async Task<SourceReferenceResult> GetReferencesAsync(ClaimsPrincipal actor, Guid repositoryId,
        string symbolId, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = await LoadIndexAsync(repositoryId, cancellationToken);
            if (index is null || !index.Symbols.Any(s => s.Id == symbolId && s.RepositoryId == repositoryId))
                return new SourceReferenceResult([], false, 0);

            var hasDuplicateIds = index.Symbols.GroupBy(s => s.Id, StringComparer.Ordinal)
                .Any(group => group.Count() > 1);
            var usages = new List<SourceUsage>();
            var locations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var usage in index.Usages ?? [])
            {
                if (usage.SymbolId != symbolId) continue;
                if (locations.Add($"{usage.Project}|{usage.File}|{usage.Line}|{usage.Column}"))
                    usages.Add(usage);
            }

            // Implementing an interface member does not require writing its name as a
            // reference. Surface the actual implementation declaration as a one-hop result.
            var implementations = index.Relations.Where(r => r.ToId == symbolId &&
                (r.Kind == "Implements" || r.Kind == "Overrides" || r.Kind == "Inherits"));
            var byId = UnambiguousSymbolsById(index.Symbols);
            foreach (var relation in implementations)
            {
                if (!byId.TryGetValue(relation.FromId, out var implementation)) continue;
                if (!locations.Add($"{implementation.Project}|{implementation.File}|{implementation.Line}|{implementation.Column}"))
                    continue;
                usages.Add(new SourceUsage(symbolId, implementation.Project, implementation.File,
                    implementation.ContainingSymbol ?? implementation.Signature,
                    implementation.Line, implementation.Column, relation.Kind,
                    implementation.Snippet, implementation.Signature, implementation.Namespace,
                    implementation.Id, implementation.Kind is "Class" or "Struct" or "Record" or "Interface"
                        ? implementation.Id : null, null, index.Version >= 8));
            }

            // Source-index v8 owners use syntax ancestry AND collision-free Roslyn IDs.
            // Old v1-v7 persisted IDs cannot safely drive multi-hop expansion:
            // never reconstruct an owner from (file,line) relation proximity.
            var symbolById = byId;
            var enriched = new List<SourceUsage>(usages.Count);
            foreach (var usage in usages)
            {
                SourceSymbol? owner = null;
                if (index.Version >= 8 && !hasDuplicateIds && usage.OwnershipVerified &&
                    usage.MemberSymbolId is { } memberId &&
                    symbolById.TryGetValue(memberId, out var member) &&
                    member.RepositoryId == repositoryId &&
                    string.Equals(member.Project, usage.Project, StringComparison.Ordinal))
                {
                    // A v8 ID came directly from the containing syntax declaration.
                    // Partial types can span files, so do not require symbol.File == usage.File.
                    owner = member;
                }
                enriched.Add(usage with { ContainingDeclaration = owner,
                    OwnershipVerified = owner is not null });
            }
            var ordered = enriched.OrderBy(u => u.Project, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.File, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.Line).ThenBy(u => u.Column).ToArray();
            return new SourceReferenceResult(ordered, index.Version < 8 || index.UsagesTruncated || hasDuplicateIds,
                ordered.Length, ordered.Count(u => !u.OwnershipVerified), index.Version >= 8 && !hasDuplicateIds,
                index.AmbiguousBindingsSkipped);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// On-demand file reading for the expanded reference viewer. Never accepts an arbitrary
    /// absolute path: both the symbol and file must exist in the authorized index. Reject
    /// traversal and linked paths, and enforce the same per-file limit as the indexer.
    /// </summary>
    public async Task<SourceFileResult> GetSourceFileAsync(ClaimsPrincipal actor, Guid repositoryId,
        string symbolId, string relativeFile, CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        if (string.IsNullOrWhiteSpace(relativeFile) || !relativeFile.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An indexed C# file is required.", nameof(relativeFile));

        string sourceRoot;
        string relative;
        DateTimeOffset? indexedUtc;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var repo = (await LoadCatalogAsync(cancellationToken)).FirstOrDefault(x => x.Id == repositoryId)
                ?? throw new KeyNotFoundException("Source registration not found.");
            var index = await LoadIndexAsync(repositoryId, cancellationToken)
                ?? throw new InvalidOperationException("The source has not been indexed.");
            if (!index.Symbols.Any(x => x.Id == symbolId && x.RepositoryId == repositoryId))
                throw new UnauthorizedAccessException("The selected symbol is not part of this source.");

            // This is deliberately narrower than arbitrary indexed-file browsing: only files
            // referenced by the currently selected symbol (or its own definition) are readable.
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            relative = relativeFile.Replace('\\', '/');
            var fileAllowed = index.Symbols.Any(x => x.Id == symbolId &&
                    string.Equals(x.File, relative, comparison)) ||
                (index.Usages?.Any(x => x.SymbolId == symbolId &&
                    string.Equals(x.File, relative, comparison)) ?? false) ||
                index.Relations.Any(x => x.ToId == symbolId &&
                    (x.Kind is "Implements" or "Overrides" or "Inherits") &&
                    index.Symbols.Any(y => y.Id == x.FromId && string.Equals(y.File, relative, comparison)));
            if (!fileAllowed)
                throw new UnauthorizedAccessException("This file is not a reference of the selected symbol.");
            sourceRoot = ResolveFolder(repo.Folder);
            indexedUtc = repo.IndexedUtc;
        }
        finally { _gate.Release(); }

        // No absolute inputs, parent traversal or filesystem links, including linked directories.
        var parts = relative.Split('/');
        if (parts.Any(x => x.Length == 0 || x is "." or ".."))
            throw new UnauthorizedAccessException("Invalid source path.");
        var current = sourceRoot;
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Linked source paths are not permitted.");
        }
        var full = Path.GetFullPath(current);
        if (!InsideDirectory(full, sourceRoot) || !File.Exists(full))
            throw new FileNotFoundException("Source file is unavailable; reindex the source.");
        var info = new FileInfo(full);
        if (info.Length > Math.Clamp(_options.MaxFileBytes, 1024, 10_000_000))
            throw new InvalidOperationException("Source file exceeds the configured MaxFileBytes limit.");
        var text = await File.ReadAllTextAsync(full, cancellationToken);
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lineCount = normalized.Count(ch => ch == '\n') + 1;
        return new SourceFileResult(relative, normalized, lineCount,
            !indexedUtc.HasValue || info.LastWriteTimeUtc > indexedUtc.Value.UtcDateTime);
    }

    public async Task<SourceSymbolDetails?> GetSymbolAsync(ClaimsPrincipal actor, Guid repositoryId, string symbolId,
        CancellationToken cancellationToken = default)
    {
        await AssertAccessAsync(actor, cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = await LoadIndexAsync(repositoryId, cancellationToken);
            var symbol = index?.Symbols.FirstOrDefault(x => x.Id == symbolId);
            if (index is null || symbol is null) return null;
            var incoming = index.Relations.Where(x => x.ToId == symbolId).Take(300).ToArray();
            var outgoing = index.Relations.Where(x => x.FromId == symbolId).Take(300).ToArray();
            var relatedIds = incoming.Select(x => x.FromId).Concat(outgoing.Select(x => x.ToId)).ToHashSet();
            var related = UnambiguousSymbolsById(index.Symbols.Where(x => relatedIds.Contains(x.Id)));
            var usages = index.Usages?.Where(x => x.SymbolId == symbolId)
                .OrderBy(x => x.Project, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.File, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Line).ThenBy(x => x.Column).ToArray() ?? [];
            return new SourceSymbolDetails(symbol, incoming, outgoing, related, usages,
                index.Version < 8 || index.UsagesTruncated ||
                index.Symbols.GroupBy(s => s.Id, StringComparer.Ordinal).Any(g => g.Count() > 1));
        }
        finally { _gate.Release(); }
    }

    private async Task<List<SourceRepository>> LoadCatalogAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(StoreRoot);
        if (!File.Exists(CatalogFile)) return [];
        await using var stream = File.OpenRead(CatalogFile);
        return await JsonSerializer.DeserializeAsync<List<SourceRepository>>(stream, Json, ct) ?? [];
    }

    private async Task<SourceIndex?> LoadIndexAsync(Guid id, CancellationToken ct)
    {
        if (_indexCache.TryGetValue(id, out var index)) return index;
        var path = IndexFile(id);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        index = await JsonSerializer.DeserializeAsync<SourceIndex>(stream, Json, ct);
        if (index?.Version is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8)) return null;
        _indexCache[id] = index;
        return index;
    }
    private string IndexFile(Guid id) => Path.Combine(StoreRoot, id.ToString("N") + ".json");

    private static async Task WriteJsonAsync<T>(string file, T data, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, data, Json, ct);
            File.Move(temp, file, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    // Indexed snapshots never include absolute paths. Only files below the registered directory are read.
    public sealed record SourceIndex(int Version, int FileCount, string Fingerprint,
        List<SourceSymbol> Symbols, List<SourceRelation> Relations,
        List<SourceUsage>? Usages = null, bool UsagesTruncated = false,
        int AmbiguousBindingsSkipped = 0);

    private SourceIndex BuildIndex(SourceRepository repo, string root, CancellationToken ct)
    {
        var files = EnumerateSafeFiles(root, ct);
        var allProjects = files.Where(x => x.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToList();
        var projects = ResolveProjectsForScope(repo, root, files, allProjects);
        var sources = files.Where(x => x.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            (repo.ProjectFile is null || projects.Any(p => InsideDirectory(path: x, directory: Path.GetDirectoryName(p)!))))
            .ToList();
        if (sources.Count == 0) throw new InvalidOperationException("No .cs source files were found.");
        if (sources.Count > Math.Clamp(_options.MaxFiles, 1, 100_000))
            throw new InvalidOperationException("Source exceeds MaxFiles. Raise the limit after evaluating capacity.");
        long total = 0;
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in sources.OrderBy(x => x, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            if (info.Length > Math.Clamp(_options.MaxFileBytes, 1024, 10_000_000))
                throw new InvalidOperationException($"File too large: {Path.GetRelativePath(root, path)}");
            total += info.Length;
            if (total > Math.Clamp(_options.MaxTotalBytes, 1_000_000L, 500_000_000L))
                throw new InvalidOperationException("Source exceeds MaxTotalBytes.");
            var body = File.ReadAllText(path);
            contents.Add(path, body);
            fingerprint.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path) + "\0" + body + "\0"));
        }
        var digest = Convert.ToHexString(fingerprint.GetHashAndReset());
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectPaths = projects.Count > 0 ? projects : [Path.Combine(root, "LooseSource.csproj")];
        var ids = projectPaths.ToDictionary(p => p, _ => ProjectId.CreateNewId(), StringComparer.OrdinalIgnoreCase);
        // Same-named csproj files may coexist in a large imported solution.
        // Assign distinct Roslyn assembly identities while retaining their display names.
        var duplicateNames = projectPaths.GroupBy(Path.GetFileNameWithoutExtension,
                StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var refs = RuntimeReferences();
        foreach (var path in projectPaths)
        {
            ct.ThrowIfCancellationRequested();
            var id = ids[path];
            var name = Path.GetFileNameWithoutExtension(path);
            var assemblyName = duplicateNames.Contains(name)
                ? name + "_Cenops_" + Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(Relative(root, path))))[..12]
                : name;
            var projectInfo = ProjectInfo.Create(
                id,
                VersionStamp.Create(),
                name,
                assemblyName,
                LanguageNames.CSharp,
                filePath: path);
            solution = solution.AddProject(projectInfo)
                .WithProjectCompilationOptions(id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithProjectParseOptions(id, new CSharpParseOptions(LanguageVersion.Latest));
            foreach (var reference in refs) solution = solution.AddMetadataReference(id, reference);
        }
        foreach (var path in projectPaths)
        {
            if (!File.Exists(path)) continue;
            foreach (var linked in ReadProjectReferences(path))
                if (ids.TryGetValue(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, linked.Replace('\\', Path.DirectorySeparatorChar))), out var target)
                    && target != ids[path])
                {
                    try { solution = solution.AddProjectReference(ids[path], new ProjectReference(target)); }
                    catch (InvalidOperationException) { /* Ignore cycles; semantic indexing remains best effort. */ }
                }
        }
        foreach (var (path, body) in contents)
        {
            ct.ThrowIfCancellationRequested();
            var owner = projectPaths.Where(p => path.StartsWith(Path.GetDirectoryName(p)! + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                .OrderByDescending(p => Path.GetDirectoryName(p)!.Length).FirstOrDefault() ?? projectPaths[0];
            solution = solution.AddDocument(DocumentId.CreateNewId(ids[owner]), Path.GetFileName(path),
                SourceText.From(body, Encoding.UTF8), filePath: path);
        }
        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("Roslyn workspace could not initialize.");
        solution = workspace.CurrentSolution;
        var symbols = new List<SourceSymbol>();
        var identities = new SourceSymbolIdentity();
        var discovered = new List<(ISymbol Symbol, string Id)>();
        // Stable IDs are scoped to the actual source project, not an assembly display name.
        // Multiple imported projects may use the same <AssemblyName>.
        var projectPathById = ids.ToDictionary(p => p.Value, p => Relative(root, p.Key));
        foreach (var project in solution.Projects)
        foreach (var doc in project.Documents)
        {
            ct.ThrowIfCancellationRequested();
            var model = doc.GetSemanticModelAsync(ct).GetAwaiter().GetResult();
            var rootNode = doc.GetSyntaxRootAsync(ct).GetAwaiter().GetResult();
            if (model is null || rootNode is null || doc.FilePath is null) continue;
            foreach (var node in rootNode.DescendantNodes().Where(IsDeclaration))
            {
                ct.ThrowIfCancellationRequested();
                var sym = model.GetDeclaredSymbol(node, ct);
                if (sym is null || sym.IsImplicitlyDeclared) continue;
                var key = SourceSymbolIdentity.PersistentDeclarationKey(sym, projectPathById[project.Id], root);
                var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24];
                if (!identities.Register(sym, id)) continue;
                var loc = sym.Locations.FirstOrDefault(x => x.IsInSource)?.GetLineSpan();
                var relative = Path.GetRelativePath(root, doc.FilePath).Replace('\\', '/');
                var item = new SourceSymbol(id, repo.Id, project.Name, DescribeKind(sym), DeclarationName(sym),
                    sym.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    sym.ContainingNamespace?.ToDisplayString() ?? "", relative,
                    (loc?.StartLinePosition.Line ?? 0) + 1, (loc?.StartLinePosition.Character ?? 0) + 1,
                    DeclarationSnippet(node),
                    sym.ContainingSymbol is INamespaceSymbol ? null :
                        sym.ContainingSymbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                symbols.Add(item);
                discovered.Add((sym, id));
            }
        }
        var relations = new List<SourceRelation>();
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        var relationLimit = Math.Clamp(_options.MaxRelations, 100, 1_000_000);
        void Add(ISymbol? from, ISymbol? to, string kind, string file, int line)
        {
            if (relations.Count >= relationLimit || from is null || to is null) return;
            if (!identities.TryResolve(from, out var sourceId) || !identities.TryResolve(to, out var targetId)) return;
            var identity = $"{sourceId}|{targetId}|{kind}|{file}|{line}";
            if (distinct.Add(identity)) relations.Add(new SourceRelation(sourceId, targetId, kind, file, line));
        }
        foreach (var (sym, _) in discovered)
        {
            ct.ThrowIfCancellationRequested();
            if (sym is INamedTypeSymbol named)
            {
                Add(named, named.BaseType, "Inherits", "", 0);
                foreach (var iface in named.Interfaces) Add(named, iface, "Implements", "", 0);
                foreach (var iface in named.AllInterfaces)
                    foreach (var member in iface.GetMembers())
                    {
                        var impl = named.FindImplementationForInterfaceMember(member);
                        if (impl is not null) Add(impl, member, "Implements", "", 0);
                    }
            }
            if (sym is IMethodSymbol m)
            {
                Add(m, m.OverriddenMethod, "Overrides", "", 0);
                foreach (var impl in m.ExplicitInterfaceImplementations) Add(m, impl, "Implements", "", 0);
            }
        }
        var usages = new List<SourceUsage>();
        var usageIdentity = new HashSet<string>(StringComparer.Ordinal);
        var ambiguousBindingsSkipped = 0;
        foreach (var project in solution.Projects)
        foreach (var doc in project.Documents)
        {
            ct.ThrowIfCancellationRequested();
            var model = doc.GetSemanticModelAsync(ct).GetAwaiter().GetResult();
            var rootNode = doc.GetSyntaxRootAsync(ct).GetAwaiter().GetResult();
            if (model is null || rootNode is null || doc.FilePath is null) continue;
            var file = Relative(root, doc.FilePath);
            var text = contents[doc.FilePath];
            var lines = text.Replace("\r\n", "\n").Split('\n');
            // One syntax name per occurrence prevents double-counting method invocations. For a
            // call, resolve the whole invocation so overload selection comes from Roslyn.
            foreach (var node in rootNode.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (usages.Count >= relationLimit && relations.Count >= relationLimit) break;
                ct.ThrowIfCancellationRequested();
                var invocation = node.Parent switch
                {
                    InvocationExpressionSyntax direct when direct.Expression == node => direct,
                    MemberAccessExpressionSyntax access when access.Name == node &&
                        access.Parent is InvocationExpressionSyntax call => call,
                    MemberBindingExpressionSyntax binding when binding.Name == node &&
                        binding.Parent is InvocationExpressionSyntax call => call,
                    _ => null
                };
                var resolution = invocation is null ? model.GetSymbolInfo(node, ct) :
                    model.GetSymbolInfo(invocation, ct);
                // CandidateSymbols are NOT confirmed bindings: picking the first candidate
                // can conflate Handle() overloads or unresolved interface methods.
                // Never draw a graph edge based on an ambiguous semantic result.
                var to = resolution.Symbol;
                if (to is null && resolution.CandidateSymbols.Length > 0)
                    ambiguousBindingsSkipped++;
                if (!identities.TryResolve(to, out var targetId)) continue;

                var location = node.GetLocation().GetLineSpan().StartLinePosition;
                var line = location.Line + 1;
                var col = location.Character + 1;
                var owner = SourceReferenceOwnership.Find(node, model, ct);
                string? enclosingMemberId = null;
                if (owner is not null && identities.TryResolve(owner, out var resolvedOwnerId))
                    enclosingMemberId = resolvedOwnerId;
                // If Roslyn did not produce an indexed declaration, keep this
                // occurrence in the Tree, but do NOT attach it to an invented vertex.
                var ownershipVerified = enclosingMemberId is not null;
                var container = ownershipVerified
                    ? owner!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                    : "Unresolved declaration";
                string? enclosingTypeId = null;
                var containingType = owner as INamedTypeSymbol ?? owner?.ContainingType;
                if (containingType is not null && identities.TryResolve(containingType, out var resolvedTypeId))
                    enclosingTypeId = resolvedTypeId;
                var usageKind = invocation is null ? "References" : "Calls";
                var identity = $"{targetId}|{file}|{line}|{col}";
                if (usages.Count < relationLimit && usageIdentity.Add(identity))
                {
                    var sourceLine = location.Line < lines.Length ? lines[location.Line].Trim() : "";
                    usages.Add(new SourceUsage(targetId, project.Name, file, container, line, col,
                        usageKind, CodeWindow(lines, location.Line), Clip(sourceLine, 400),
                        owner?.ContainingNamespace?.ToDisplayString() ?? "",
                        enclosingMemberId, enclosingTypeId, null, ownershipVerified));
                }
                if (relations.Count < relationLimit)
                {
                    // Store a relationship only when a declaration enclosing this exact
                    // token was indexed; never associate it via a neighboring line.
                    if (ownershipVerified) Add(owner, to, usageKind, file, line);
                }
            }
        }
        // MediatR bridges are *reverse dependencies*: callers of Send(request) consume
        // the handler's Handle method at runtime, although C# contains no direct Handle()
        // invocation. Store the dispatch site as a reference to the precise handler method.
        // Strict type matching and receiver checks prevent arbitrary Send() calls being
        // misrepresented as MediatR relationships.
        var mediatRDispatches = MediatRDispatchIndexer.Find(solution, discovered, CodeWindow, ct);
        foreach (var dispatch in mediatRDispatches)
        {
            ct.ThrowIfCancellationRequested();
            if (usages.Count >= relationLimit) break;
            if (!identities.TryResolve(dispatch.Handler, out var handlerId) ||
                !identities.TryResolve(dispatch.Consumer, out var consumerId)) continue;
            var relativeFile = Relative(root, dispatch.File);
            var identity = $"{handlerId}|{relativeFile}|{dispatch.Line}|{dispatch.Column}";
            if (!usageIdentity.Add(identity)) continue;
            string? typeId = null;
            if (dispatch.Consumer.ContainingType is { } containingType)
                if (identities.TryResolve(containingType, out var resolvedTypeId))
                    typeId = resolvedTypeId;
            usages.Add(new SourceUsage(handlerId, dispatch.Project, relativeFile, dispatch.Container,
                dispatch.Line, dispatch.Column, dispatch.Kind, dispatch.Code,
                Clip(dispatch.SourceLine, 400), dispatch.Namespace, consumerId, typeId,
                null, true));
            var relationIdentity = $"{consumerId}|{handlerId}|{dispatch.Kind}|{relativeFile}|{dispatch.Line}";
            if (relations.Count < relationLimit && distinct.Add(relationIdentity))
                relations.Add(new SourceRelation(consumerId, handlerId, dispatch.Kind, relativeFile, dispatch.Line));
        }
        return new SourceIndex(8, sources.Count, digest, symbols, relations, usages,
            usages.Count >= relationLimit, ambiguousBindingsSkipped);

    }

    private List<string> EnumerateSafeFiles(string root, CancellationToken ct)
    {
        var found = new List<string>();
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = stack.Pop();
            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                if (Directory.Exists(path))
                {
                    if (!Excluded.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)) stack.Push(path);
                }
                else if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || IsProjectOrSolution(path))
                {
                    found.Add(path);
                    if (found.Count > Math.Max(10_000, _options.MaxFiles * 3))
                        throw new InvalidOperationException("Too many source files.");
                }
            }
        }
        return found;
    }

    private static bool InsideDirectory(string path, string directory) =>
        path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string CodeWindow(string[] lines, int zeroBasedLine)
    {
        var start = Math.Max(0, zeroBasedLine - 3);
        var end = Math.Min(lines.Length, zeroBasedLine + 5);
        var output = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            output.Append(i == zeroBasedLine ? "> " : "  ");
            output.Append((i + 1).ToString().PadLeft(5));
            output.Append(" | ");
            output.AppendLine(Clip(lines[i].TrimEnd('\r'), 250));
        }
        return Clip(output.ToString(), 1100);
    }

    private static List<string> ResolveProjectsForScope(SourceRepository repo, string root,
        List<string> files, List<string> projects)
    {
        if (string.IsNullOrWhiteSpace(repo.ProjectFile)) return projects;
        var selected = files.FirstOrDefault(x => IsProjectOrSolution(x) &&
            string.Equals(Relative(root, x), repo.ProjectFile, StringComparison.OrdinalIgnoreCase));
        if (selected is null) throw new FileNotFoundException("The selected solution or project no longer exists. Register it again.");
        if (selected.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(selected);
            while (pending.Count > 0)
            {
                var project = pending.Pop();
                if (!visited.Add(project)) continue;
                foreach (var reference in ReadProjectReferences(project))
                {
                    var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!,
                        reference.Replace('\\', Path.DirectorySeparatorChar)));
                    if (projects.Contains(candidate, StringComparer.OrdinalIgnoreCase)) pending.Push(candidate);
                }
            }
            return projects.Where(visited.Contains).ToList();
        }
        IEnumerable<string> mentioned;
        if (selected.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = XmlReader.Create(selected,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            mentioned = XDocument.Load(reader).Descendants()
                .Where(e => e.Name.LocalName == "Project")
                .Select(e => (string?)e.Attribute("Path") ?? (string?)e.Attribute("Include"))
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray();
        }
        else
        {
            // Parse project declarations only; never execute solution or project MSBuild content.
            var source = File.ReadAllText(selected);
            mentioned = Regex.Matches(source,
                "^\\s*Project\\([^\\r\\n]*\\)\\s*=\\s*\"[^\"]+\"\\s*,\\s*\"([^\"]+\\.csproj)\"",
                RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3))
                .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }
        var selectedDir = Path.GetDirectoryName(selected)!;
        var members = mentioned.Select(relative => Path.GetFullPath(Path.Combine(selectedDir,
                relative.Replace('\\', Path.DirectorySeparatorChar))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = projects.Where(members.Contains).ToList();
        if (result.Count == 0)
            throw new InvalidOperationException("No readable .csproj inside the configured root was found for this solution.");
        return result;
    }

    private static IEnumerable<string> ReadProjectReferences(string path)
    {
        try
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader).Descendants().Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => (string?)e.Attribute("Include")).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToArray();
        }
        catch (XmlException) { return []; }
    }

    private static List<MetadataReference> RuntimeReferences()
    {
        var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "").Split(Path.PathSeparator);
        return trusted.Where(p => p.Length > 0 && (Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) ||
            Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll" or "Microsoft.CSharp.dll"))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }
    private static bool IsDeclaration(SyntaxNode n) => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or
        BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or PropertyDeclarationSyntax or
        IndexerDeclarationSyntax or EventDeclarationSyntax or VariableDeclaratorSyntax or
        ParameterSyntax or EnumMemberDeclarationSyntax or
        SingleVariableDesignationSyntax or ForEachStatementSyntax or CatchDeclarationSyntax or
        TypeParameterSyntax or NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax;
    private static string DeclarationName(ISymbol symbol) => symbol is IMethodSymbol
        { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor
        ? constructor.ContainingType.Name : symbol.Name;

    private static string DescribeKind(ISymbol s) => s switch
    {
        INamedTypeSymbol t when t.IsRecord => t.TypeKind == TypeKind.Struct ? "RecordStruct" : "Record",
        INamedTypeSymbol t => t.TypeKind.ToString(),
        IMethodSymbol m when m.MethodKind == MethodKind.Constructor => "Constructor",
        IMethodSymbol => "Method", IPropertySymbol => "Property", IEventSymbol => "Event",
        IFieldSymbol f when f.ContainingType?.TypeKind == TypeKind.Enum => "EnumMember",
        IFieldSymbol => "Field", ILocalSymbol => "Variable", IParameterSymbol => "Parameter",
        ITypeParameterSymbol => "TypeParameter",
        _ => s.Kind.ToString()
    };
    private static string DeclarationSnippet(SyntaxNode node)
    {
        // Keep declaration inspector concise for large class/method bodies. No source is executed.
        var body = node.ToString().Trim();
        var lines = body.Replace("\r\n", "\n").Split('\n');
        return Clip(string.Join("\n", lines.Take(24)), 2600) +
            (lines.Length > 24 ? "\n// …" : "");
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max] + "\n// …";
}
