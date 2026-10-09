using System.Text.Json;

namespace RagExperiment.Documents.Models;

/// <summary>Texto canónico y estructura de una revisión procesada, preparados para chunking.</summary>
public sealed class NormalizedDocument
{
    /// <summary>Almacena un resultado ya normalizado; este constructor no limpia texto ni recalcula spans.</summary>
    /// <param name="documentId">Identidad lógica del documento, conservada respecto de ParsedDocument.</param>
    /// <param name="revisionId">ID no vacío de la revisión procesada, calculado o elegido por el llamador.</param>
    /// <param name="text">Texto canónico completo producido por el normalizador.</param>
    /// <param name="nodes">Nodos ordenados cuyos spans ya deben referirse al texto canónico; se copian y validan.</param>
    /// <param name="metadata">Atributos JSON opcionales de esta representación; se copian y clonan.</param>
    public NormalizedDocument(string documentId, string revisionId, string text,
        IEnumerable<DocumentNode> nodes, IReadOnlyDictionary<string, JsonElement>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(revisionId);
        ArgumentNullException.ThrowIfNull(text);
        DocumentId = documentId;
        RevisionId = revisionId;
        Text = text;
        Nodes = DocumentModelValidation.CopyNodes(text, nodes);
        Metadata = DocumentModelValidation.CopyMetadata(metadata);
    }

    /// <summary>Identidad lógica del documento, conservada a través de sus revisiones y variantes de chunking.</summary>
    public string DocumentId { get; }
    /// <summary>Identificador explícito de la revisión procesada. Lo suministra el llamador; este modelo no lo calcula automáticamente.</summary>
    public string RevisionId { get; }
    /// <summary>Texto canónico preparado para el chunking. Los spans de los nodos y chunks se refieren a este string.</summary>
    public string Text { get; }
    /// <summary>Nodos ordenados por inicio, con spans actualizados al texto canónico. La colección se copia y es de solo lectura.</summary>
    public IReadOnlyList<DocumentNode> Nodes { get; }
    /// <summary>Atributos documentales extensibles de esta representación. Los valores JSON se clonan para conservarlos aunque se libere su JsonDocument original.</summary>
    public IReadOnlyDictionary<string, JsonElement> Metadata { get; }
}
