// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IMsalTokenProvider
    {
        Task<IMsalToken> AcquireTokenSilentlyAsync(CancellationToken cancellationToken);

        Task<IMsalToken> AcquireTokenWithDeviceFlowAsync(Func<DeviceCodeResult, Task> deviceCodeHandler, CancellationToken cancellationToken, ILogger logging);

        Task<IMsalToken> AcquireTokenWithUI(CancellationToken cancellationToken, ILogger logging);

        Task<IMsalToken> AcquireTokenWithWindowsIntegratedAuth(CancellationToken cancellationToken);
    }

    public interface IMsalToken
    {
        string AccessTokenType { get; }

        string AccessToken { get; }
    }

    public class MsalToken : IMsalToken
    {
        public MsalToken(string accessToken)
        {
            this.AccessToken = accessToken;
        }

        public MsalToken(AuthenticationResult authenticationResult)
            : this(authenticationResult.AccessToken)
        {
        }

        public string AccessTokenType => "Bearer";

        public string AccessToken { get; }
    }
}
