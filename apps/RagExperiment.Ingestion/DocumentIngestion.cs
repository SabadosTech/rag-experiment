using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagExperiment.Documents.Models;
using RagExperiment.Documents.Readers;

namespace RagExperiment.Ingestion;

/// <summary>Abre el archivo elegido por la aplicación, invoca su lector y registra un resumen del resultado.</summary>
/// <param name="configuration">Configuración del host; Document contiene Path, Id y opcionalmente Format y Title.</param>
/// <param name="readers">Registro de estrategias para seleccionar el lector del formato explícito o inferido.</param>
/// <param name="logger">Registro de identidad, título y cantidades; no recibe el contenido completo del libro.</param>
internal sealed class DocumentIngestion(
    IConfiguration configuration, DocumentReaderResolver readers, ILogger<DocumentIngestion> logger)
{
    /// <summary>Procesa un archivo si hay configuración Document; sin ella no realiza ninguna lectura.</summary>
    /// <param name="cancellationToken">Cancelación de la aplicación, propagada al lector documental.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("Document");
        if (!section.GetChildren().Any()) return;
        var path = section["Path"];
        var id = section["Id"];
        ArgumentException.ThrowIfNullOrWhiteSpace(path, "Document:Path");
        ArgumentException.ThrowIfNullOrWhiteSpace(id, "Document:Id");
        var format = section["Format"] ?? Path.GetExtension(path).TrimStart('.');
        var reader = readers.Resolve(format);
        var metadata = new Dictionary<string, JsonElement>();
        if (section["Title"] is { } title) metadata["title"] = JsonSerializer.SerializeToElement(title);
        await using var stream = File.OpenRead(path);
        var document = await reader.ReadAsync(stream, new DocumentReadContext(id, metadata), cancellationToken);
        logger.LogInformation("Read document {DocumentId}: title={Title}, text length={TextLength}, nodes={NodeCount}.",
            document.DocumentId, document.Metadata.TryGetValue("title", out var value) ? value.ToString() : "",
            document.Text.Length, document.Nodes.Count);
    }
}
