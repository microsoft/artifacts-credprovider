using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.Util;

public static class FeedEndpointCredentialsUtil
{
    public static Dictionary<string, EndpointCredentials> ParseJsonToDictionary(ILogger logger)
    {
        string feedEndPointsJson = EnvUtil.GetFeedEndpointCredentials();
        if (string.IsNullOrWhiteSpace(feedEndPointsJson))
        {
            logger.Verbose(Resources.BuildTaskEndpointEnvVarError);
            return null;
        }

        try
        {
            // Parse JSON from VSS_NUGET_EXTERNAL_FEED_ENDPOINTS
            logger.Verbose(Resources.ParsingJson);
            if (!feedEndPointsJson.Contains("':"))
            {
                logger.Warning(Resources.InvalidJsonWarning);
            }
            Dictionary<string, EndpointCredentials> credsResult = new Dictionary<string, EndpointCredentials>(StringComparer.OrdinalIgnoreCase);
            EndpointCredentialsContainer endpointCredentials = JsonConvert.DeserializeObject<EndpointCredentialsContainer>(feedEndPointsJson);
            if (endpointCredentials == null)
            {
                logger.Verbose(Resources.NoEndpointsFound);
                return credsResult;
            }

            foreach (EndpointCredentials credentials in endpointCredentials.EndpointCredentials)
            {
                if (credentials == null)
                {
                    logger.Verbose(Resources.EndpointParseFailure);
                    break;
                }

                if (credentials.Username == null)
                {
                    credentials.Username = "VssSessionToken";
                }

                if (!Uri.TryCreate(credentials.Endpoint, UriKind.Absolute, out var endpointUri))
                {
                    logger.Verbose(Resources.EndpointParseFailure);
                    break;
                }

                var urlEncodedEndpoint = endpointUri.AbsoluteUri;
                if (!credsResult.ContainsKey(urlEncodedEndpoint))
                {
                    credsResult.Add(urlEncodedEndpoint, credentials);
                }
            }

            return credsResult;
        }
        catch (Exception e)
        {
            logger.Verbose(string.Format(Resources.VstsBuildTaskExternalCredentialCredentialProviderError, e));
            throw;
        }
    }
}
