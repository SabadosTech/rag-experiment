using System.Text;
using System.Text.Json;
using RagExperiment.Documents.Models;
using RagExperiment.Documents.Readers;
using Xunit;

namespace RagExperiment.Tests;

public sealed class EpubReaderTests
{
    private readonly EpubDocumentReader reader = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadsSpineOrderMetadataAndLeavesCallerStreamOpen(bool epub3)
    {
        using var stream = EpubFixture.Create(epub3: epub3);
        var doc = await reader.ReadAsync(stream, new("book"));
        Assert.Equal("Hola mundo.\n\nFinal.", doc.Text);
        Assert.Equal("book", doc.DocumentId);
        Assert.Equal("Libro", doc.Metadata["title"].GetString());
        Assert.Equal("Autora", doc.Metadata["authors"][0].GetString());
        Assert.Equal("es", doc.Metadata["language"].GetString());
        Assert.Equal("Descripción", doc.Metadata["description"].GetString());
        Assert.True(stream.CanRead);
        Assert.Equal(2, doc.Nodes.Count(n => n.Kind == DocumentNodeKind.Section));
    }

    [Fact]
    public async Task ExtractsUnicodeBlocksAndSourceCoordinatesWithoutDuplicatingText()
    {
        using var stream = EpubFixture.Create("""
            Texto <em>suelto</em>.
            <h1 id="title">Título &amp; 😀</h1>
            <p id="p1">Hola <strong>mundo</strong> &lt;é&gt;.<br/>Otra línea.</p>
            <ul><li>Uno <em>dos</em></li><li>Tres<ul><li>Cuatro</li></ul></li></ul>
            <table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>
            <pre id="code">  x&#13;&#10;    y</pre>
            <p><a href="https://example.invalid">Enlace</a><img src="missing-image.png"/></p>
            <nav><p>Omitir navegación</p></nav><script>Omitir script</script><style>Omitir estilo</style>
            """);
        var doc = await reader.ReadAsync(stream, new("book"));
        Assert.Equal("Texto suelto.\n\nTítulo & 😀\n\nHola mundo <é>.\nOtra línea.\n\nUno dos\nTres\nCuatro\n\nA\tB\n1\t2\n\n  x\n    y\n\nEnlace\n\nFinal.", doc.Text);
        var p = Assert.Single(doc.Nodes, n => n.Source?.Anchor == "p1");
        Assert.Equal("Hola mundo <é>.\nOtra línea.", Slice(doc, p));
        Assert.Equal("OPS/chapter.xhtml", p.Source!.Resource);
        Assert.Null(p.Source.StartLine);
        Assert.Contains(doc.Nodes, n => n.Kind == DocumentNodeKind.Code);
        Assert.Contains(doc.Nodes, n => n.Kind == DocumentNodeKind.List);
        Assert.Contains(doc.Nodes, n => n.Kind == DocumentNodeKind.Table);
        Assert.Equal(doc.Text, Slice(doc, doc.Nodes.Single(n => n.Kind == DocumentNodeKind.Document)));
        foreach (var node in doc.Nodes.Where(n => n.ParentNodeId is not null))
        {
            var parent = doc.Nodes.Single(n => n.NodeId == node.ParentNodeId);
            Assert.InRange(node.Span.Start, parent.Span.Start, parent.Span.End);
            Assert.InRange(node.Span.End, node.Span.Start, parent.Span.End);
        }
    }

    [Fact]
    public async Task PreservesExplicitSectionsAndClosesHeadingSectionsAtTheirScopeBoundary()
    {
        using var stream = EpubFixture.Create("""
            <h1 id="a">A</h1><p>Intro</p><div><h3 id="b">B</h3><p>Detalle</p></div>
            <section id="explicit"><h2 id="c">C</h2><section id="inner"><p>Dentro</p></section></section>
            <p>Después</p><h1 id="d">D</h1><p>Fin</p>
            """);
        var doc = await reader.ReadAsync(stream, new("book"));
        DocumentNode Section(string anchor) => doc.Nodes.Single(n => n.Kind == DocumentNodeKind.Section && n.Source?.Anchor == anchor);
        Assert.Equal(Section("a").NodeId, Section("b").ParentNodeId);
        Assert.Equal(Section("b").NodeId, Section("explicit").ParentNodeId);
        Assert.Equal(Section("explicit").NodeId, Section("c").ParentNodeId);
        Assert.Equal(Section("c").NodeId, Section("inner").ParentNodeId);
        Assert.Equal("C\n\nDentro", Slice(doc, Section("explicit")));
        Assert.EndsWith("Después", Slice(doc, Section("b")));
        Assert.DoesNotContain("D\n\nFin", Slice(doc, Section("a")));
        Assert.Equal(Section("a").ParentNodeId, Section("d").ParentNodeId);
    }

