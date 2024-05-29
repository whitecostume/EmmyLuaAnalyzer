﻿using EmmyLua.CodeAnalysis.Compilation.Type;
using EmmyLua.CodeAnalysis.Syntax.Node.SyntaxNodes;

namespace EmmyLua.CodeAnalysis.Compilation.Infer;

public static class DeclarationInfer
{

    public static LuaType InferLocalName(LuaLocalNameSyntax localName, SearchContext context)
    {
        var symbol = context.FindDeclaration(localName);
        return symbol?.Info.DeclarationType ?? Builtin.Unknown;
    }

    public static LuaType InferSource(LuaSourceSyntax source, SearchContext context)
    {
        return context.Compilation.Db.GetModuleExportType(source.DocumentId) ?? Builtin.Unknown;
    }

    public static LuaType InferParam(LuaParamDefSyntax paramDef, SearchContext context)
    {
        var symbol = context.FindDeclaration(paramDef);
        return symbol?.Info.DeclarationType ?? Builtin.Unknown;
    }
}
