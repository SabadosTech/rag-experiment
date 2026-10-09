using RagExperiment.Documents.Abstractions;
using RagExperiment.Documents.Models;
using RagExperiment.Documents.Readers;
using Xunit;

namespace RagExperiment.Tests;

public sealed class DocumentReaderResolverTests
{
    [Fact]
    public async Task SupportsAdditionalFormatsAndReplacementEpubReader()
    {
        var epub = new StubReader("epub");
        var markdown = new StubReader("markdown");
        var resolver = new DocumentReaderResolver([markdown, epub]);
        Assert.Same(epub, resolver.Resolve("EPUB"));
        Assert.Same(markdown, resolver.Resolve("markdown"));
        using var content = new MemoryStream();
        var doc = await resolver.Resolve("epub").ReadAsync(content, new("logical-id"));
        Assert.Equal("logical-id", doc.DocumentId);
        Assert.Equal("epub", doc.Text);
        Assert.True(content.CanRead);
    }

    [Fact]
    public void RejectsUnknownFormatsAndDuplicateRegistrationsRegardlessOfCase()
    {
        Assert.Throws<NotSupportedException>(() => new DocumentReaderResolver([]).Resolve("markdown"));
        Assert.Throws<ArgumentException>(() => new DocumentReaderResolver([new StubReader("epub"), new StubReader("EPUB")]));
        Assert.Throws<ArgumentException>(() => new DocumentReaderResolver([new StubReader(" ")]));
    }

    private sealed class StubReader(string format) : IDocumentReader
    {
        public string FormatId => format;
        public Task<ParsedDocument> ReadAsync(Stream content, DocumentReadContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ParsedDocument(context.DocumentId, format, []));
    }
}
