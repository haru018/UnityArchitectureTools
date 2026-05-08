using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class Program
{
    static void Main()
    {
        // サンプルのソースコード
        string sourceCode = @"
            namespace MyApp.Domain
            {
                public class Status { }

                public class Weapon { 
                    public Status WeaponStatus { get; set; }
                }

                public class Player {
                    private Weapon _equippedWeapon;
                    public void Equip(Weapon w) { }
                }

                public class GameManager {
                    public Player MainPlayer { get; set; }
                }
            }
        ";

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("MyCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);


        var allClassNodes = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>();

        var allClassSymbols = allClassNodes
            .Select(node => semanticModel.GetDeclaredSymbol(node))
            .OfType<INamedTypeSymbol>()
            .ToList();

        var allEdges = new List<(INamedTypeSymbol From, INamedTypeSymbol To)>();

        foreach (var classSymbol in allClassSymbols)
        {
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

                if (targetType is not INamedTypeSymbol namedTargetType) continue;

                if (namedTargetType.Locations.Any(l => l.IsInSource))
                    allEdges.Add((classSymbol, namedTargetType));
            }
        }

        var playerSymbol = allClassSymbols.FirstOrDefault(c => c.Name == "Player");

        if (playerSymbol == null)
        {
            Console.WriteLine("Playerクラスのシンボルが見つかりませんでした。");
            return;
        }

        var queue = new Queue<INamedTypeSymbol>();
        queue.Enqueue(playerSymbol);
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var printedEdges = new HashSet<string>();

        while (queue.Count > 0)
        {
            var currentSymbol = queue.Dequeue();

            if (!visited.Add(currentSymbol))
                continue;

            var outgoingEdges = allEdges
                .Where(e => SymbolEqualityComparer.Default.Equals(e.From, currentSymbol));
            foreach (var (From, To) in outgoingEdges)
            {
                string edge = $"{From.Name} --> {To.Name}";

                if (!printedEdges.Add(edge)) continue;

                Console.WriteLine(edge);
                queue.Enqueue(To);
            }

            var incomingEdges = allEdges
                .Where(e => SymbolEqualityComparer.Default.Equals(e.To, currentSymbol));
            foreach (var (From, To) in incomingEdges)
            {
                string edge = $"{From.Name} --> {To.Name}";

                if (!printedEdges.Add(edge)) continue;

                Console.WriteLine(edge);
                queue.Enqueue(From);
            }
        }
    }
}