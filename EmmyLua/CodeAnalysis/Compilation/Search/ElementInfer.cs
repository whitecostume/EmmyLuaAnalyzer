﻿using EmmyLua.CodeAnalysis.Compilation.Infer;
using EmmyLua.CodeAnalysis.Syntax.Node;
using EmmyLua.CodeAnalysis.Syntax.Node.SyntaxNodes;
using EmmyLua.CodeAnalysis.Type;

namespace EmmyLua.CodeAnalysis.Compilation.Search;

public class ElementInfer(SearchContext context)
{
    private Dictionary<SyntaxElementId, LuaType> InferCaches { get; } = new();

    private HashSet<SyntaxElementId> InferGuard { get; } = [];

    private const int MaxDepth = 1000;

    // 推断深度
    private int _currentDepth;

    public LuaType Infer(LuaSyntaxElement? element)
    {
        if (element is null)
        {
            return Builtin.Unknown;
        }

        if (context.Features.Cache)
        {
            if (InferCaches.TryGetValue(element.UniqueId, out var luaType))
            {
                return luaType;
            }

            luaType = InferCore(element);
            if (context.Features.CacheUnknown || !luaType.IsSameType(Builtin.Unknown, context))
            {
                InferCaches[element.UniqueId] = luaType;
            }

            return luaType;
        }

        return InferCore(element);
    }

    private LuaType InferCore(LuaSyntaxElement element)
    {
        if (_currentDepth > MaxDepth)
        {
            return Builtin.Unknown;
        }

        if (!InferGuard.Add(element.UniqueId))
        {
            return Builtin.Unknown;
        }

        try
        {
            _currentDepth++;
            
            // First check if we have a stored type from @type annotation
            var storedType = context.Compilation.TypeManager.FindTypeInfo(element.UniqueId)?.BaseType;
            if (storedType != null && !storedType.IsSameType(Builtin.Unknown, context))
            {
                return storedType;
            }

            return element switch
            {
                LuaExprSyntax expr => ExpressionInfer.InferExpr(expr, context),
                LuaLocalNameSyntax localName => DeclarationInfer.InferLocalName(localName, context),
                LuaParamDefSyntax paramDef => DeclarationInfer.InferParam(paramDef, context),
                LuaSourceSyntax source => DeclarationInfer.InferSource(source, context),
                LuaDocTypeSyntax ty => TypeInfer.InferType(ty, context),
                _ => Builtin.Unknown
            };
        }
        finally
        {
            _currentDepth--;
            InferGuard.Remove(element.UniqueId);
        }
    }

}
