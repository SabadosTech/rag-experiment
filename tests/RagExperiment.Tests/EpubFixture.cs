using System.IO.Compression;
using System.Text;

namespace RagExperiment.Tests;

internal static class EpubFixture
{
    public static MemoryStream Create(string body = "<p>Hola mundo.</p>", bool epub3 = true,
        bool missing = false, bool remote = false, bool encrypted = false, bool navigation = false,
        string encryptionUri = "OPS/chapter.xhtml")
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string value)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(value);
            }
            Add("mimetype", "application/epub+zip");
            Add("META-INF/container.xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="OPS/book.opf" media-type="application/oebps-package+xml" /></rootfiles>
                </container>
                """);
            var version = epub3 ? "3.0" : "2.0";
            var href = remote ? "https://example.invalid/chapter.xhtml" : "chapter.xhtml";
            Add("OPS/book.opf", $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="{{version}}" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="book-id">urn:test:book</dc:identifier><dc:title>Libro</dc:title>
                    <dc:creator>Autora</dc:creator><dc:language>es</dc:language><dc:description>Descripción</dc:description>
                    <meta property="dcterms:modified">2020-01-01T00:00:00Z</meta>
                  </metadata>
                  <manifest>
                    <item id="second" href="second.xhtml" media-type="application/xhtml+xml" />
                    <item id="first" href="{{href}}" media-type="application/xhtml+xml" />
                    <item id="image" href="missing-image.png" media-type="image/png" />
                    <item id="remote-image" href="https://example.invalid/image.png" media-type="image/png" />
                    {{(navigation ? "<item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\" />" : "")}}
                  </manifest>
                  <spine><itemref idref="first" />{{(navigation ? "<itemref idref=\"nav\" />" : "")}}<itemref idref="second" /></spine>
                </package>
                """);
            // Physical ZIP order deliberately disagrees with spine order.
            Add("OPS/second.xhtml", Wrap("<p>Final.</p>"));
            if (!missing && !remote) Add("OPS/chapter.xhtml", Wrap(body));
            if (navigation) Add("OPS/nav.xhtml", Wrap("<nav epub:type=\"toc\"><ol><li><a href=\"chapter.xhtml\">Índice</a></li></ol></nav><p>No ingerir.</p>"));
            if (encrypted) Add("META-INF/encryption.xml", $$"""
                <encryption xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <EncryptedData xmlns="http://www.w3.org/2001/04/xmlenc#">
                    <EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#aes128-cbc" />
                    <CipherData><CipherReference URI="{{encryptionUri}}" /></CipherData>
                  </EncryptedData>
                </encryption>
                """);
        }
        stream.Position = 0;
        return stream;
    }

    private static string Wrap(string body) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
          <head><title>No extraer</title><style>no extraer</style></head><body>{body}</body>
        </html>
        """;
}
