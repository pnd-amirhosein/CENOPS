using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NTSW_CENOPS.Infrastructure.SourceIntelligence;

/// <summary>
/// Discovers conservative, source-backed MediatR dispatch relationships without executing
/// imported projects or loading their NuGet packages. These are framework-level inferred
/// edges, not ordinary C# calls to Handle(). No graph traversal happens during indexing.
/// </summary>
internal static class MediatRDispatchIndexer
{
    internal sealed record Dispatch(
        ISymbol Handler, ISymbol Consumer, string Project, string File, int Line, int Column,
        string Container, string Namespace, string Code, string SourceLine, string Kind);

    private sealed record HandlerRegistration(IMethodSymbol Handle, string Id, ITypeSymbol RequestType, string Operation);

    internal static IReadOnlyList<Dispatch> Find(
        Solution solution, IReadOnlyList<(ISymbol Symbol, string Id)> declarations,
        Func<string[], int, string> codeWindow,
        CancellationToken cancellationToken)
    {
        // A global using may be located in a separate file. Resolve it per project,
        // not across the whole registered solution (which could contain unrelated apps).
        var globalImports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in solution.Projects)
        foreach (var file in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = file.GetSyntaxRootAsync(cancellationToken).GetAwaiter().GetResult();
            if (root is CompilationUnitSyntax unit && unit.Usings.Any(u =>
                u.GlobalKeyword.RawKind != 0 && IsMediatRImport(u)))
                globalImports.Add(project.AssemblyName ?? project.Name);
        }
        var registrations = FindHandlers(declarations, globalImports, cancellationToken);
        if (registrations.Count == 0) return [];

