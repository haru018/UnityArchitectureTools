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

        var namespaceDict = new Dictionary<string, List<INamedTypeSymbol>>();
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
                if (semanticModel.GetDeclaredSymbol(node) is not INamedTypeSymbol classSymbol) continue;

                // enumはスキップ
                if (classSymbol.TypeKind == TypeKind.Enum) continue;

                // 「具象クラス」はスキップ
                bool isConcrete = !classSymbol.IsAbstract && classSymbol.TypeKind == TypeKind.Class;
                bool inheritsFromAbstract = classSymbol.BaseType != null && classSymbol.BaseType.IsAbstract;
                if (isConcrete && inheritsFromAbstract) continue;

                // LifetimeScopeはスキップ
                if (classSymbol.BaseType != null && classSymbol.BaseType.Name == "LifetimeScope") continue;

                // namespaceの管理
                string nsName;
                if (classSymbol.ContainingNamespace.IsGlobalNamespace)
                    nsName = string.Empty;
                else
                    nsName = classSymbol.ContainingNamespace.ToString() ?? string.Empty;

                if (!namespaceDict.ContainsKey(nsName))
                    namespaceDict[nsName] = [];
                namespaceDict[nsName].Add(classSymbol);

                // 継承の抽出
                if (classSymbol.BaseType != null && classSymbol.BaseType.SpecialType != SpecialType.System_Object)
                    if (classSymbol.BaseType.Locations.Any(l => l.IsInSource))
                        allEdges.Add($"{classSymbol.BaseType.Name} <|-- {classSymbol.Name}");

                //実装の抽出
                foreach (var iface in classSymbol.Interfaces)
                    if (iface.Locations.Any(l => l.IsInSource))
                        allEdges.Add($"{iface.Name} <|.. {classSymbol.Name}");

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

                    if (targetType.TypeKind == TypeKind.Enum) continue;

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
        sb.AppendLine("set namespaceSeparator .");
        sb.AppendLine("");

        foreach (var kvp in namespaceDict)
        {
            string ns = kvp.Key;
            if (ns != string.Empty) sb.AppendLine($"namespace {ns} {{");

            foreach (var classSymbol in kvp.Value)
                if (IsInheritingUnityClass(classSymbol, out string unityType))
                    sb.AppendLine($"    class {classSymbol.Name} {unityType}");
                else
                    sb.AppendLine($"    class {classSymbol.Name}");

            if (ns != string.Empty) sb.AppendLine($"}}");

            sb.AppendLine();
        }

        foreach (var edge in allEdges)
            sb.AppendLine(edge);

        sb.AppendLine("@enduml");

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    static bool IsInheritingUnityClass(INamedTypeSymbol classSymbol, out string unityBaseName)
    {
        unityBaseName = string.Empty;
        var currentBase = classSymbol.BaseType;

        while (currentBase != null && currentBase.SpecialType != SpecialType.System_Object)
        {
            if (currentBase.Name == "MonoBehaviour" || currentBase.Name == "ScriptableObject")
            {
                unityBaseName = currentBase.Name;
                return true;
            }
            currentBase = currentBase.BaseType;
        }

        return false;
    }
}