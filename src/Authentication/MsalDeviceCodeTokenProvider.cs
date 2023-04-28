// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalDeviceCodeTokenProvider : ITokenProvider
{
    private readonly IPublicClientApplication app;
    private readonly ILogger logger;

    public MsalDeviceCodeTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "MSAL Device Code";

    public bool IsInteractive => true;

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        return tokenRequest.IsInteractive;
    }

    public async Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(tokenRequest.InteractiveTimeout);

        try
        {
            var result = await app.AcquireTokenWithDeviceCode(MsalConstants.AzureDevOpsScopes, tokenRequest.DeviceCodeResultCallback ?? ((DeviceCodeResult deviceCodeResult) =>
                {
                    logger.LogInformation(deviceCodeResult.Message);

                    return Task.CompletedTask;
                }))
                .ExecuteAsync(cts.Token);

            return result;
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            logger.LogWarning(ex.Message);
            return null;
        }
    }
}
