using NuGetCredentialProvider.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGetCredentialProvider.Util
{
    public interface ICachedCredentialsValidator
    {
        Task<bool> ValidateAsync(Uri uri, string username, string password);
    }

    public class BasicAuthCredentialsValidator : ICachedCredentialsValidator
    {
        public static HttpClient httpClient = new HttpClient();
        private readonly ILogger logger;

        public BasicAuthCredentialsValidator(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<bool> ValidateAsync(Uri uri, string username, string password)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            string base64EncodedCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64EncodedCreds}");

            logger.Verbose(string.Format(Resources.ValidatingCachedCredentialsFor, uri));
            HttpResponseMessage result = await httpClient.SendAsync(request);
            if (result.IsSuccessStatusCode)
            {
                logger.Verbose(string.Format(Resources.ValidCachedCredentials, uri));
                return true;
            }
            else
            {
                logger.Verbose(string.Format(Resources.InvalidCachedCredentials, uri));
                return false;
            }
        }
    }
}
