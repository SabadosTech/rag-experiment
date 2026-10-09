using System.Diagnostics;
using RagExperiment.Ingestion;
using Xunit;

namespace RagExperiment.Tests;

public sealed class IngestionProcessTests
{
    [Theory]
    [InlineData("none", 0)]
    [InlineData("valid", 0)]
    [InlineData("partial", 1)]
    [InlineData("missing", 1)]
    public async Task ApplicationReturnsExpectedExitCode(string scenario, int expected)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".epub");
        try
        {
            if (scenario == "valid")
            {
                using var fixture = EpubFixture.Create();
                await File.WriteAllBytesAsync(path, fixture.ToArray());
            }
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(typeof(DocumentIngestion).Assembly.Location);
            // The smoke tests must not pick up a local corpus configured through environment variables.
            foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("Document__", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(key);
            if (scenario != "none") start.ArgumentList.Add($"--Document:Path={path}");
            if (scenario is "valid" or "missing") start.ArgumentList.Add("--Document:Id=process-book");
            using var process = Process.Start(start)!;
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var output = await outputTask + await errorTask;
            Assert.True(process.ExitCode == expected, $"Exit={process.ExitCode}; expected={expected}\n{output}");
            if (scenario == "valid")
            {
                Assert.Contains("process-book", output);
                Assert.Contains("text length=19", output);
                Assert.DoesNotContain("Hola mundo.", output);
            }
        }
        finally { File.Delete(path); }
    }
}
