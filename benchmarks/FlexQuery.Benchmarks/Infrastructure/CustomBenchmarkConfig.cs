using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Exporters.Csv;
using System;
using System.IO;

namespace FlexQuery.Benchmarks.Infrastructure
{
    public class CustomBenchmarkConfig : ManualConfig
    {
        public CustomBenchmarkConfig()
        {
            // Requirement: Use timestamp-based unique names per run
            // Example timestamp format: yyyyMMdd_HHmmss
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            // Requirement: Use a custom artifacts path like: BenchmarkDotNet.Artifacts/Runs/{timestamp}
            ArtifactsPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts", "Runs", timestamp);

            // Requirement: Ensure exporters are added
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(HtmlExporter.Default);
            AddExporter(CsvExporter.Default);
            AddExporter(JsonExporter.Full);
            
            // Note: By using a unique folder per run, we avoid overwriting previous results.
        }
    }
}
