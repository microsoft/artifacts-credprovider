namespace Microsoft.Artifacts.Authentication;

public interface ITokenProvidersFactory
{
    Task<IEnumerable<ITokenProvider>> GetAsync(Uri authority);
}
