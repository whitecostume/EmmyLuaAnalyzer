﻿using EmmyLua.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EmmyLua.LanguageServer.Completion;

// ReSharper disable once ClassNeverInstantiated.Global
public class CompletionHandler(ServerContext context) : CompletionHandlerBase
{
    private CompletionBuilder Builder { get; } = new();

    private CompletionDocumentResolver DocumentResolver { get; } = new();

    protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new()
        {
            ResolveProvider = true,
            TriggerCharacters = new List<string> { ".", ":", "(", "[", "\"", "\'", ",", "@", "\\", "/" },
            CompletionItem = new()
            {
                LabelDetailsSupport = true
            }
        };
    }

    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToUri().AbsoluteUri;
        CompletionList container = new();
        context.ReadyRead(() =>
        {
            var semanticModel = context.GetSemanticModel(uri);
            if (semanticModel is not null)
            {
                var completeContext = new CompleteContext(semanticModel, request.Position, cancellationToken, context);
                var completions = Builder.Build(completeContext);
                container = CompletionList.From(completions);
            }
        });

        return Task.FromResult(container);
    }

    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
    {
        var item = request;
        context.ReadyRead(() =>
        {
            item = DocumentResolver.Resolve(request, context);
        });
        
        return Task.FromResult(item);
    }
}