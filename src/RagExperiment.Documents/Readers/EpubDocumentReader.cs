using System.Text.Json;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using RagExperiment.Documents.Abstractions;
using RagExperiment.Documents.Models;
using RagExperiment.Documents.Readers.Epub;
using VersOne.Epub;
using VersOne.Epub.Options;

namespace RagExperiment.Documents.Readers;

/// <summary>Adapta VersOne.Epub y la extracción XHTML al modelo común ParsedDocument.</summary>
public sealed class EpubDocumentReader : IDocumentReader
{
    /// <summary>Clave epub usada por el resolver; no identifica la biblioteca ni el documento concreto.</summary>
    public string FormatId => "epub";

    /// <summary>
    /// Lee recursos según el spine y construye texto, jerarquía y procedencia sin cargar imágenes ni fuentes.
    /// </summary>
    /// <param name="content">EPUB en un stream legible y posicionable ubicado en cero; permanece abierto al terminar.</param>
    /// <param name="context">ID lógico y metadata del corpus que prevalece por clave sobre la extraída.</param>
    /// <param name="cancellationToken">Se comprueba entre llamadas a VersOne y durante extracción; no interrumpe una llamada interna sin token.</param>
    /// <returns>Documento extraído completo, con spans sobre su Text; no devuelve resultados parciales.</returns>
    public async Task<ParsedDocument> ReadAsync(Stream content, DocumentReadContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(context);
        if (!content.CanRead || !content.CanSeek || content.Position != 0)
            throw new ArgumentException("EPUB content must be readable, seekable and positioned at zero.", nameof(content));
        cancellationToken.ThrowIfCancellationRequested();

        using var wrapper = new LeaveOpenStream(content);
        try
        {
            var encryptedResources = ReadEncryptedResources(wrapper);
            cancellationToken.ThrowIfCancellationRequested();
            var options = new EpubReaderOptions
            {
                ContentDownloaderOptions = new ContentDownloaderOptions { DownloadContent = false },
                PackageReaderOptions = new PackageReaderOptions { IgnoreMissingToc = true },
                Epub3NavDocumentReaderOptions = new Epub3NavDocumentReaderOptions { IgnoreMissingNavManifestItemError = true },
                ContentReaderOptions = new ContentReaderOptions { IgnoreMissingFileError = false },
                SpineReaderOptions = new SpineReaderOptions
                {
                    IgnoreMissingManifestItems = false,
                    IgnoreMissingContentFiles = false,
                    SkipSpineItemsReferencingRemoteContent = false
                }
            };
            using var book = await EpubReader.OpenBookAsync(wrapper, options).ConfigureAwait(false)
                ?? throw new FormatException("EPUB reader returned no book.");
            cancellationToken.ThrowIfCancellationRequested();
            var resources = await book.GetReadingOrderAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var builder = new XhtmlDocumentBuilder(cancellationToken);
            foreach (var resource in resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (resource.FilePath == book.Content.NavigationHtmlFile?.FilePath) continue;
                if (encryptedResources.Contains(NormalizeResource(resource.FilePath)))
                    throw new FormatException($"EPUB resource '{resource.FilePath}' is encrypted and is not supported.");
                var xhtml = await resource.ReadContentAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                builder.AddResource(resource.FilePath, xhtml);
            }

            var metadata = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(book.Title)) metadata["title"] = JsonSerializer.SerializeToElement(book.Title);
            if (book.AuthorList.Count > 0) metadata["authors"] = JsonSerializer.SerializeToElement(book.AuthorList);
            var language = book.Schema.Package.Metadata.Languages.FirstOrDefault()?.Language;
            if (!string.IsNullOrWhiteSpace(language)) metadata["language"] = JsonSerializer.SerializeToElement(language);
            if (!string.IsNullOrWhiteSpace(book.Description)) metadata["description"] = JsonSerializer.SerializeToElement(book.Description);
            foreach (var (key, value) in context.Metadata) metadata[key] = value;
            cancellationToken.ThrowIfCancellationRequested();
            return builder.Build(context, metadata);
        }
        catch (Exception) when (wrapper.SourceFailure is not null)
        {
            // EPUB/ZIP libraries can wrap stream failures; preserve their I/O/cancellation types.
            wrapper.SourceFailure.Throw();
            throw;
        }
        catch (Exception exception) when (exception is EpubReaderException or InvalidDataException or XmlException or FormatException)
        {
            throw new DocumentReadException($"Unable to interpret EPUB document '{context.DocumentId}'.", exception);
        }
    }

    /// <summary>Consulta encryption.xml para reconocer contenido cifrado y vuelve a posicionar el stream en cero.</summary>
    /// <param name="content">Stream EPUB posicionable cuyo cierre sigue siendo responsabilidad del llamador.</param>
    /// <returns>Rutas internas normalizadas declaradas como cifradas; el lector solo rechaza las de contenido principal.</returns>
    private static HashSet<string> ReadEncryptedResources(Stream content)
    {
        // Fonts may be obfuscated in otherwise readable books. Reject only encrypted reading content.
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("META-INF/encryption.xml");
        var resources = new HashSet<string>(StringComparer.Ordinal);
        if (entry is not null)
        {
            using var stream = entry.Open();
            using var xml = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var document = XDocument.Load(xml);
            XNamespace encryption = "http://www.w3.org/2001/04/xmlenc#";
            foreach (var reference in document.Descendants(encryption + "CipherReference"))
                if (reference.Attribute("URI") is { } uri) resources.Add(NormalizeResource(uri.Value));
        }
        content.Position = 0;
        return resources;
    }

    /// <summary>Resuelve segmentos de ruta y escapes URI para comparar referencias al mismo recurso interno.</summary>
    /// <param name="resource">Ruta o URI de un recurso declarado en el EPUB.</param>
    /// <returns>Ruta decodificada sin barra inicial; la base URI usada no se consulta por red.</returns>
    private static string NormalizeResource(string resource) =>
        Uri.UnescapeDataString(new Uri(new Uri("https://epub.invalid/"), resource).AbsolutePath).TrimStart('/');

}
