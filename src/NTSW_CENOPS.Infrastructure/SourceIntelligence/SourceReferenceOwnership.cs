using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NTSW_CENOPS.Infrastructure.SourceIntelligence;

/// <summary>
/// Finds the nearest meaningful declaration that physically contains a source occurrence.
/// Never infers ownership from a shared line, namespace, a relation, or a similar symbol name.
/// Callers must independently check that the resolved symbol is present in the index.
/// </summary>
internal static class SourceReferenceOwnership
{
    internal static ISymbol? Find(SyntaxNode occurrence, SemanticModel model, CancellationToken ct)
    {
        foreach (var ancestor in occurrence.Ancestors())
        {
            ct.ThrowIfCancellationRequested();
            // Accessor method IDs are not indexed independently; the owning property/event is.
            if (ancestor is AccessorDeclarationSyntax accessor)
            {
                if (accessor.Parent?.Parent is SyntaxNode propertyOrEvent)
                {
                    var associated = model.GetDeclaredSymbol(propertyOrEvent, ct);
                    if (associated is IPropertySymbol or IEventSymbol) return associated;
                }
                continue;
            }
            // Local variables and parameters are occurrences *inside* their method.
            // Only class-level fields/events become consumer vertices themselves.
            if (ancestor is VariableDeclaratorSyntax variable &&
                ancestor.Parent?.Parent is FieldDeclarationSyntax or EventFieldDeclarationSyntax)
                return model.GetDeclaredSymbol(variable, ct);

            // A field's type occurs in VariableDeclaration.Type, outside the
            // VariableDeclarator. Resolve a single field directly; multiple field
            // declarators cannot be uniquely attributed to one vertex.
            if (ancestor is FieldDeclarationSyntax field)
                return field.Declaration.Variables.Count == 1
                    ? model.GetDeclaredSymbol(field.Declaration.Variables[0], ct) : null;
            if (ancestor is EventFieldDeclarationSyntax eventField)
                return eventField.Declaration.Variables.Count == 1
                    ? model.GetDeclaredSymbol(eventField.Declaration.Variables[0], ct) : null;

            if (ancestor is LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or
                PropertyDeclarationSyntax or IndexerDeclarationSyntax or EventDeclarationSyntax or
                BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or EnumMemberDeclarationSyntax)
            {
                var symbol = model.GetDeclaredSymbol(ancestor, ct);
                if (symbol is not null && !symbol.IsImplicitlyDeclared) return symbol;
            }
        }
        // A top-level statement, malformed declaration, or unresolved semantic model
        // is unknown: never invent a Namespace or choose another declaration by proximity.
        return null;
    }
}
