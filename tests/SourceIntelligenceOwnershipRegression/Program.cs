// Runnable regression fixture; no source projects are executed or restored by CENOPS.
// dotnet run --project tests/SourceIntelligenceOwnershipRegression
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NTSW_CENOPS.Infrastructure.SourceIntelligence;

var files = new Dictionary<string, string>
{
    ["Handler.cs"] = """
        namespace Billing;
        public interface IRequestHandler<T> { }
        public class CheckSaveInspectionCertificateBillOfLading { }
        public class Handler : IRequestHandler<CheckSaveInspectionCertificateBillOfLading>
        {
            public void Handle(CheckSaveInspectionCertificateBillOfLading request) { }
        }
        """,
    ["Query.cs"] = """
        namespace Billing;
        public class Query
        {
            public void Validate() { var request = new CheckSaveInspectionCertificateBillOfLading(); }
        }
        """,
    ["Command.cs"] = """
        namespace Billing;
        public class Command
        {
            public void Save() { Send(new CheckSaveInspectionCertificateBillOfLading()); }
            private void Send(object request) { }
        }
        """,
    ["Tests.cs"] = """
        namespace Billing;
        public class Test
        {
            public void Mock() { object request = new CheckSaveInspectionCertificateBillOfLading(); }
        }
        """,
};
var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Value, path: f.Key)).ToArray();
var compilation = CSharpCompilation.Create("Billing", trees,
    [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var found = new List<(string File, string Owner, string Kind)>();
foreach (var tree in trees)
{
    var model = compilation.GetSemanticModel(tree);
    foreach (var occurrence in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
                 .Where(n => n.Identifier.ValueText == "CheckSaveInspectionCertificateBillOfLading"))
    {
        var owner = SourceReferenceOwnership.Find(occurrence, model, CancellationToken.None);
        if (owner is null) throw new Exception($"Unresolved known declaration owner: {tree.FilePath}");
        found.Add((tree.FilePath, owner.Name, owner.Kind.ToString()));
    }
}
Require(found.Count == 5, $"Expected 5 source occurrences; received {found.Count}.");
Require(found.Select(r => r.File).Distinct().Count() == 4, "Expected 4 source files.");
Require(found.Any(r => r.Owner == "Handler" && r.Kind == "NamedType"),
    "Generic IRequestHandler<T> must belong to handler class, not Namespace.");
Require(found.Any(r => r.Owner == "Handle" && r.Kind == "Method"),
    "Handle(Request request) must belong to Handle(), not unrelated handler method.");
Require(found.Any(r => r.Owner == "Validate"), "Validation method owner missing.");
Require(found.Any(r => r.Owner == "Save"), "Command method owner missing.");
Require(found.Any(r => r.Owner == "Mock"), "Test method owner missing.");
Require(found.All(r => r.Kind != "Namespace"), "A Namespace must never own an occurrence.");

// One physical line, two methods: old (file,line) relation lookup incorrectly merged these.
var sameLine = CSharpSyntaxTree.ParseText("""
    namespace Billing;
    class Invoice {} class Ambiguous { void Left() { new Invoice(); } void Right() { new Invoice(); } }
    """, path: "same-line.cs");
var sample = CSharpCompilation.Create("SameLine", [sameLine],
    [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
var sampleModel = sample.GetSemanticModel(sameLine);
var owners = sameLine.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
    .Where(n => n.Identifier.ValueText == "Invoice")
    .Select(n => SourceReferenceOwnership.Find(n, sampleModel, CancellationToken.None)?.Name)
    .ToArray();
Require(owners.SequenceEqual(["Left", "Right"]),
    "Same-line source occurrences must retain distinct containing methods.");

// A field initializer should belong to the field, while a local belongs to its method.
var nested = CSharpSyntaxTree.ParseText("""
    class Invoice {} class Owner {
        private Invoice value = new Invoice();
        void Run() { var invoice = new Invoice(); }
    }
    """, path: "ownership.cs");
var nestedModel = CSharpCompilation.Create("Owner", [nested],
    [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]).GetSemanticModel(nested);
var nestedOwners = nested.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
    .Where(n => n.Identifier.ValueText == "Invoice")
    .Select(n => SourceReferenceOwnership.Find(n, nestedModel, CancellationToken.None)?.Name).ToArray();
Require(nestedOwners.SequenceEqual(["value", "value", "Run"]),
    "Field declaration should be attached to the field; local references to the method.");

Console.WriteLine("PASS: 5 occurrences / 4 files, precise owners, no namespace ownership, same-line split, local and field ownership.");
static void Require(bool success, string message)
{
    if (!success) throw new Exception(message);
}
