﻿using EmmyLua.LanguageServer.Completion.CompleteProvider;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Completion;

namespace EmmyLua.LanguageServer.Completion;

public class CompletionBuilder
{
    private List<ICompleteProviderBase> Providers { get; } = [
        new RequireProvider(),
        new ResourcePathProvider(),
        new AliasAndEnumProvider(),
        new TableFieldProvider(),
        new LocalEnvProvider(),
        new GlobalProvider(),
        new KeywordsProvider(),
        new MemberProvider(),
        new ModuleProvider(),
        new DocProvider(),
        new SelfMemberProvider(),
        new PostfixProvider()
    ];
    
    public List<CompletionItem> Build(CompleteContext completeContext)
    {
        try
        {
            foreach (var provider in Providers)
            {
                provider.AddCompletion(completeContext);
                if (!completeContext.Continue)
                {
                    break;
                }
            }
            return completeContext.CompletionItems.ToList();
        }
        catch (OperationCanceledException)
        {
            return new();
        }
    }
}