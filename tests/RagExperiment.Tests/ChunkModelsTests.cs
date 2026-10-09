using System.Text.Json;
using RagExperiment.Chunking.Models;
using RagExperiment.Documents.Models;
using Xunit;

namespace RagExperiment.Tests;

public sealed class ChunkModelsTests
{
    private static NormalizedDocument Document() => new("d", "r", "ab😀cd",
        [new("n", DocumentNodeKind.Paragraph, new(0, 6))]);

    private static Chunk Make(string id = "c", int sequence = 0, int level = 0,
        string? parent = null, string documentId = "d") =>
        new(id, documentId, sequence, "ab", ["n"], [new(0, 2)], level, parent);

    private static ChunkingResult Result(params Chunk[] chunks) =>
        new(Document(), "v", "structure-aware", "1", chunks);

    [Fact]
    public void ResultPreservesOrderedHierarchyAndExplicitIdentities()
    {
        var parent = Make("p");
        var child = Make("c", 1, 1, "p");
        var result = Result(parent, child);
        Assert.Equal("d", result.DocumentId);
        Assert.Equal("r", result.RevisionId);
        Assert.Equal("v", result.VariantId);
        Assert.Equal("structure-aware", result.StrategyId);
        Assert.Equal("1", result.StrategyVersion);
        Assert.Equal(new[] { parent, child }, result.Chunks);
        Assert.Equal("p", result.Chunks[1].ParentChunkId);
        Assert.Equal(1, result.Chunks[1].Level);
    }

    [Fact]
    public void ParentMayAppearAfterChildInGlobalSequence()
    {
        Assert.Equal(2, Result(Make("c", 0, 1, "p"), Make("p", 1)).Chunks.Count);
    }

    [Fact]
    public void MultipleUtf16SourceRangesAndSeparateHeadingsArePreserved()
    {
        var chunk = new Chunk("c", "d", 0, "😀cd", ["n"], [new(2, 2), new(4, 2)],
            headingPath: ["Chapter", "Section"]);
        var result = Result(chunk);
        Assert.Equal("😀", Document().Text.Substring(chunk.SourceSpans[0].Start, chunk.SourceSpans[0].Length));
        Assert.Equal("😀cd", result.Chunks[0].Text);
        Assert.Equal(new[] { "Chapter", "Section" }, chunk.HeadingPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void InvalidResultsAreRejected(int scenario)
    {
        Chunk[] chunks = scenario switch
        {
            0 => [Make(), Make(sequence: 1)],
            1 => [Make(sequence: 1)],
            2 => [Make(documentId: "other")],
            3 => [Make(level: 1, parent: "missing")],
            4 => [Make("p"), Make("c", 1, 2, "p")],
            5 => [Make("a", 0, 1, "b"), Make("b", 1, 2, "a")],
            6 => [new("c", "d", 0, "x", ["n"], [new(6, 1)])],
            7 => [new("c", "d", 0, "x", ["missing"], [new(0, 1)])],
            _ => [null!]
        };
        Assert.Throws<ArgumentException>(() => Result(chunks));
    }

    [Fact]
    public void EmptyResultsAndWarningsAreSupported()
    {
        var result = new ChunkingResult(new("d", "r", "", []), "v", "s", "1", [],
            ["Document has no text."]);
        Assert.Empty(result.Chunks);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void CollectionsAndJsonAreOwnedByModels()
    {
        using var json = JsonDocument.Parse("""{"number":3,"enabled":true,"items":["a"]}""");
        var metadata = new Dictionary<string, JsonElement> { ["data"] = json.RootElement };
        var ids = new List<string> { "n" };
        var spans = new List<TextSpan> { new(0, 2) };
        var headings = new List<string> { "Title" };
        var chunk = new Chunk("c", "d", 0, "ab", ids, spans, headingPath: headings, metadata: metadata);
        var chunks = new List<Chunk> { chunk };
        var warnings = new List<string> { "Example warning." };
        var result = new ChunkingResult(Document(), "v", "s", "1", chunks, warnings);
        ids.Clear();
        spans.Clear();
        headings.Clear();
        metadata.Clear();
        chunks.Clear();
        warnings.Clear();
        json.Dispose();

        Assert.Equal("n", Assert.Single(chunk.SourceNodeIds));
        Assert.Equal(new TextSpan(0, 2), Assert.Single(chunk.SourceSpans));
        Assert.Equal("Title", Assert.Single(chunk.HeadingPath));
        Assert.Equal(3, chunk.Metadata["data"].GetProperty("number").GetInt32());
        Assert.Single(result.Chunks);
        Assert.Single(result.Warnings);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)chunk.SourceNodeIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TextSpan>)chunk.SourceSpans).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)chunk.HeadingPath).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, JsonElement>)chunk.Metadata).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Chunk>)result.Chunks).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.Warnings).Clear());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RequiredIdsAreValidated(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => Make(id!));
        Assert.ThrowsAny<ArgumentException>(() => Make(documentId: id!));
        Assert.ThrowsAny<ArgumentException>(() => new ChunkingResult(Document(), id!, "s", "1", []));
        Assert.ThrowsAny<ArgumentException>(() => new ChunkingResult(Document(), "v", id!, "1", []));
        Assert.ThrowsAny<ArgumentException>(() => new ChunkingResult(Document(), "v", "s", id!, []));
    }

    [Fact]
    public void InvalidChunkConstructionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(sequence: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(level: -1));
        Assert.Throws<ArgumentException>(() => Make(level: 1));
        Assert.Throws<ArgumentException>(() => Make(parent: "p"));
        Assert.Throws<ArgumentException>(() => Make(parent: "c", level: 1));
        Assert.Throws<ArgumentException>(() => new Chunk("c", "d", 0, "", ["n", "n"], []));
        Assert.ThrowsAny<ArgumentException>(() => new Chunk("c", "d", 0, "", [" "], []));
        Assert.Throws<ArgumentNullException>(() => new Chunk("c", "d", 0, null!, [], []));
        Assert.Throws<ArgumentNullException>(() => new Chunk("c", "d", 0, "", null!, []));
        Assert.Throws<ArgumentNullException>(() => new Chunk("c", "d", 0, "", [], null!));
        Assert.Throws<ArgumentException>(() => new Chunk("c", "d", 0, "", [], [],
            metadata: new Dictionary<string, JsonElement> { ["bad"] = default }));
        Assert.Throws<ArgumentNullException>(() => new ChunkingResult(null!, "v", "s", "1", []));
        Assert.Throws<ArgumentNullException>(() => new ChunkingResult(Document(), "v", "s", "1", null!));
    }
}
