using System.Text.Json;

namespace RagExperiment.Documents.Models;

/// <summary>Texto y estructura extraídos por un lector, antes de la normalización común.</summary>
public sealed class ParsedDocument
{
    /// <summary>Crea una representación inmutable y valida orden, spans, IDs y jerarquía de los nodos.</summary>
    /// <param name="documentId">Identidad lógica no vacía suministrada por la aplicación, independiente de la ruta física.</param>
    /// <param name="text">Texto completo extraído; todos los spans de nodes se interpretan sobre este string.</param>
    /// <param name="nodes">Nodos ordenados por inicio UTF-16; se copian y pueden tener spans anidados.</param>
    /// <param name="metadata">Atributos JSON opcionales; se clonan para no depender del diccionario o JsonDocument del llamador.</param>
    public ParsedDocument(string documentId, string text,
        IEnumerable<DocumentNode> nodes, IReadOnlyDictionary<string, JsonElement>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(text);
        DocumentId = documentId;
        Text = text;
        Nodes = DocumentModelValidation.CopyNodes(text, nodes);
        Metadata = DocumentModelValidation.CopyMetadata(metadata);
    }

    /// <summary>Identidad lógica del documento, suministrada por el llamador e independiente de la ruta del archivo.</summary>
    public string DocumentId { get; }
    /// <summary>Texto extraído antes de la normalización. Los spans de Nodes se refieren a este string.</summary>
    public string Text { get; }
    /// <summary>Nodos estructurales ordenados por inicio del span. Admiten rangos anidados y padres referenciados por ID; la colección se copia y es de solo lectura.</summary>
    public IReadOnlyList<DocumentNode> Nodes { get; }
    /// <summary>Atributos documentales extensibles, como título, autores o idioma. Los valores JSON se clonan y no reemplazan la identidad ni la procedencia.</summary>
    public IReadOnlyDictionary<string, JsonElement> Metadata { get; }
}