        // Never join requests/handlers using their printed type or method names.
        // A codebase may contain hundreds of different Handle() methods and duplicate
        // request names in unrelated projects. Roslyn semantic equality is required.
        var indexedDeclarations = new HashSet<ISymbol>(
            declarations.Select(d => SourceSymbolIdentity.Canonical(d.Symbol)),
            SymbolEqualityComparer.Default);
        var results = new List<Dispatch>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in solution.Projects)
        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.FilePath is null) continue;
            var model = document.GetSemanticModelAsync(cancellationToken).GetAwaiter().GetResult();
            var syntax = document.GetSyntaxRootAsync(cancellationToken).GetAwaiter().GetResult();
            if (model is null || syntax is null) continue;
            var code = document.GetTextAsync(cancellationToken).GetAwaiter().GetResult().ToString();
            var lines = code.Replace("\r\n", "\n").Split('\n');
            var mediatRUsing = HasMediatRUsing(syntax) ||
                globalImports.Contains(project.AssemblyName ?? project.Name);
            foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (call.Expression is not MemberAccessExpressionSyntax access ||
                    call.ArgumentList.Arguments.Count == 0) continue;
                var methodName = access.Name.Identifier.ValueText;
                var operation = methodName switch
                {
                    "Send" => "Request",
                    "Publish" => "Notification",
                    _ => null
                };
                if (operation is null || !IsMediatRReceiver(model, access, mediatRUsing, operation, cancellationToken))
                    continue;

                var argument = call.ArgumentList.Arguments[0].Expression;
                var requestType = model.GetTypeInfo(argument, cancellationToken).Type;
                if (requestType is null || requestType.TypeKind == TypeKind.Error)
                {
                    if (argument is ObjectCreationExpressionSyntax created)
                        requestType = model.GetTypeInfo(created.Type, cancellationToken).Type;
                    else if (argument is ImplicitObjectCreationExpressionSyntax)
                        requestType = model.GetTypeInfo(argument, cancellationToken).ConvertedType;
                }
                if (requestType is null || requestType.TypeKind == TypeKind.Error) continue;
                var handlers = registrations.Where(h => h.Operation == operation &&
                    SymbolEqualityComparer.Default.Equals(
                        SourceSymbolIdentity.Canonical(h.RequestType),
                        SourceSymbolIdentity.Canonical(requestType))).ToArray();
                if (handlers.Length == 0) continue;
                // Send expects one handler for the concrete request. If more than one
                // implementation is indexed, we cannot know which one DI will choose.
                // Publish intentionally fans out to all matching notification handlers.
                if (operation == "Request" && handlers.Length != 1) continue;

                // A dispatch belongs to the enclosing method/property, NEVER to the
                // containing class simply because it declares that method.
                var owner = SourceReferenceOwnership.Find(call, model, cancellationToken);
                if (owner is null || !indexedDeclarations.Contains(SourceSymbolIdentity.Canonical(owner))) continue;
                var pos = call.GetLocation().GetLineSpan().StartLinePosition;
                var line = pos.Line + 1;
                var column = pos.Character + 1;
                foreach (var handler in handlers)
                {
                    var unique = $"{handler.Id}|{document.FilePath}|{call.SpanStart}";
                    if (!seen.Add(unique)) continue;
                    results.Add(new Dispatch(handler.Handle, owner, project.Name, document.FilePath,
                        line, column, owner.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        owner.ContainingNamespace?.ToDisplayString() ?? "", codeWindow(lines, pos.Line),
                        pos.Line < lines.Length ? lines[pos.Line].Trim() : "",
                        operation == "Request" ? "MediatR.Send (inferred)" : "MediatR.Publish (inferred)"));
                }
            }
        }
        return results;
    }

    private static List<HandlerRegistration> FindHandlers(
        IReadOnlyList<(ISymbol Symbol, string Id)> declarations,
        HashSet<string> globalImports, CancellationToken ct)
    {
        var handlers = new List<HandlerRegistration>();
        foreach (var (symbol, id) in declarations)
        {
            ct.ThrowIfCancellationRequested();
            if (symbol is not IMethodSymbol { Name: "Handle" } method ||
                method.Parameters.Length == 0 || method.ContainingType is null) continue;
            var type = method.ContainingType;
            // Prefer real Roslyn interface identity; for manually copied solutions,
            // MediatR references may not be restored, so also inspect explicit C#
            // interface syntax (only when that file imports/qualifies MediatR).
            // When the interface resolves, validate the *actual implementation*,
            // not just a method named Handle with a coincidentally matching parameter.
            var operation = "";
            foreach (var iface in type.AllInterfaces)
            {
                if (iface.ContainingNamespace?.ToDisplayString() != "MediatR" ||
                    iface.TypeArguments.Length == 0 ||
                    iface.Name is not ("IRequestHandler" or "INotificationHandler") ||
                    !SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], method.Parameters[0].Type))
                    continue;
                if (iface.Name == "IRequestHandler" && iface.TypeArguments.Length is not (1 or 2))
                    continue;
                if (iface.Name == "INotificationHandler" && iface.TypeArguments.Length != 1)
                    continue;
                var implementsThisMethod = iface.GetMembers("Handle").OfType<IMethodSymbol>()
                    .Any(member => SymbolEqualityComparer.Default.Equals(
                        SourceSymbolIdentity.Canonical(type.FindImplementationForInterfaceMember(member) ?? member),
                        SourceSymbolIdentity.Canonical(method)));
                if (!implementsThisMethod) continue;
                operation = iface.Name == "INotificationHandler" ? "Notification" : "Request";
                break;
            }
            if (operation.Length == 0)
            {
                foreach (var syntaxRef in type.DeclaringSyntaxReferences)
                {
                    if (syntaxRef.GetSyntax(ct) is not TypeDeclarationSyntax declaration || declaration.BaseList is null)
                        continue;
                    var imported = HasMediatRUsing(declaration.SyntaxTree.GetRoot(ct)) ||
                        globalImports.Contains(type.ContainingAssembly.Name);
                    foreach (var baseType in declaration.BaseList.Types)
                    {
                        // Only a *directly implemented* handler interface counts.
                        // An IRequestHandler nested inside another generic base type is
                        // not proof that the class is itself a handler.
                        GenericNameSyntax? iface = baseType.Type switch
                        {
                            GenericNameSyntax generic => generic,
                            QualifiedNameSyntax qualified => qualified.Right as GenericNameSyntax,
                            AliasQualifiedNameSyntax alias => alias.Name as GenericNameSyntax,
                            _ => null
                        };
                        if (iface is not null && iface.Identifier.ValueText is not
                            ("IRequestHandler" or "INotificationHandler")) iface = null;
                        if (iface is null || iface.TypeArgumentList.Arguments.Count == 0 ||
                            !(imported || baseType.Type.ToString().Contains("MediatR.", StringComparison.Ordinal)))
                            continue;
                        var expected = iface.TypeArgumentList.Arguments[0];
                        var actualName = method.Parameters[0].Type.Name;
                        var syntaxName = expected switch
                        {
                            IdentifierNameSyntax name => name.Identifier.ValueText,
                            GenericNameSyntax generic => generic.Identifier.ValueText,
                            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                            _ => expected.ToString().Split('.').Last()
                        };
                        if (!string.Equals(actualName, syntaxName, StringComparison.Ordinal)) continue;
                        operation = iface.Identifier.ValueText == "INotificationHandler" ?
                            "Notification" : "Request";
                        break;
                    }
                    if (operation.Length > 0) break;
                }
            }
            if (operation.Length > 0 && method.Parameters[0].Type.TypeKind != TypeKind.Error)
                handlers.Add(new HandlerRegistration(method, id, method.Parameters[0].Type, operation));
        }
        return handlers;
    }

    private static bool IsMediatRImport(UsingDirectiveSyntax u) =>
        u.Name?.ToString() == "MediatR" ||
        u.Name?.ToString().StartsWith("MediatR.", StringComparison.Ordinal) == true;

    private static bool HasMediatRUsing(SyntaxNode syntax) =>
        syntax.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>().Any(IsMediatRImport);

    private static bool IsMediatRReceiver(SemanticModel model, MemberAccessExpressionSyntax access,
        bool imported, string operation, CancellationToken ct)
    {
        var receiver = model.GetTypeInfo(access.Expression, ct).Type;
        if (receiver is null) return false;
        var permitted = operation == "Request" ? new[] { "IMediator", "ISender" } :
            new[] { "IMediator", "IPublisher" };
        static bool IsAllowed(INamedTypeSymbol value, string[] names) =>
            names.Contains(value.Name, StringComparer.Ordinal) &&
            (value.ContainingNamespace?.ToDisplayString() == "MediatR" || value.TypeKind == TypeKind.Error);
        if (receiver is INamedTypeSymbol named)
        {
            if (IsAllowed(named, permitted) &&
                (named.TypeKind != TypeKind.Error || imported)) return true;
            return named.AllInterfaces.Any(i => IsAllowed(i, permitted) &&
                (i.TypeKind != TypeKind.Error || imported));
        }
        return false;
    }
}
