using BenchmarkDotNet.Running;
using FlexQuery.Benchmarks.Infrastructure;
using FlexQuery.Benchmarks.Benchmarks.Api;

namespace FlexQuery.Benchmarks;

/// <summary>
/// FlexQuery.NET Benchmark Suite
///
/// Usage:
///   dotnet run -c Release                          → Interactive menu
///   dotnet run -c Release -- --filter *Parsing*    → Parsing benchmarks only
///   dotnet run -c Release -- --filter *Execution*  → End-to-end benchmarks only
///   dotnet run -c Release -- --filter *Expression* → Expression generation only
///   dotnet run -c Release -- --list flat            → List all benchmarks
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("setup", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkDbSetup.Initialize();
            return;
        }

        // BenchmarkSwitcher allows running specific benchmarks via command line filters
        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args);
    }
}
