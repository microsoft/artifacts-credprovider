namespace Microsoft.Artifacts.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 5, iterationCount: 1)]
public class ColdStartBenchmark
{
    [Benchmark]
    public async Task<int> CachedSessionToken() => await NuGetCredentialProvider.Program.Main(new[]
    {
        "-Uri", "https://pkgs.dev.azure.com/mseng/AzureDevOps/_packaging/AzureDevOps_PublicPackages/nuget/v3/index.json",
        "-F", "Json"
    });

    [Benchmark]
    public async Task<int> CachedMsalToken() => await NuGetCredentialProvider.Program.Main(new[]
    {
        "-Uri", "https://pkgs.dev.azure.com/mseng/AzureDevOps/_packaging/AzureDevOps_PublicPackages/nuget/v3/index.json",
        "-F", "Json",
        "-IsRetry"
    });
}
