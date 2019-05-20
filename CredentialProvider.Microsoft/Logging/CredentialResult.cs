using System.Runtime.Serialization;

namespace NuGetCredentialProvider.Logging
{
    [DataContract]
    public class CredentialResult
    {
        public CredentialResult(string username, string password)
        {
            Username = username;
            Password = password;
        }

        [DataMember]
        public string Username { get; }

        [DataMember]
        public string Password { get; }
    }
}
