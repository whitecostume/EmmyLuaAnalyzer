﻿using EmmyLua.CodeAnalysis.Compilation.Type;
using EmmyLua.CodeAnalysis.Syntax.Node.SyntaxNodes;

namespace EmmyLua.CodeAnalysis.Compilation.Infer;

public static class GenericInfer
{
    public static void InferInstantiateByExpr(
        LuaType type,
        LuaExprSyntax expr,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        var exprType = context.Infer(expr);
        InferInstantiateByType(type, exprType, genericParameter, result, context);
    }

    public static void InferInstantiateByVarargTypeAndExprs(
        LuaGenericVarargType genericVarargType,
        IEnumerable<LuaExprSyntax> expr,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context
    )
    {
        InferInstantiateByVarargTypeAndTypes(genericVarargType, expr.Select(context.Infer), genericParameter, result,
            context);
    }

    public static void InferInstantiateByVarargTypeAndTypes(
        LuaGenericVarargType genericVarargType,
        IEnumerable<LuaType> types,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context
    )
    {
        result.TryAdd(genericVarargType.Name, new LuaMultiReturnType(types.ToList()));
    }

    public static void InferInstantiateByType(
        LuaType type,
        LuaType exprType,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        switch (type)
        {
            case LuaGenericType genericType:
            {
                GenericInstantiateByType(genericType, exprType, genericParameter, result, context);
                break;
            }
            case LuaNamedType namedType:
            {
                NamedTypeInstantiateByType(namedType, exprType, genericParameter, result, context);
                break;
            }
            case LuaArrayType arrayType:
            {
                ArrayTypeInstantiateByType(arrayType, exprType, genericParameter, result, context);
                break;
            }
            case LuaMethodType methodType:
            {
                MethodTypeInstantiateByType(methodType, exprType, genericParameter, result, context);
                break;
            }
            case LuaUnionType unionType:
            {
                UnionTypeInstantiateByType(unionType, exprType, genericParameter, result, context);
                break;
            }
            case LuaTupleType tupleType:
            {
                TupleTypeInstantiateByType(tupleType, exprType, genericParameter, result, context);
                break;
            }
        }
    }

    private static bool IsGenericParameter(string name, HashSet<string> genericParameter)
    {
        return genericParameter.Contains(name);
    }

    private static void GenericInstantiateByType(
        LuaGenericType genericType,
        LuaType exprType,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        if (exprType is LuaGenericType genericType2)
        {
            if (genericType2.Name == genericType.Name)
            {
                var genericArgs1 = genericType.GenericArgs;
                var genericArgs2 = genericType2.GenericArgs;

                for (int i = 0; i < genericArgs1.Count && i < genericArgs2.Count; i++)
                {
                    InferInstantiateByType(genericArgs1[i], genericArgs2[i], genericParameter, result, context);
                }
            }
        }
        else if (exprType is LuaTableLiteralType tableType)
        {
            if (IsGenericParameter(genericType.Name, genericParameter))
            {
                result.TryAdd(genericType.Name, Builtin.Table);
            }

            var tableExpr = tableType.TableExprPtr.ToNode(context);
            if (tableExpr is not null)
            {
                GenericTableExprInstantiate(genericType, tableExpr, genericParameter, result, context);
            }
        }
    }

    private static void GenericTableExprInstantiate(
        LuaGenericType genericType,
        LuaTableExprSyntax tableExpr,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        var genericArgs = genericType.GenericArgs;
        if (genericArgs.Count != 2)
        {
            return;
        }

        LuaType keyType = Builtin.Unknown;
        LuaType valueType = Builtin.Unknown;

        foreach (var fieldSyntax in tableExpr.FieldList)
        {
            if (fieldSyntax.IsValue)
            {
                keyType = keyType.Union(Builtin.Integer);
            }
            else if (fieldSyntax.IsStringKey || fieldSyntax.IsNameKey)
            {
                keyType = keyType.Union(Builtin.String);
            }

            var fieldValueType = context.Infer(fieldSyntax.Value);
            valueType = valueType.Union(fieldValueType);
        }

        InferInstantiateByType(genericArgs[0], keyType, genericParameter, result, context);
        InferInstantiateByType(genericArgs[1], valueType, genericParameter, result, context);
    }

    private static void NamedTypeInstantiateByType(
        LuaNamedType namedType,
        LuaType exprType,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        if (IsGenericParameter(namedType.Name, genericParameter))
        {
            result.TryAdd(namedType.Name, exprType);
        }
    }