    [Fact]
    public async Task MetadataIsDefensiveAndCorpusOverridesExtractedValues()
    {
        using var json = JsonDocument.Parse("{\"title\":\"Corpus\",\"tags\":[\"test\"]}");
        var attributes = json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var context = new DocumentReadContext("book", attributes);
        attributes.Clear();
        json.Dispose();
        using var stream = EpubFixture.Create();
        var doc = await reader.ReadAsync(stream, context);
        Assert.Equal("Corpus", doc.Metadata["title"].GetString());
        Assert.Equal("test", doc.Metadata["tags"][0].GetString());
        Assert.Equal("Autora", doc.Metadata["authors"][0].GetString());
    }

    [Fact]
    public async Task NavigationDocumentInSpineIsExcludedAndRepeatedReadsAreDeterministic()
    {
        using var stream = EpubFixture.Create(navigation: true);
        var first = await reader.ReadAsync(stream, new("book"));
        stream.Position = 0;
        var second = await reader.ReadAsync(stream, new("book"));
        Assert.Equal("Hola mundo.\n\nFinal.", first.Text);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(first.Nodes.Select(n => (n.NodeId, n.Kind, n.Span, n.ParentNodeId)),
            second.Nodes.Select(n => (n.NodeId, n.Kind, n.Span, n.ParentNodeId)));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task RejectsMissingRemoteOrProtectedContent(bool missing, bool remote, bool encrypted)
    {
        using var stream = EpubFixture.Create(missing: missing, remote: remote, encrypted: encrypted);
        var error = await Assert.ThrowsAsync<DocumentReadException>(() => reader.ReadAsync(stream, new("book")));
        Assert.NotNull(error.InnerException);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task RejectsCorruptArchiveAndInvalidStreamsAndHonorsCancellation()
    {
        using var corrupt = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));
        await Assert.ThrowsAsync<DocumentReadException>(() => reader.ReadAsync(corrupt, new("book")));
        Assert.True(corrupt.CanRead);
        using var valid = EpubFixture.Create();
        valid.Position = 1;
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(valid, new("book")));
        valid.Position = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(valid, new("book"), new CancellationToken(true)));
        Assert.True(valid.CanRead);
        Assert.Equal(0, valid.Position);
        await Assert.ThrowsAsync<ArgumentNullException>(() => reader.ReadAsync(null!, new("book")));
        using var nonSeekable = new NonSeekableStream();
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(nonSeekable, new("book")));
        using var closed = new MemoryStream();
        closed.Dispose();
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(closed, new("book")));
    }

    private static string Slice(ParsedDocument doc, DocumentNode node) => doc.Text.Substring(node.Span.Start, node.Span.Length);

    [Fact]
    public async Task FontObfuscationDoesNotBlockUnencryptedTextAndEmptySectionsAreValid()
    {
        using var stream = EpubFixture.Create("<section id=\"empty\"></section><p>Texto</p>",
            encrypted: true, encryptionUri: "OPS/font.otf");
        var doc = await reader.ReadAsync(stream, new("book"));
        Assert.Equal("Texto\n\nFinal.", doc.Text);
        Assert.Equal(0, Assert.Single(doc.Nodes, n => n.Source?.Anchor == "empty").Span.Length);
    }

    [Fact]
    public async Task SourceIoFailuresKeepTheirTypeAndLeaveStreamOpen()
    {
        using var stream = new BrokenStream();
        var error = await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync(stream, new("book")));
        Assert.Equal("source unavailable", error.Message);
        Assert.True(stream.CanRead);
    }

    private sealed class BrokenStream : MemoryStream
    {
        public BrokenStream() : base(new byte[64]) { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("source unavailable");
        public override int Read(Span<byte> buffer) => throw new IOException("source unavailable");
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
