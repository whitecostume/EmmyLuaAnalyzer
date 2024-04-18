﻿using EmmyLua.CodeAnalysis.Compilation.Infer;
using EmmyLua.CodeAnalysis.Compilation.Type;
using EmmyLua.CodeAnalysis.Syntax.Node.SyntaxNodes;

namespace EmmyLua.CodeAnalysis.Compilation.Analyzer.ResolveAnalyzer;

public class ResolveAnalyzer(LuaCompilation compilation) : LuaAnalyzer(compilation, "Resolve")
{
    private SearchContext Context { get; } = new(compilation, true, false);

    public override void Analyze(AnalyzeContext analyzeContext)
    {
        var resolveDependencyGraph = new ResolveDependencyGraph(Context);
        resolveDependencyGraph.Build(analyzeContext.UnResolves);
        do
        {
            foreach (var canResolved in resolveDependencyGraph.CanResolvedList)
            {
                var unResolved = canResolved.UnResolved;
                var state = canResolved.State;
                switch (state)
                {
                    case ResolveState.UnResolvedType:
                    {
                        ResolveType(unResolved);
                        break;
                    }
                    case ResolveState.UnResolvedIndex:
                    {
                        ResolveIndex(unResolved);
                        break;
                    }
                    case ResolveState.UnResolveReturn:
                    {
                        ResolveReturn(unResolved);
                        break;
                    }
                    case ResolveState.UnResolvedParameters:
                    {
                        ResolveParameters(unResolved);
                        break;
                    }
                }
            }
        } while (resolveDependencyGraph.CalcDependency());

        foreach (var unResolve in resolveDependencyGraph.UnResolvedList)
        {
            var unResolved = unResolve.UnResolved;
            var state = unResolve.State;
            switch (state)
            {
                case ResolveState.UnResolvedType:
                {
                    FinalResolveType(unResolved);
                    break;
                }
            }
        }

        Context.ClearCache();
    }

    // private void ResolveStep1(List<UnResolved> unResolvedList)
    // {
    //     bool changed;
    //     var resolvedCount = 0;
    //     do
    //     {
    //         changed = false;
    //         for (var i = 0; i < unResolvedList.Count - resolvedCount;)
    //         {
    //             var unResolved = unResolvedList[i];
    //             if ((unResolved.ResolvedState & ResolveState.UnResolvedType) != 0)
    //             {
    //                 ResolveType(unResolved, ref changed);
    //             }
    //             else if ((unResolved.ResolvedState & ResolveState.UnResolvedIndex) != 0)
    //             {
    //                 ResolveIndex(unResolved, ref changed);
    //             }
    //             else if ((unResolved.ResolvedState & ResolveState.UnResolveReturn) != 0)
    //             {
    //                 ResolveReturn(unResolved, ref changed);
    //             }
    //
    //             if (unResolved.ResolvedState == ResolveState.Resolved)
    //             {
    //                 if (i < unResolvedList.Count - resolvedCount - 1)
    //                 {
    //                     // Move the resolved object to the end of the list
    //                     (unResolvedList[i], unResolvedList[unResolvedList.Count - resolvedCount - 1]) =
    //                         (unResolvedList[unResolvedList.Count - resolvedCount - 1], unResolvedList[i]);
    //                 }
    //
    //                 resolvedCount++;
    //             }
    //             else
    //             {
    //                 i++;
    //             }
    //         }
    //     } while (changed);
    // }

    private void ResolveType(UnResolved unResolved)
    {
        if (unResolved is UnResolvedDeclaration unResolvedDeclaration)
        {
            var exprRef = unResolvedDeclaration.ExprRef;
            if (exprRef is not null)
            {
                var exprType = Context.Infer(exprRef.Expr);
                if (!exprType.Equals(Builtin.Unknown))
                {
                    MergeType(unResolvedDeclaration, exprType, exprRef.RetId);
                }
            }
        }
        else if (unResolved is UnResolvedForRangeParameter unResolvedForRangeParameter)
        {
            var exprList = unResolvedForRangeParameter.ExprList;
            switch (exprList.Count)
            {
                case 0:
                {
                    return;
                }
                // ipairs and pairs
                case 1:
                {
                    var iterExpr = exprList.First();
                    var iterType = Context.Infer(iterExpr);
                    if (iterType is LuaMethodType methodType)
                    {
                        var returnType = methodType.MainSignature.ReturnType;
                        var returnTypes = new List<LuaType>();
                        if (returnType is LuaMultiReturnType multiReturnType)
                        {
                            returnTypes.AddRange(multiReturnType.RetTypes);
                        }
                        else
                        {
                            returnTypes.Add(returnType);
                        }

                        for (var i = 0; i < unResolvedForRangeParameter.ParameterLuaDeclarations.Count; i++)
                        {
                            var parameter = unResolvedForRangeParameter.ParameterLuaDeclarations[i];
                            if (parameter.DeclarationType is null)
                            {
                                parameter.DeclarationType = i < returnTypes.Count ? returnTypes[i] : Builtin.Unknown;
                            }
                        }
                    }

                    return;
                }
                // custom iterator
                // default:
                // {
                //     // TODO: implement custom iterator
                //     break;
                // }
            }
        }
    }

    private void FinalResolveType(UnResolved unResolved)
    {
        if (unResolved is UnResolvedDeclaration unResolvedDeclaration)
        {
            var declaration = unResolvedDeclaration.LuaDeclaration;
            declaration.DeclarationType = new LuaNamedType(declaration.Ptr.Stringify);
        }
    }

