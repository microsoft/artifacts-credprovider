// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IAdalTokenProvider
    {
        Task<IAdalToken> AcquireTokenSilentlyAsync(CancellationToken cancellationToken);

        Task<IAdalToken> AcquireTokenWithDeviceFlowAsync(Func<DeviceCodeResult, Task> deviceCodeHandler, CancellationToken cancellationToken);

        Task<IAdalToken> AcquireTokenWithUI(CancellationToken cancellationToken);
    }

    public interface IAdalToken
    {
        string AccessTokenType { get; }

        string AccessToken { get; }
    }

    public class AdalToken : IAdalToken
    {
        public AdalToken(string accessTokenType, string accessToken)
        {
            this.AccessTokenType = accessTokenType;
            this.AccessToken = accessToken;
        }

        public AdalToken(AuthenticationResult authenticationResult)
            : this(authenticationResult.AccessTokenType, authenticationResult.AccessToken)
        {
        }

        public string AccessTokenType { get; }

        public string AccessToken { get; }
    }
}
