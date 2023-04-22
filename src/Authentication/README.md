## Azure Artifacts authentication library for credential providers

The Azure Artifacts credential provider authentication library provides extension methods and defaults for using the Microsoft Authentication Library (MSAL) for Azure Artifacts.

## Examples

### Basic usage

Example usage to create a PublicClientApplication with recommended settings and defaults for Azure Artifacts and enumerate providers:

```csharp
var app = AzureArtifacts.CreateDefaultBuilder(authority)
    .WithBroker(true, logger)
    .WithLogging((LogLevel level, string message, bool containsPii) =>
    {
        // Application specific logging
    })
    .Build();

// Can use MsalTokenProviders which works for most cases, or compose the token providers manually
var providers = MsalTokenProviders.Get(app, logger);

var tokenRequest = new TokenRequest("https://pkgs.dev.azure.com/org")
{
    IsInteractive = true
};

foreach (var provider in providers)
{
    if (!provider.CanGetToken(tokenRequest))
        continue;

    var result = await provider.GetTokenAsync(tokenRequest);
}
```

### Token cache

The MSAL cache must be initialized for the WAM Broker to return cached accounts, and for non-Windows the token cache ensures users are not prompted when the session token expires.

```csharp
var cacheLocation = MsalCache.DefaultMsalCacheLocation;
var cache = await MsalCache.GetMsalCacheHelperAsync(cacheLocation, logger);

var app = AzureArtifacts.CreateDefaultBuilder(authority, logger).Build();

cache.RegisterCache(app.UserTokenCache);
```
