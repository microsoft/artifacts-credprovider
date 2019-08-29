namespace NuGetCredentialProvider.Cancellation
{
    public class RegistrationInfo
    {
        public string Name { get; }
        public string StackTrace { get; }

        public RegistrationInfo(string name, string stackTrace)
        {
            Name = name;
            StackTrace = stackTrace;
        }

    }
}