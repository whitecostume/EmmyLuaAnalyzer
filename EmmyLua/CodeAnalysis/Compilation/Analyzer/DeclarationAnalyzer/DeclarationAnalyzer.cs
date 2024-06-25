﻿namespace EmmyLua.CodeAnalysis.Compilation.Analyzer.DeclarationAnalyzer;

public class DeclarationAnalyzer(LuaCompilation compilation) : LuaAnalyzer(compilation, "Declaration")
{
    public override void Analyze(AnalyzeContext analyzeContext)
    {
        foreach (var document in analyzeContext.LuaDocuments)
        {
            var builder = new DeclarationBuilder(document.Id, document.SyntaxTree, this, analyzeContext);
            var tree = builder.Build();
            if (tree is not null)
            {
                Compilation.Db.AddDeclarationTree(document.Id, tree);
            }
        }
    }
}
