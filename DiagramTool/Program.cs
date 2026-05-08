using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class Program
{
    static void Main()
    {
        // 複数のクラスが絡み合うソースコード
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
            }
        ";

        // 1. 構文木を作成
        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);

        // 2. コンパイル空間（Compilation）を作成し、意味モデル（Semantic Model）を取得！
        var compilation = CSharpCompilation.Create("MyCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)) // C#の基本型を認識させる
            .AddSyntaxTrees(tree);

        var semanticModel = compilation.GetSemanticModel(tree);

        // 3. 対象のクラス（今回はPlayer）の構文木を探す
        var root = tree.GetRoot();
        var playerClassNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "Player");

        // 4. 構文木から「シンボル（型の意味情報）」に変換
        var playerSymbol = semanticModel.GetDeclaredSymbol(playerClassNode);

        if (playerSymbol == null)
        {
            Console.WriteLine("Playerクラスのシンボルが見つかりませんでした。");
            return;
        }

        Console.WriteLine($"=== [{playerSymbol.Name}] の依存先を探索 ===");

        var queue = new Queue<INamedTypeSymbol>();
        queue.Enqueue(playerSymbol);
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var printedEdges = new HashSet<string>();

        // 5. 依存先を抽出（今回はシンプルにフィールドとプロパティの型だけを見る）
        while (queue.Count > 0)
        {
            var currentSymbol = queue.Dequeue();

            if (visited.Contains(currentSymbol))
                continue;
            visited.Add(currentSymbol);

            foreach (var member in currentSymbol.GetMembers())
            {
                if (member.IsImplicitlyDeclared) continue;

                ITypeSymbol targetType;
                if (member is IFieldSymbol fieldSymbol)
                    targetType = fieldSymbol.Type;
                else if (member is IPropertySymbol propertySymbol)
                    targetType = propertySymbol.Type;
                else
                    continue;

                if (targetType == null || targetType is not INamedTypeSymbol namedTargetType) continue;

                // 【解決策2】その型が「解析対象のソースコード内（IsInSource）」で定義されているかチェック
                // これにより、System.Int32(int) や System.String などの外部型が弾かれる
                if (namedTargetType.Locations.Any(l => l.IsInSource))
                {
                    string edge = $"{currentSymbol.Name} --> {namedTargetType.Name}";

                    if (!printedEdges.Add(edge)) continue;

                    Console.WriteLine(edge);
                    queue.Enqueue(namedTargetType);

                }

            }
        }
    }
}