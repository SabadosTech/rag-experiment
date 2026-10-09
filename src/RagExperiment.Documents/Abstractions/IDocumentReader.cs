using RagExperiment.Documents.Models;

namespace RagExperiment.Documents.Abstractions;

/// <summary>Adapta un formato de origen a texto y estructura comunes, antes de la normalización.</summary>
public interface IDocumentReader
{
    /// <summary>Clave del formato, por ejemplo epub; permite seleccionar o sustituir lectores mediante el resolver.</summary>
    string FormatId { get; }

    /// <summary>Extrae un documento completo sin cerrar el stream del llamador, incluso ante errores.</summary>
    /// <param name="content">Contenido de origen; cada lector documenta sus requisitos de lectura y posicionamiento.</param>
    /// <param name="context">Identidad lógica y atributos del corpus suministrados por la aplicación.</param>
    /// <param name="cancellationToken">Solicitud de cancelación que la implementación observa durante el procesamiento.</param>
    /// <returns>Texto extraído, nodos y metadata; todavía no es un NormalizedDocument.</returns>
    Task<ParsedDocument> ReadAsync(Stream content, DocumentReadContext context,
        CancellationToken cancellationToken = default);
}
