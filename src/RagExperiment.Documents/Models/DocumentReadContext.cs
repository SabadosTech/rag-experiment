using System.Text.Json;

namespace RagExperiment.Documents.Models;

/// <summary>Datos de la aplicación que acompañan una lectura sin acoplar al lector con rutas o configuración global.</summary>
public sealed class DocumentReadContext
{
    /// <summary>Copia la identidad y los atributos del corpus para una operación de lectura.</summary>
    /// <param name="documentId">Identidad lógica no vacía; el lector no la deduce del nombre del archivo.</param>
    /// <param name="metadata">Overrides JSON opcionales del corpus, clonados; reemplazan valores extraídos por clave.</param>
    public DocumentReadContext(string documentId, IReadOnlyDictionary<string, JsonElement>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        DocumentId = documentId;
        Metadata = DocumentModelValidation.CopyMetadata(metadata);
    }

    /// <summary>Identidad que se asignará al ParsedDocument resultante.</summary>
    public string DocumentId { get; }
    /// <summary>Atributos del corpus de solo lectura; prevalecen sobre metadata extraída por clave, sin combinación profunda.</summary>
    public IReadOnlyDictionary<string, JsonElement> Metadata { get; }
}
