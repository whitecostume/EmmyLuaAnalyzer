﻿using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.InlineValue;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using EmmyLua.LanguageServer.Server;

namespace EmmyLua.LanguageServer.InlineValues;

// ReSharper disable once ClassNeverInstantiated.Global
public class InlineValuesHandler(ServerContext context): InlineValueHandlerBase
{
    private InlineValuesBuilder Builder { get; } = new();
    
    protected override Task<InlineValueResponse> Handle(InlineValueParams inlineValueParams, CancellationToken cancellationToken)
    {
        var uri = inlineValueParams.TextDocument.Uri.UnescapeUri;
        InlineValueResponse? container = null;
        context.ReadyRead(() =>
        {
            var semanticModel = context.GetSemanticModel(uri);
            if (semanticModel is not null)
            {
                var range = inlineValueParams.Range;
                var stopRangePosition = inlineValueParams.Context.StoppedLocation.End;
                var validRange = range with { End = stopRangePosition };
                
                var result =  Builder.Build(semanticModel, validRange);
                container = new InlineValueResponse(result);
            }
        });
        
        return Task.FromResult(container)!;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities)
    {
        serverCapabilities.InlineValueProvider = true;
    }
}