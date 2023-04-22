global using BenchmarkDotNet.Attributes;
global using BenchmarkDotNet.Engines;
global using BenchmarkDotNet.Running;

namespace Microsoft.Artifacts.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