    private static void ArrayTypeInstantiateByType(
        LuaArrayType arrayType,
        LuaType exprType,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        if (exprType is LuaArrayType arrayType2)
        {
            InferInstantiateByType(arrayType.BaseType, arrayType2.BaseType, genericParameter, result, context);
        }
        else if (exprType is LuaTableLiteralType tableLiteralType)
        {
            var tableExpr = tableLiteralType.TableExprPtr.ToNode(context);
            if (tableExpr is not null)
            {
                LuaType valueType = Builtin.Unknown;

                foreach (var field in tableExpr.FieldList)
                {
                    if (field.IsValue)
                    {
                        var fieldValueType = context.Infer(field.Value);
                        valueType = valueType.Union(fieldValueType);
                    }
                }

                InferInstantiateByType(arrayType.BaseType, valueType, genericParameter, result, context);
            }
        }
    }

    private static void MethodTypeInstantiateByType(
        LuaMethodType methodType,
        LuaType exprType,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        if (exprType is LuaMethodType methodType2)
        {
            var mainSignature = methodType.MainSignature;
            var mainSignature2 = methodType2.MainSignature;
            for (int i = 0; i < mainSignature.Parameters.Count && i < mainSignature2.Parameters.Count; i++)
            {
                var parameter = mainSignature.Parameters[i];
                var parameter2 = mainSignature2.Parameters[i];
                if (parameter is { DeclarationType: { } type })
                {
                    var paramType = parameter2.DeclarationType ?? Builtin.Any;
                    InferInstantiateByType(type, paramType, genericParameter, result, context);
                }
            }

            if (mainSignature.ReturnType is { } returnType && mainSignature2.ReturnType is { } returnType2)
            {
                InferInstantiateByType(returnType, returnType2, genericParameter, result, context);
            }
        }
    }

    private static void UnionTypeInstantiateByType(
        LuaUnionType unionType,
        LuaType exprType,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        if (unionType.UnionTypes.Contains(Builtin.Nil))
        {
            var newType = unionType.Remove(Builtin.Nil);
            InferInstantiateByType(newType, exprType, genericParameter, result, context);
        }

        foreach (var luaType in unionType.UnionTypes)
        {
            InferInstantiateByType(luaType, exprType, genericParameter, result, context);
            if (genericParameter.Count == result.Count)
            {
                break;
            }
        }
    }

    private static void TupleTypeInstantiateByType(
        LuaTupleType tupleType,
        LuaType exprType,
        HashSet<string> genericParameter,
        Dictionary<string, LuaType> result,
        SearchContext context)
    {
        if (exprType is LuaTupleType tupleType2)
        {
            for (var i = 0; i < tupleType.TupleDeclaration.Count && i < tupleType2.TupleDeclaration.Count; i++)
            {
                var leftElementType = tupleType.TupleDeclaration[i].DeclarationType!;
                if (leftElementType is LuaGenericVarargType genericVarargType)
                {
                    var rightExprs = tupleType2.TupleDeclaration[i..]
                        .Where(it => it.DeclarationType is not null)
                        .Select(it => it.DeclarationType!);
                    InferInstantiateByVarargTypeAndTypes(genericVarargType, rightExprs, genericParameter, result,
                        context);
                }
                else
                {
                    var rightElementType = tupleType2.TupleDeclaration[i].DeclarationType!;
                    InferInstantiateByType(leftElementType, rightElementType, genericParameter, result, context);
                }
            }
        }
        else if (exprType is LuaTableLiteralType tableLiteralType)
        {
            var tableExpr = tableLiteralType.TableExprPtr.ToNode(context);
            if (tableExpr is not null)
            {
                var fileList = tableExpr.FieldList.ToList();
                for (var i = 0; i < fileList.Count && i < tupleType.TupleDeclaration.Count; i++)
                {
                    var tupleElementType = tupleType.TupleDeclaration[i].DeclarationType!;
                    if (tupleElementType is LuaGenericVarargType genericVarargType)
                    {
                        var fileExprs = fileList[i..]
                            .Where(it => it is { IsValue: true, Value: not null })
                            .Select(it => it.Value!);
                        InferInstantiateByVarargTypeAndExprs(genericVarargType, fileExprs, genericParameter, result,
                            context);
                        break;
                    }
                    else
                    {
                        var field = fileList[i];
                        if (field is { IsValue: true, Value: { } valueExpr })
                        {
                            InferInstantiateByExpr(
                                tupleElementType,
                                valueExpr,
                                genericParameter,
                                result, context);
                        }
                    }
                }
            }
        }
    }
}
