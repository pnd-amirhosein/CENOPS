// Run with: dotnet run --project tests/SourceIntelligenceSemanticIdentityRegression
// No imported application code is executed by CENOPS; this fixture is an isolated test project.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NTSW_CENOPS.Infrastructure.SourceIntelligence;
using System.Text;

const string code = """
    using System;
    using MediatR;
    namespace MediatR
    {
        public interface ISender { object Send<T>(T request); }
        public interface IRequestHandler<T, R> { R Handle(T request, System.Threading.CancellationToken token); }
    }
    namespace Billing
    {
        public sealed class InvoiceQuery { }
        public sealed class InvoiceCommand { }
        public sealed class InvoiceQueryHandler : IRequestHandler<InvoiceQuery, int>
        {
            public int Handle(InvoiceQuery request, System.Threading.CancellationToken token) => 1;
            public int Handle(string unrelated) => 0;
        }
        public sealed class InvoiceCommandHandler : IRequestHandler<InvoiceCommand, int>
        {
            public int Handle(InvoiceCommand request, System.Threading.CancellationToken token) => 2;
        }
        public sealed class Utility
        {
            public int Handle(InvoiceQuery request, System.Threading.CancellationToken token) => 3;
        }
        public sealed class Controller
        {
            private readonly ISender sender;
            public Controller(ISender sender) { this.sender = sender; }
            public object Get() => sender.Send(new InvoiceQuery());
            public object Post() => sender.Send(new InvoiceCommand());
        }
        public sealed class GenericBox<T> { }
        public sealed class GenericConsumer
        {
            public void Process() { var box = new GenericBox<InvoiceQuery>(); }
        }
    }
    """;

using var workspace = new AdhocWorkspace();
var projectId = ProjectId.CreateNewId();
var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
    projectId, VersionStamp.Create(), "Billing", "Billing", LanguageNames.CSharp))
    .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
    .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Latest))
    .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
var docId = DocumentId.CreateNewId(projectId);
solution = solution.AddDocument(docId, "Sample.cs", SourceText.From(code, Encoding.UTF8), filePath: "Sample.cs");
Check(workspace.TryApplyChanges(solution), "Roslyn workspace initialized");
solution = workspace.CurrentSolution;
var document = solution.GetDocument(docId)!;
var tree = (await document.GetSyntaxRootAsync())!;
var model = (await document.GetSemanticModelAsync())!;
var methods = tree.DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(x => x.Identifier.Text == "Handle")
    .Select(x => (Syntax: x, Symbol: (IMethodSymbol)model.GetDeclaredSymbol(x)!)).ToArray();
Check(methods.Length == 4, "Expected two handlers, an overload and unrelated Utility.Handle");
var identities = new SourceSymbolIdentity();
var ids = new List<string>();
for (var i = 0; i < methods.Length; i++)
{
    var id = "method:" + i;
    Check(identities.Register(methods[i].Symbol, id), "Each Handle declaration must be distinct");
    ids.Add(id);
}
Check(ids.Length == 4 && ids.Distinct().Count() == 4, "Different Handle declarations retain distinct IDs");
var queryHandler = methods.Single(x => x.Symbol.ContainingType.Name == "InvoiceQueryHandler" &&
    x.Symbol.Parameters.Length == 2).Symbol;
var commandHandler = methods.Single(x => x.Symbol.ContainingType.Name == "InvoiceCommandHandler").Symbol;
var utilityHandler = methods.Single(x => x.Symbol.ContainingType.Name == "Utility").Symbol;
Check(identities.TryResolve(queryHandler, out var queryId), "Query handler resolves");
Check(identities.TryResolve(commandHandler, out var commandId) && commandId != queryId,
    "Command handler is not query handler");
Check(identities.TryResolve(utilityHandler, out var utilityId) && utilityId != queryId,
    "Unrelated Handle is not query handler");

var boxDefinition = tree.DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Single(x => x.Identifier.Text == "GenericBox");
var boxSymbol = model.GetDeclaredSymbol(boxDefinition)!;
Check(identities.Register(boxSymbol, "generic:box"), "Generic definition indexed");
var boxUsage = tree.DescendantNodes().OfType<GenericNameSyntax>()
    .Single(x => x.Identifier.Text == "GenericBox");
var constructedBox = model.GetSymbolInfo(boxUsage).Symbol;
Check(identities.TryResolve(constructedBox, out var genericId) && genericId == "generic:box",
    "Constructed generic type resolves to its declaration");

