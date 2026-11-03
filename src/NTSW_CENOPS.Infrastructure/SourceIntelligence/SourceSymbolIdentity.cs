using Microsoft.CodeAnalysis;

namespace NTSW_CENOPS.Infrastructure.SourceIntelligence;

/// <summary>
/// Index-session identity map. Roslyn symbols, not names or display strings, determine
/// whether a reference targets an indexed declaration. Constructed generics map to
/// their original source declaration. Distinct overloads and containing types stay distinct.
/// </summary>
internal sealed class SourceSymbolIdentity
{
    private readonly Dictionary<ISymbol, string> _indexed = new(SymbolEqualityComparer.Default);
    // A persisted ID must identify exactly one semantic symbol. Do not allow two
    // distinct declarations to share it: later graph lookups use IDs as dictionary keys.
    private readonly Dictionary<string, ISymbol> _byId = new(StringComparer.Ordinal);

    internal static ISymbol Canonical(ISymbol symbol)
    {
        if (symbol is IAliasSymbol alias) symbol = alias.Target;
        if (symbol is IMethodSymbol { ReducedFrom: { } originalMethod }) symbol = originalMethod;
        return symbol.OriginalDefinition;
    }

    internal bool Register(ISymbol symbol, string id)
    {
        var canonical = Canonical(symbol);
        if (_indexed.ContainsKey(canonical)) return false; // e.g. another partial declaration
        if (_byId.ContainsKey(id))
            throw new InvalidOperationException(
                "Distinct source declarations were assigned the same ID. " +
                "The index was not published; inspect source symbol identity generation.");
        _indexed.Add(canonical, id);
        _byId.Add(id, canonical);
        return true;
    }

    internal bool TryResolve(ISymbol? symbol, out string id)
    {
        if (symbol is not null && _indexed.TryGetValue(Canonical(symbol), out var result))
        {
            id = result;
            return true;
        }
        id = string.Empty;
        return false;
    }

    /// <summary>
    /// Source-stable key for persistence. The caller adds the registered project path,
    /// keeping same-assembly-name projects independent. This key is never used to
    /// decide semantic equivalence during indexing.
    /// </summary>
    internal static string PersistentDeclarationKey(ISymbol symbol, string projectPath, string? sourceRoot = null)
    {
        var original = Canonical(symbol);
        var signature = original.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var key = projectPath.Replace('\\', '/') + "|" + original.Kind + "|" + signature;

        // FullyQualifiedFormat is a *display* format, not a unique identity.
        // Distinct namespace fragments, explicit implementations or similarly
        // displayed symbols can produce the same text. A stable source anchor
        // disambiguates them without ever using a name to resolve references.
        // Partial declarations share a canonical Roslyn symbol and are indexed once.
        var location = original.Locations
            .Where(x => x.IsInSource)
            .OrderBy(x => x.SourceTree?.FilePath ?? "", StringComparer.Ordinal)
            .ThenBy(x => x.SourceSpan.Start)
            .FirstOrDefault();
        if (location is not null)
        {
            var file = location.SourceTree?.FilePath ?? "";
            if (sourceRoot is not null && Path.IsPathFullyQualified(file))
                file = Path.GetRelativePath(sourceRoot, file);
            key += "|at:" + file.Replace('\\', '/') + ":" + location.SourceSpan.Start;
        }
        return key;
    }
}
