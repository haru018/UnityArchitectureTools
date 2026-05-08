using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class Program
{
    static void Main(string[] args)
    {
        string targetDirectory = args.Length > 0 ? args[0] : "./";
        string outputPath = args.Length > 1 ? args[1] : "ProjectClassDiagram.puml";

        var csFiles = Directory.GetFiles(targetDirectory, "*.cs", SearchOption.AllDirectories);
        var syntaxTrees = new List<SyntaxTree>();

        foreach (var file in csFiles)
        {
            var code = File.ReadAllText(file);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(code, path: file));
        }

        var compilation = CSharpCompilation.Create("UnityProjectCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTrees);

        var allEdges = new HashSet<string>();

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var classNodes = tree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>();

            foreach (var node in classNodes)
            {
                if (!(semanticModel.GetDeclaredSymbol(node) is INamedTypeSymbol classSymbol)) continue;

                foreach (var member in classSymbol.GetMembers())
                {
                    if (member.IsImplicitlyDeclared) continue;

                    ITypeSymbol targetType;
                    if (member is IFieldSymbol fieldSymbol)
                        targetType = fieldSymbol.Type;
                    else if (member is IPropertySymbol propertySymbol)
                        targetType = propertySymbol.Type;
                    else
                        continue;

                    if (targetType is INamedTypeSymbol namedTargetType && namedTargetType.Locations.Any(l => l.IsInSource))
                        allEdges.Add($"{classSymbol.Name} --> {namedTargetType.Name}");
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("@startuml name\"ClassDiagram\"");
        sb.AppendLine("title \"Project - Class Diagram\"");
        sb.AppendLine("!define MonoBehaviour <<(M, Fuchsia)>>");
        sb.AppendLine("!define ScriptableObject <<(S, Turquoise)>>");
        sb.AppendLine("top to bottom direction");
        sb.AppendLine("skinparam linetype ortho");


        foreach (var edge in allEdges)
            sb.AppendLine(edge);

        sb.AppendLine("@enduml");

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}