using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.Util;

public class ExternalCredentialsConverter : Newtonsoft.Json.JsonConverter<ExternalCredentialsBase>
{
    public override ExternalCredentialsBase ReadJson(JsonReader reader, Type objectType, ExternalCredentialsBase existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        // Load the object so we can inspect which discriminator property is present
        var jtoken = JObject.Load(reader);
        if (jtoken is not JObject obj)
        {
            return null;
        }

        // Determine the concrete type based on endpoint / endpointPrefix and deserialize fully
        if (obj.TryGetValue("endpointPrefix", StringComparison.OrdinalIgnoreCase, out var p) && p.Type == JTokenType.String)
        {
            var c = new ExternalEndpointPrefixCredentials();
            serializer.Populate(jtoken.CreateReader(), c);
            return c;
        }
        else if (obj.TryGetValue("endpoint", StringComparison.OrdinalIgnoreCase, out _))
        {
            var c = new ExternalEndpointCredentials();
            serializer.Populate(jtoken.CreateReader(), c);
            return c;
        }

        // Unknown shape; allow caller to handle validation failure
        return null;
    }

    public override void WriteJson(JsonWriter writer, ExternalCredentialsBase value, Newtonsoft.Json.JsonSerializer serializer)
    {
        // Not required
        throw new NotImplementedException();
    }
}

[Newtonsoft.Json.JsonConverter(typeof(ExternalCredentialsConverter))]
public abstract class ExternalCredentialsBase
{
    [JsonProperty("username")]
    public string Username { get; set; }
    [JsonProperty("password")]
    public string Password { get; set; }

    internal abstract bool Validate();

    internal abstract bool IsMatch(string uriString, out ExternalCredentialsBase matchingExternalEndpoint);
}

public class ExternalEndpointCredentials : ExternalCredentialsBase
{
    [JsonProperty("endpoint")]
    public string Endpoint { get; set; }

    internal override bool IsMatch(string uriString, out ExternalCredentialsBase matchingExternalEndpoint)
    {
        matchingExternalEndpoint = null;
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpointUri))
        {
            return false;
        }
        var url = endpointUri.AbsoluteUri;
        if (url.Equals(uriString, StringComparison.OrdinalIgnoreCase))
        {
            matchingExternalEndpoint = this;
            return true;
        }
        return false;
    }

    internal override bool Validate()
    {
        return Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpointUri);
    }

}

public class ExternalEndpointPrefixCredentials : ExternalCredentialsBase
{
    [JsonProperty("endpointPrefix")]
    public string EndpointPrefix { get; set; }

    internal override bool IsMatch(string uriString, out ExternalCredentialsBase matchingExternalEndpoint)
    {
        matchingExternalEndpoint = null;
        if (!Uri.TryCreate(EndpointPrefix, UriKind.Absolute, out var endpointUri))
        {
            return false;
        }
        var url = endpointUri.AbsoluteUri;
        if (!url.EndsWith("/"))
        {
            url += '/';
        }
        if (uriString.StartsWith(url, StringComparison.OrdinalIgnoreCase))
        {
            matchingExternalEndpoint = this;
            return true;
        }
        return false;
    }

    internal override bool Validate()
    {
        return Uri.TryCreate(EndpointPrefix, UriKind.Absolute, out var endpointUri);
    }
}


public class ExternalCredentialsList : List<ExternalCredentialsBase>
{
    internal bool FindMatch(string uriString, out ExternalCredentialsBase matchingExternalEndpoint)
    {
        matchingExternalEndpoint = null;
        foreach (var credentials in this)
        {
            if (credentials.IsMatch(uriString, out matchingExternalEndpoint))
            {
                return true;
            }
        }
        return false;
    }


    public ExternalCredentialsBase this[string index]
    {
        get
        {
            if (FindMatch(index, out var matchingExternalEndpoint))
            {
                return matchingExternalEndpoint;
            }
            return null;
        }
    }
}

public class ExternalEndpointCredentialsContainer
{
    [JsonProperty("endpointCredentials")]
    public ExternalCredentialsList EndpointCredentials { get; set; }
}

public class EndpointCredentials
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; }
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; }
    [JsonPropertyName("clientCertificateFilePath")]
    public string CertificateFilePath { get; set; }
    [JsonPropertyName("clientCertificateSubjectName")]
    public string CertificateSubjectName { get; set; }
}

public class EndpointCredentialsContainer
{
    [JsonPropertyName("endpointCredentials")]
    public EndpointCredentials[] EndpointCredentials { get; set; }
}

public static class FeedEndpointCredentialsParser
{
    private static readonly JsonSerializerOptions options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static Dictionary<string, EndpointCredentials> ParseFeedEndpointsJsonToDictionary(ILogger logger)
    {
        string feedEndpointsJson = Environment.GetEnvironmentVariable(EnvUtil.EndpointCredentials);
        if (string.IsNullOrWhiteSpace(feedEndpointsJson))
        {
            return new Dictionary<string, EndpointCredentials>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            logger.Verbose(Resources.ParsingJson);
            Dictionary<string, EndpointCredentials> credsResult = new Dictionary<string, EndpointCredentials>(StringComparer.OrdinalIgnoreCase);
            EndpointCredentialsContainer endpointCredentials = System.Text.Json.JsonSerializer.Deserialize<EndpointCredentialsContainer>(feedEndpointsJson, options);
            if (endpointCredentials == null)
            {
                logger.Verbose(Resources.NoEndpointsFound);
                return credsResult;
            }

            foreach (var credentials in endpointCredentials.EndpointCredentials)
            {
                if (credentials == null)
                {
                    logger.Verbose(Resources.EndpointParseFailure);
                    break;
                }

                if (credentials.ClientId == null)
                {
                    logger.Verbose(Resources.EndpointParseFailure);
                    break;
                }

                if (credentials.CertificateSubjectName != null && credentials.CertificateFilePath != null)
                {
                    logger.Verbose(Resources.EndpointParseFailure);
                    break;
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
        catch (Exception ex)
        {
            logger.Verbose(string.Format(Resources.VstsBuildTaskExternalCredentialCredentialProviderError, ex));
            return new Dictionary<string, EndpointCredentials>(StringComparer.OrdinalIgnoreCase); ;
        }
    }

    public static ExternalCredentialsList ParseExternalFeedEndpointsJsonToList(ILogger logger)
    {
        string feedEndpointsJson = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);
        if (string.IsNullOrWhiteSpace(feedEndpointsJson))
        {
            return new ExternalCredentialsList();
        }

        try
        {
            logger.Verbose(Resources.ParsingJson);
            if (feedEndpointsJson.Contains("':"))
            {
                logger.Warning(Resources.InvalidJsonWarning);
            }
            logger.Info(feedEndpointsJson);

            var credsResult = new ExternalCredentialsList();
            ExternalEndpointCredentialsContainer endpointCredentials = JsonConvert.DeserializeObject<ExternalEndpointCredentialsContainer>(feedEndpointsJson);
            if (endpointCredentials == null || endpointCredentials.EndpointCredentials == null)
            {
                logger.Verbose(Resources.NoEndpointsFound);
                return credsResult;
            }

            
            foreach (var credentials in endpointCredentials.EndpointCredentials)
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

                if (!credentials.Validate())
                {
                    logger.Verbose(Resources.EndpointParseFailure);
                    break;
                }
                
                credsResult.Add(credentials);
            }

            return credsResult;
        }
        catch (Exception ex)
        {
            logger.Verbose(string.Format(Resources.VstsBuildTaskExternalCredentialCredentialProviderError, ex));
            return new ExternalCredentialsList();
        }
    }
}
