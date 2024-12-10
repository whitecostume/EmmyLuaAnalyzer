using EmmyLua.CodeAnalysis.Compilation.Search;
using EmmyLua.CodeAnalysis.Syntax.Node.SyntaxNodes;
using EmmyLua.CodeAnalysis.Type;

namespace EmmyLua.CodeAnalysis.Compilation.Infer;

public static class DeclarationInfer
{
    public static LuaType InferLocalName(LuaLocalNameSyntax localName, SearchContext context)
    {
        // First check for stored type from @type annotation
        var storedType = context.Compilation.TypeManager.FindTypeInfo(localName.UniqueId)?.BaseType;
        if (storedType != null && !storedType.IsSameType(Builtin.Unknown, context))
        {
            return storedType;
        }

        var symbol = context.FindDeclaration(localName);
        return symbol?.Type ?? Builtin.Unknown;
    }

    public static LuaType InferSource(LuaSourceSyntax source, SearchContext context)
    {
        return context.Compilation.Db.QueryModuleType(source.DocumentId) ?? Builtin.Unknown;
    }

    public static LuaType InferParam(LuaParamDefSyntax paramDef, SearchContext context)
    {
        // First check for stored type from @type annotation
        var storedType = context.Compilation.TypeManager.FindTypeInfo(paramDef.UniqueId)?.BaseType;
        if (storedType != null && !storedType.IsSameType(Builtin.Unknown, context))
        {
            return storedType;
        }

        var symbol = context.FindDeclaration(paramDef);
        return symbol?.Type ?? Builtin.Unknown;
    }
}
