using RagExperiment.Documents.Models;
using RagExperiment.Documents.Readers;
using Xunit;
using Xunit.Abstractions;

namespace RagExperiment.IntegrationTests;

public sealed class LocalEpubTests(ITestOutputHelper output)
{
    [Local1984Fact]
    [Trait("Category", "LocalCorpus")]
    public async Task Reads1984FromLocalCorpusWithMetadataStructureAndValidSpans()
    {
        await using var content = File.OpenRead(Local1984FactAttribute.BookPath!);
        var document = await new EpubDocumentReader().ReadAsync(content, new("orwell-1984"));

        Assert.Equal("orwell-1984", document.DocumentId);
        Assert.Contains("1984", document.Metadata["title"].GetString());
        Assert.Contains(document.Metadata["authors"].EnumerateArray(), 
            author => author.GetString()?.Contains("Orwell", StringComparison.OrdinalIgnoreCase) == true);
        Assert.True(document.Text.Length > 1000, "Expected the book's body, not only its title or metadata.");
        Assert.True(content.CanRead);

        var root = Assert.Single(document.Nodes, node => node.Kind == DocumentNodeKind.Document);
        Assert.Equal(new TextSpan(0, document.Text.Length), root.Span);
        Assert.True(document.Nodes.Count(node => node.Kind == DocumentNodeKind.Section) > 1);
        Assert.Contains(document.Nodes, node => node.Kind == DocumentNodeKind.Paragraph && node.Span.Length > 0);
        Assert.Contains(document.Nodes, node => node.Kind == DocumentNodeKind.Heading && node.Span.Length > 0);

        var byId = document.Nodes.ToDictionary(node => node.NodeId);
        var previousStart = 0;
        foreach (var node in document.Nodes)
        {
            Assert.InRange(node.Span.Start, previousStart, document.Text.Length);
            Assert.InRange(node.Span.End, node.Span.Start, document.Text.Length);
            previousStart = node.Span.Start;
            if (node == root) continue;

            Assert.NotNull(node.Source);
            Assert.False(string.IsNullOrWhiteSpace(node.Source.Resource));
            Assert.NotNull(node.ParentNodeId);
            Assert.True(byId.ContainsKey(node.ParentNodeId));
            var parent = byId[node.ParentNodeId];
            Assert.InRange(node.Span.Start, parent.Span.Start, parent.Span.End);
            Assert.InRange(node.Span.End, node.Span.Start, parent.Span.End);
            if (node.Kind is DocumentNodeKind.Paragraph or DocumentNodeKind.Heading)
                Assert.False(string.IsNullOrWhiteSpace(document.Text.Substring(node.Span.Start, node.Span.Length)));
        }

        output.WriteLine("Title: {0}; authors: {1}; UTF-16 length: {2}; nodes: {3}; resources: {4}.",
            document.Metadata["title"], document.Metadata["authors"], document.Text.Length,
            document.Nodes.Count, document.Nodes.Select(node => node.Source?.Resource).OfType<string>().Distinct().Count());
    }
}

/// <summary>The local corpus is optional and is read in place, without copying the book to test outputs.</summary>
public sealed class Local1984FactAttribute : FactAttribute
{
    internal static string? BookPath
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "rag-experiment.sln")))
                    return Path.Combine(directory.FullName, "data", "documents", "1984 - George Orwell.epub");
            return null;
        }
    }

    public Local1984FactAttribute()
    {
        if (!File.Exists(BookPath))
            Skip = "Local corpus file is required: data/documents/1984 - George Orwell.epub (not distributed with tests).";
    }
}
