using System.Collections.ObjectModel;
using System.Text.Json;
using RagExperiment.Documents.Models;

namespace RagExperiment.Chunking.Models;

/// <summary>A canonical documentary unit, independent of indexing and retrieval.</summary>
public sealed class Chunk
{
    public Chunk(string chunkId, string documentId, int sequence, string text,
        IEnumerable<string> sourceNodeIds, IEnumerable<TextSpan> sourceSpans,
        int level = 0, string? parentChunkId = null,
        IEnumerable<string>? headingPath = null,
        IReadOnlyDictionary<string, JsonElement>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sourceSpans);
        if (parentChunkId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(parentChunkId);
        if (StringComparer.Ordinal.Equals(chunkId, parentChunkId))
            throw new ArgumentException("A chunk cannot be its own parent.", nameof(parentChunkId));
        if ((parentChunkId is null) != (level == 0))
            throw new ArgumentException("Root chunks have level zero; children require a parent and positive level.", nameof(level));
        ChunkId = chunkId;
        DocumentId = documentId;
        Sequence = sequence;
        Text = text;
        Level = level;
        ParentChunkId = parentChunkId;
        SourceNodeIds = CopyStrings(sourceNodeIds);
        if (SourceNodeIds.Distinct(StringComparer.Ordinal).Count() != SourceNodeIds.Count)
            throw new ArgumentException("Source node references must be unique.", nameof(sourceNodeIds));
        SourceSpans = Array.AsReadOnly(sourceSpans.ToArray());
        HeadingPath = CopyStrings(headingPath ?? []);
        var copy = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (metadata is not null)
            foreach (var (key, value) in metadata)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(key);
                if (value.ValueKind == JsonValueKind.Undefined)
                    throw new ArgumentException("Metadata values must be valid JSON.", nameof(metadata));
                copy.Add(key, value.Clone());
            }
        Metadata = new ReadOnlyDictionary<string, JsonElement>(copy);
    }

    /// <summary>Identificador explícito del chunk, único dentro del resultado. Se usa también para las referencias padre/hijo.</summary>
    public string ChunkId { get; }
    /// <summary>Identidad lógica del documento fuente. Debe coincidir con la del documento usado para construir ChunkingResult.</summary>
    public string DocumentId { get; }
    /// <summary>Posición global del chunk dentro del resultado, comenzando en cero. Las secuencias deben ser contiguas, independientemente del nivel jerárquico.</summary>
    public int Sequence { get; }
    /// <summary>Texto canónico del chunk. Los headings se conservan aparte; el modelo no los inserta ni verifica la concatenación literal de los spans.</summary>
    public string Text { get; }
    /// <summary>Profundidad en la jerarquía de chunks: cero para raíces y nivel del padre más uno para hijos. No es el nivel de heading del documento.</summary>
    public int Level { get; }
    /// <summary>ID del chunk padre dentro del mismo resultado. Es null para raíces; representa la relación sin implementar retrieval padre/hijo.</summary>
    public string? ParentChunkId { get; }
    /// <summary>IDs únicos de los nodos del documento normalizado vinculados al chunk. Pueden estar vacíos; el resultado valida que los referenciados existan.</summary>
    public IReadOnlyList<string> SourceNodeIds { get; }
    /// <summary>Rangos de procedencia sobre NormalizedDocument.Text, en unidades UTF-16. Permiten varias regiones; el resultado valida sus límites y no exige cobertura completa.</summary>
    public IReadOnlyList<TextSpan> SourceSpans { get; }
    /// <summary>Ruta de títulos que aporta contexto jerárquico, desde el más general al más específico. Puede estar vacía y se mantiene separada del texto del chunk.</summary>
    public IReadOnlyList<string> HeadingPath { get; }
    /// <summary>Atributos extensibles del chunk, con valores JSON clonados y colección de solo lectura. No reemplazan sus campos de identidad o procedencia.</summary>
    public IReadOnlyDictionary<string, JsonElement> Metadata { get; }

    internal static IReadOnlyList<string> CopyStrings(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = values.ToArray();
        foreach (var value in copy) ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Array.AsReadOnly(copy);
    }
}
