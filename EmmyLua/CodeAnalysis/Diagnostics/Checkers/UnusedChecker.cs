﻿using EmmyLua.CodeAnalysis.Compilation;
using EmmyLua.CodeAnalysis.Compilation.Declaration;
using EmmyLua.CodeAnalysis.Compilation.Semantic;
using EmmyLua.CodeAnalysis.Syntax.Node.SyntaxNodes;

namespace EmmyLua.CodeAnalysis.Diagnostics.Checkers;

public class UnusedChecker(LuaCompilation compilation) : DiagnosticCheckerBase(compilation, [DiagnosticCode.Unused])
{
    public override void Check(DiagnosticContext context)
    {
        var semanticModel = Compilation.GetSemanticModel(context.Document.Id);
        if (semanticModel is null)
        {
            return;
        }

        var declarations = semanticModel.DeclarationTree.RootScope?.Descendants;
        if (declarations is null)
        {
            return;
        }

        var localOrParamDeclarations = new HashSet<LuaDeclaration>();
        foreach (var luaDeclaration in declarations)
        {
            if (luaDeclaration.Info is LocalInfo or ParamInfo)
            {
                localOrParamDeclarations.Add(luaDeclaration);
            }
        }

        LocalOrParamUnusedCheck(localOrParamDeclarations, semanticModel, context);
    }

    private void LocalOrParamUnusedCheck(
        HashSet<LuaDeclaration> declarationSet,
        SemanticModel semanticModel,
        DiagnosticContext context)
    {
        var nameExprs = semanticModel
            .Document
            .SyntaxTree
            .SyntaxRoot
            .Descendants
            .OfType<LuaNameExprSyntax>();

        foreach (var nameExpr in nameExprs)
        {
            if (semanticModel.DeclarationTree.FindDeclaration(nameExpr, semanticModel.Context) is { } declaration)
            {
                declarationSet.Remove(declaration);
            }
        }

        foreach (var luaDeclaration in declarationSet)
        {
            if (luaDeclaration.Info.Ptr.ToNode(context.Document) is { } node)
            {
                context.Report(new Diagnostic(
                        DiagnosticSeverity.Hint,
                        DiagnosticCode.Unused,
                        "unused variable",
                        node.Range,
                    DiagnosticTag.Unnecessary
                ));
            }
        }
    }
}
