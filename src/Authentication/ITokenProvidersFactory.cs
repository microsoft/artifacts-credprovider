namespace Microsoft.Artifacts.Authentication;

public interface ITokenProvidersFactory
{
    IEnumerable<ITokenProvider> Get(Uri authority);
}