    private void ResolveIndex(UnResolved unResolved)
    {
        if (unResolved is UnResolvedDeclaration unResolvedDeclaration)
        {
            var declaration = unResolvedDeclaration.LuaDeclaration;
            if (declaration.Ptr.ToNode(Context) is LuaIndexExprSyntax { PrefixExpr: { } prefixExpr } indexExpr)
            {
                var documentId = indexExpr.DocumentId;
                var ty = Context.Infer(prefixExpr);
                if (ty is LuaNamedType namedType)
                {
                    Compilation.ProjectIndex.AddMember(documentId, namedType.Name, declaration);
                }
            }
        }
    }

    private void ResolveReturn(UnResolved unResolved)
    {
        if (unResolved is UnResolvedMethod unResolvedMethod)
        {
            var methodType = unResolvedMethod.MethodType;
            if (!methodType.MainSignature.ReturnType.Equals(Builtin.Unknown))
            {
                return;
            }

            var block = unResolvedMethod.Block;
            var returnType = AnalyzeBlockReturns(block);
            methodType.MainSignature.ReturnType = returnType;
        }
        else if (unResolved is UnResolvedSource unResolvedSource)
        {
            var block = unResolvedSource.Block;
            var returnType = AnalyzeBlockReturns(block);
            if (returnType is LuaMultiReturnType multiReturnType)
            {
                var mainReturn = multiReturnType.RetTypes.ElementAtOrDefault(0);
                if (mainReturn is not null)
                {
                    returnType = mainReturn;
                }
            }

            Compilation.ProjectIndex.AddExportType(unResolvedSource.DocumentId, returnType);
        }
    }

    private void ResolveParameters(UnResolved unResolved)
    {
        if (unResolved is UnResolvedClosureParameters unResolvedClosureParameters)
        {
            var callExpr = unResolvedClosureParameters.CallExprSyntax;
            var prefixType = Context.Infer(callExpr.PrefixExpr);
            var callArgList = callExpr.ArgList?.ArgList.ToList() ?? [];
            TypeHelper.Each<LuaMethodType>(prefixType, type =>
            {
                var signature = type.FindPerfectMatchSignature(callExpr, callArgList, Context);
                var paramIndex = unResolvedClosureParameters.Index;
                if (paramIndex == -1) return;
                var paramDeclaration = signature.Parameters.ElementAtOrDefault(paramIndex);
                if (paramDeclaration is not { DeclarationType: { } paramType }) return;
                var closureParams = unResolvedClosureParameters.ParameterLuaDeclarations;
                TypeHelper.Each<LuaMethodType>(paramType, methodType =>
                {
                    var mainParams = methodType.MainSignature.Parameters;
                    for (var i = 0; i < closureParams.Count && i < mainParams.Count; i++)
                    {
                        var closureParam = closureParams[i];
                        var mainParam = mainParams[i];
                        closureParam.DeclarationType ??= mainParam.DeclarationType;
                    }
                });
            });
        }
    }

    private LuaType AnalyzeBlockReturns(LuaBlockSyntax mainBlock)
    {
        LuaType returnType = Builtin.Unknown;
        var cfg = Context.Compilation.GetControlFlowGraph(mainBlock);
        if (cfg is null)
        {
            return returnType;
        }

        var prevNodes = cfg.GetPredecessors(cfg.ExitNode).ToList();
        foreach (var prevNode in prevNodes)
        {
            if (prevNode.Statements.Count != 0)
            {
                if (prevNode.Statements.Last().ToNode(Context) is LuaReturnStatSyntax returnStmt)
                {
                    var rets = returnStmt.ExprList.ToList();
                    switch (rets.Count)
                    {
                        case 0:
                        {
                            returnType = returnType.Union(Builtin.Nil);
                            break;
                        }
                        case 1:
                        {
                            var mainReturn = Context.Infer(rets[0]);
                            if (mainReturn.Equals(Builtin.Unknown))
                            {
                                return returnType;
                            }

                            returnType = returnType.Union(mainReturn);
                            break;
                        }
                        case > 1:
                        {
                            var retTypes = new List<LuaType>();
                            foreach (var ret in rets)
                            {
                                var retType = Context.Infer(ret);
                                if (retType.Equals(Builtin.Unknown))
                                {
                                    return returnType;
                                }

                                retTypes.Add(retType);
                            }

                            returnType = returnType.Union(new LuaMultiReturnType(retTypes));
                            break;
                        }
                    }
                }
                else
                {
                    returnType = returnType.Union(Builtin.Nil);
                }
            }
            else
            {
                returnType = returnType.Union(Builtin.Nil);
            }
        }

        return returnType;
    }

    private void MergeType(UnResolvedDeclaration unResolved, LuaType type, int retId)
    {
        if (type is LuaMultiReturnType returnType)
        {
            var childTy = returnType.RetTypes.ElementAtOrDefault(retId);
            if (childTy is not null)
            {
                type = childTy;
            }
        }

        var declaration = unResolved.LuaDeclaration;

        if (declaration.DeclarationType is null)
        {
            declaration.DeclarationType = type;
        }
        else if (unResolved.IsTypeDeclaration && TypeHelper.IsExtensionType(type))
        {
            var declarationType = unResolved.LuaDeclaration.DeclarationType;
            if (declarationType is LuaNamedType namedType)
            {
                if (type is LuaTableLiteralType tableType)
                {
                    var typeName = namedType.Name;
                    var members = Compilation.ProjectIndex.GetMembers(tableType.Name);
                    var documentId = declaration.Ptr.DocumentId;

                    foreach (var member in members)
                    {
                        Compilation.ProjectIndex.AddMember(documentId, typeName, member);
                    }

                    Compilation.ProjectIndex.AddRelatedType(documentId, tableType.Name, namedType);
                }
                else
                {
                    var documentId = declaration.Ptr.DocumentId;
                    Compilation.ProjectIndex.AddSuper(documentId, namedType.Name, type);
                }
            }
        }
    }
}
