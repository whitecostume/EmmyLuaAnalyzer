﻿using EmmyLua.LanguageServer.Server;
using EmmyLua.LanguageServer.Util;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EmmyLua.LanguageServer.SignatureHelper;

// ReSharper disable once ClassNeverInstantiated.Global
public class SignatureHelperHandler(ServerContext context) : SignatureHelpHandlerBase
{
    private SignatureHelperBuilder Builder { get; } = new();

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(SignatureHelpCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new()
        {
            TriggerCharacters = new[] { "(", "," },
        };
    }

    public override Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToUri().AbsoluteUri;
        SignatureHelp? signatureHelp = null;
        context.ReadyRead(() =>
        {
            var semanticModel = context.GetSemanticModel(uri);
            if (semanticModel is null)
            {
                return;
            }

            var position = request.Position;
            var triggerToken =
                semanticModel.Document.SyntaxTree.SyntaxRoot.TokenLeftBiasedAt(position.Line, position.Character);
            if (triggerToken is not null)
            {
                var config = context.SettingManager.GetSignatureConfig();
                signatureHelp = Builder.Build(semanticModel, triggerToken, request, config);
            }
        });

        return Task.FromResult<SignatureHelp?>(signatureHelp);
    }
}