// Real, resolved MediatR-like interface identities must map each Send to the
// appropriate handler. A utility Handle method or overload must never be chosen.
var declarations = tree.DescendantNodes()
    .Where(x => x is BaseTypeDeclarationSyntax or MethodDeclarationSyntax or ConstructorDeclarationSyntax)
    .Select(x => model.GetDeclaredSymbol(x))
    .Where(x => x is not null)
    .Select((x, i) => (Symbol: x!, Id: "declaration:" + i)).ToArray();
var dispatches = MediatRDispatchIndexer.Find(solution, declarations,
    (lines, index) => lines[index], CancellationToken.None);
Check(dispatches.Count == 2, $"Expected two resolved Send sites, got {dispatches.Count}");
Check(dispatches.Any(x => SymbolEqualityComparer.Default.Equals(x.Handler, queryHandler) && x.Consumer.Name == "Get"),
    "Get() must dispatch only to InvoiceQueryHandler.Handle(Query,...)");
Check(dispatches.Any(x => SymbolEqualityComparer.Default.Equals(x.Handler, commandHandler) && x.Consumer.Name == "Post"),
    "Post() must dispatch only to InvoiceCommandHandler.Handle(Command,...)");
Check(dispatches.All(x => !SymbolEqualityComparer.Default.Equals(x.Handler, utilityHandler)),
    "Unrelated Handle methods may not become inferred MediatR edges");
// An unresolved overload set must never be resolved by taking CandidateSymbols[0].
var ambiguousTree = CSharpSyntaxTree.ParseText("""
    class Ambiguous
    {
        void Choose(string value) { }
        void Choose(int? value) { }
        void Run() { Choose(null); }
    }
    """, path: "Ambiguous.cs");
var ambiguousCompilation = CSharpCompilation.Create("Ambiguous", [ambiguousTree],
    [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
var ambiguousModel = ambiguousCompilation.GetSemanticModel(ambiguousTree);
var ambiguousCall = ambiguousTree.GetRoot().DescendantNodes()
    .OfType<InvocationExpressionSyntax>().Single();
var ambiguousBinding = ambiguousModel.GetSymbolInfo(ambiguousCall);
Check(ambiguousBinding.Symbol is null && ambiguousBinding.CandidateSymbols.Length >= 2,
    "Overload ambiguity must remain unresolved; never index the first candidate as a reference");

var projectA = SourceSymbolIdentity.PersistentDeclarationKey(queryHandler, "Application/A.csproj");
var projectB = SourceSymbolIdentity.PersistentDeclarationKey(queryHandler, "OtherApplication/B.csproj");
Check(projectA != projectB, "Same assembly/type/method names in different projects must have different persisted keys");
// Two independently compiled source declarations can share the same fully
// qualified name and project display path. The source anchor must make their
// persisted keys distinct, rather than producing the same 24-character ID.
var duplicateA = CSharpSyntaxTree.ParseText("namespace Invoices { public class Invoice {} }", path: "First.cs");
var duplicateB = CSharpSyntaxTree.ParseText("namespace Invoices { public class Invoice {} }", path: "Second.cs");
var compilationA = CSharpCompilation.Create("Billing", [duplicateA]);
var compilationB = CSharpCompilation.Create("Billing", [duplicateB]);
var symbolA = compilationA.GetTypeByMetadataName("Invoices.Invoice")!;
var symbolB = compilationB.GetTypeByMetadataName("Invoices.Invoice")!;
Check(symbolA.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
      symbolB.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    "Fixture uses equal display strings");
var persistentA = SourceSymbolIdentity.PersistentDeclarationKey(symbolA, "Billing/Billing.csproj");
var persistentB = SourceSymbolIdentity.PersistentDeclarationKey(symbolB, "Billing/Billing.csproj");
Check(persistentA != persistentB, "Distinct source locations must have distinct persisted keys");
var collisionGuard = new SourceSymbolIdentity();
var first = compilationA.GetTypeByMetadataName("Invoices.Invoice")!;
var second = first.ContainingNamespace;
Check(collisionGuard.Register(first, "same-id"), "First declaration registers");
var collisionRejected = false;
try { collisionGuard.Register(second, "same-id"); }
catch (InvalidOperationException) { collisionRejected = true; }
Check(collisionRejected, "Distinct declarations cannot be registered with the same ID");
Console.WriteLine("PASS: exact Handle IDs, generic definitions, MediatR request routing, ambiguous overloads and project-scoped keys, collision-safe persistent identities.");

static void Check(bool condition, string detail)
{
    if (!condition) throw new InvalidOperationException(detail);
}
