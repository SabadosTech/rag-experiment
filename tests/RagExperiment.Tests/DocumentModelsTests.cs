using System.Text.Json;
using RagExperiment.Documents.Models;
using Xunit;

namespace RagExperiment.Tests;

public sealed class DocumentModelsTests
{
    [Fact]
    public void DocumentsPreserveNestedStructureUnicodeAndSourceCoordinates()
    {
        const string text = "Título 😀\ncódigo";
        var source = new SourceLocator(1, 2, "chapter.xhtml", "title");
        DocumentNode[] nodes =
        [
            new("root", DocumentNodeKind.Document, new(0, text.Length)),
            new("heading", DocumentNodeKind.Heading, new(0, 9), "root", source),
            new("code", DocumentNodeKind.Code, new(10, 6), "root")
        ];
        var parsed = new ParsedDocument("book", text, nodes);
        var normalized = new NormalizedDocument("book", "revision", text, nodes);

        Assert.Equal(text, parsed.Text);
        Assert.Equal("revision", normalized.RevisionId);
        Assert.Equal("Título 😀", text.Substring(nodes[1].Span.Start, nodes[1].Span.Length));
        Assert.Equal(9, nodes[1].Span.End);
        Assert.Equal("root", normalized.Nodes[2].ParentNodeId);
        Assert.Same(source, normalized.Nodes[1].Source);
        Assert.Equal("chapter.xhtml", source.Resource);
        Assert.Equal("title", source.Anchor);
    }

    [Fact]
    public void EmptyDocumentsAreValid()
    {
        Assert.Empty(new ParsedDocument("d", "", []).Nodes);
        Assert.Empty(new NormalizedDocument("d", "r", "", []).Nodes);
        Assert.Equal(0, default(TextSpan).End);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(int.MaxValue, 1)]
    public void InvalidSpansAreRejected(int start, int length) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSpan(start, length));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RequiredIdsAreRejected(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ParsedDocument(id!, "", []));
        Assert.ThrowsAny<ArgumentException>(() => new NormalizedDocument(id!, "r", "", []));
        Assert.ThrowsAny<ArgumentException>(() => new NormalizedDocument("d", id!, "", []));
        Assert.ThrowsAny<ArgumentException>(() => new DocumentNode(id!, DocumentNodeKind.Other, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void InvalidDocumentStructureIsRejectedForBothRepresentations(int scenario)
    {
        DocumentNode[] nodes = scenario switch
        {
            0 => [new("a", DocumentNodeKind.Paragraph, new(0, 1)), new("a", DocumentNodeKind.Paragraph, new(1, 1))],
            1 => [new("a", DocumentNodeKind.Paragraph, new(0, 1), "missing")],
            2 => [new("a", DocumentNodeKind.Section, new(0, 2), "b"), new("b", DocumentNodeKind.Section, new(0, 2), "a")],
            3 => [new("a", DocumentNodeKind.Paragraph, new(2, 1))],
            4 => [new("a", DocumentNodeKind.Paragraph, new(1, 1)), new("b", DocumentNodeKind.Paragraph, new(0, 1))],
            _ => [null!]
        };
        Assert.Throws<ArgumentException>(() => new ParsedDocument("d", "ab", nodes));
        Assert.Throws<ArgumentException>(() => new NormalizedDocument("d", "r", "ab", nodes));
    }

    [Fact]
    public void DeepHierarchiesAreValidatedWithoutRecursion()
    {
        var nodes = Enumerable.Range(0, 10000)
            .Select(i => new DocumentNode(i.ToString(), DocumentNodeKind.Section, default,
                i == 9999 ? null : (i + 1).ToString()));
        Assert.Equal(10000, new NormalizedDocument("d", "r", "", nodes).Nodes.Count);
    }

    [Fact]
    public void DocumentsCopyCollectionsAndOwnTheirJsonValues()
    {
        using var json = JsonDocument.Parse("""{"items":[1,true],"name":"book"}""");
        var metadata = new Dictionary<string, JsonElement> { ["data"] = json.RootElement };
        var nodes = new List<DocumentNode> { new("n", DocumentNodeKind.Paragraph, new(0, 1)) };
        var parsed = new ParsedDocument("d", "a", nodes, metadata);
        var normalized = new NormalizedDocument("d", "r", "a", nodes, metadata);
        nodes.Clear();
        metadata.Clear();
        json.Dispose();

        foreach (var saved in new[] { parsed.Metadata, normalized.Metadata })
        {
            Assert.True(saved["data"].GetProperty("items")[1].GetBoolean());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, JsonElement>)saved).Clear());
        }
        Assert.Single(parsed.Nodes);
        Assert.Single(normalized.Nodes);
        Assert.Throws<NotSupportedException>(() => ((IList<DocumentNode>)parsed.Nodes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<DocumentNode>)normalized.Nodes).Clear());
    }

    [Fact]
    public void InvalidMetadataAndNullInputsAreRejected()
    {
        var metadata = new Dictionary<string, JsonElement> { ["undefined"] = default };
        Assert.Throws<ArgumentException>(() => new ParsedDocument("d", "", [], metadata));
        Assert.Throws<ArgumentException>(() => new NormalizedDocument("d", "r", "", [], metadata));
        Assert.Throws<ArgumentNullException>(() => new ParsedDocument("d", null!, []));
        Assert.Throws<ArgumentNullException>(() => new NormalizedDocument("d", "r", "", null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentNode("n", (DocumentNodeKind)99, default));
        Assert.Throws<ArgumentException>(() => new DocumentNode("n", DocumentNodeKind.Section, default, "n"));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(null, 1)]
    [InlineData(3, 2)]
    public void InvalidSourceLinesAreRejected(int? start, int? end) =>
        Assert.ThrowsAny<ArgumentException>(() => new SourceLocator(start, end));
}
