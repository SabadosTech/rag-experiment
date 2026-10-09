using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagExperiment.Documents.Readers;
using RagExperiment.Ingestion;
using Xunit;

namespace RagExperiment.Tests;

public sealed class DocumentIngestionTests
{
    [Fact]
    public async Task WithoutDocumentConfigurationDoesNothing()
    {
        var log = new RecordingLogger();
        await Create([], log).RunAsync(default);
        Assert.Empty(log.Messages);
    }

    [Theory]
    [InlineData("Document:Path", "missing.epub")]
    [InlineData("Document:Id", "book")]
    public async Task PartialArgumentsAreRejected(string key, string value)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Create(new() { [key] = value }).RunAsync(default));
    }

    [Fact]
    public async Task UnknownFormatAndMissingFileAreReported()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => Create(new()
        {
            ["Document:Path"] = "book.md", ["Document:Id"] = "book"
        }).RunAsync(default));
        await Assert.ThrowsAsync<FileNotFoundException>(() => Create(new()
        {
            ["Document:Path"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".epub"), ["Document:Id"] = "book"
        }).RunAsync(default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("epub")]
    public async Task ReadsLocalFileWithExplicitOrInferredFormatAndReleasesFile(string? format)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".EPUB");
        try
        {
            using (var fixture = EpubFixture.Create())
                await File.WriteAllBytesAsync(path, fixture.ToArray());
            var log = new RecordingLogger();
            await Create(new()
            {
                ["Document:Path"] = path, ["Document:Id"] = "corpus-id", ["Document:Format"] = format,
                ["Document:Title"] = "Corpus title"
            }, log).RunAsync(default);
            var message = Assert.Single(log.Messages);
            Assert.Contains("corpus-id", message);
            Assert.Contains("Corpus title", message);
            Assert.DoesNotContain("Hola mundo.", message);
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }

    private static DocumentIngestion Create(Dictionary<string, string?> values, RecordingLogger? logger = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new DocumentReaderResolver([new EpubDocumentReader()]), logger ?? new RecordingLogger());

    private sealed class RecordingLogger : ILogger<DocumentIngestion>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
