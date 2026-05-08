using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NamespaceCheckerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RULE001";

    // ルールの定義
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "指定ネームスペースの使用検知",
        messageFormat: "ネームスペース '{0}' が使用されています. 設計を見直してください.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    // いつ解析を走らせるか
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // using Hoge;と書くたびに, AnalyzeUsing メソッドを呼び出す
        context.RegisterSyntaxNodeAction(AnalyzeUsing, SyntaxKind.UsingDirective);
    }

    // 実際の処理
    private void AnalyzeUsing(SyntaxNodeAnalysisContext context)
    {
        // 構文木から「using句」のデータを取得
        var usingNode = (UsingDirectiveSyntax)context.Node;

        // usingしているネームスペースの名前を文字列として取得
        var namespaceName = usingNode.Name?.ToString();

        // 例：「UnityEngine」だったら警告を出す！
        if (namespaceName == "UnityEngine")
        {
            // 波線を引く場所（usingNodeの位置）と、メッセージに埋め込む名前を渡す
            var diagnostic = Diagnostic.Create(Rule, usingNode.GetLocation(), namespaceName);
            context.ReportDiagnostic(diagnostic);
        }
    }
}