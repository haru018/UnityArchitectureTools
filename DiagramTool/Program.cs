using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class Program
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

        var allTypes = new List<INamedTypeSymbol>();
        var allEdges = new HashSet<string>();

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeNodes = tree
                .GetRoot()
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>();

            foreach (var node in typeNodes)
            {
                if (semanticModel.GetDeclaredSymbol(node) is not INamedTypeSymbol typeSymbol) continue;

                // enumはスキップ
                if (typeSymbol.TypeKind == TypeKind.Enum) continue;

                // ネストされたクラスはスキップ
                if (typeSymbol.ContainingType != null) continue;

                // 「具象クラス」はスキップ
                bool isConcrete = !typeSymbol.IsAbstract && typeSymbol.TypeKind == TypeKind.Class;
                bool inheritsFromAbstract = typeSymbol.BaseType != null && typeSymbol.BaseType.IsAbstract;
                if (isConcrete && inheritsFromAbstract) continue;

                // LifetimeScopeはスキップ
                if (typeSymbol.BaseType != null && typeSymbol.BaseType.Name == "LifetimeScope") continue;

                allTypes.Add(typeSymbol);

                string sourceId = GetSafePumlId(typeSymbol);

                // 継承の抽出
                if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object && typeSymbol.BaseType.Locations.Any(l => l.IsInSource))
                    allEdges.Add($"{GetSafePumlId(typeSymbol.BaseType)} <|-- {sourceId}");

                //実装の抽出
                foreach (var iface in typeSymbol.Interfaces)
                    if (iface.Locations.Any(l => l.IsInSource))
                        allEdges.Add($"{GetSafePumlId(iface)} <|.. {sourceId}");

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member.IsImplicitlyDeclared) continue;

                    if (member is IFieldSymbol fieldSymbol)
                    {
                        foreach (var fType in ExtractValidTargets(fieldSymbol.Type))
                            allEdges.Add($"{sourceId} --> {GetSafePumlId(fType)}");
                    }
                    else if (member is IPropertySymbol propertySymbol)
                    {
                        foreach (var pType in ExtractValidTargets(propertySymbol.Type))
                            allEdges.Add($"{sourceId} --> {GetSafePumlId(pType)}");
                    }
                    else if (member is IMethodSymbol methodSymbol)
                    {
                        if (methodSymbol.MethodKind != MethodKind.Ordinary && methodSymbol.MethodKind != MethodKind.Constructor) continue;

                        // 戻り値の依存
                        foreach (var rType in ExtractValidTargets(methodSymbol.ReturnType))
                            allEdges.Add($"{sourceId} ..> {GetSafePumlId(rType)}");

                        // 引数の依存
                        foreach (var param in methodSymbol.Parameters)
                            foreach (var targetType in ExtractValidTargets(param.Type))
                                allEdges.Add($"{sourceId} ..> {GetSafePumlId(targetType)}");
                    }
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

        foreach (var typeSymbol in allTypes)
        {
            string typeKeyword = typeSymbol.TypeKind == TypeKind.Interface ? "interface" : "class";
            string pumlId = GetSafePumlId(typeSymbol);
            string displayName = typeSymbol.Name;

            if (IsInheritingUnityClass(typeSymbol, out string unityType))
                sb.AppendLine($"{typeKeyword} \"{displayName}\" as {pumlId} {unityType}");
            else
                sb.AppendLine($"{typeKeyword} \"{displayName}\" as {pumlId}");
        }

        sb.AppendLine();

        foreach (var edge in allEdges)
            sb.AppendLine(edge);

        sb.AppendLine("@enduml");

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    static string GetSafePumlId(INamedTypeSymbol symbol)
    {
        string? id = symbol.OriginalDefinition.ToString();
        if (id == null)
            return symbol.Name;

        //* ジェネリクス対処
        int genericIndex = id.IndexOf('<');
        if (genericIndex >= 0)
            id = id[..genericIndex];

        return id
            .Replace("<T>", "")
            .Replace("<TKey, TValue>", "")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace(" ", "");
    }

    // タプルやジェネリクスの中身を再帰的に分解し、有効な依存先をすべて抽出する
    static IEnumerable<INamedTypeSymbol> ExtractValidTargets(ITypeSymbol target)
    {
        if (target is not INamedTypeSymbol named) yield break;

        // タプルの場合は中身を再帰的に展開
        if (named.IsTupleType)
        {
            foreach (var element in named.TupleElements)
                foreach (var innerTarget in ExtractValidTargets(element.Type))
                    yield return innerTarget;
            yield break;
        }

        // ジェネリクスの場合は <T> の中身を展開する
        if (named.IsGenericType)
            foreach (var typeArg in named.TypeArguments)
                foreach (var innerTarget in ExtractValidTargets(typeArg))
                    yield return innerTarget;

        if (named.Locations.Any(l => l.IsInSource) &&
            named.TypeKind != TypeKind.Enum &&
            named.SpecialType != SpecialType.System_Void &&
            !named.IsAnonymousType &&
            named.ContainingType == null)
            yield return named;
    }

    static bool IsInheritingUnityClass(INamedTypeSymbol typeSymbol, out string unityBaseName)
    {
        unityBaseName = string.Empty;
        var currentBase = typeSymbol.BaseType;

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