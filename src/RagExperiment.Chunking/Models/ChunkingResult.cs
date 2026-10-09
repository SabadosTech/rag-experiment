using RagExperiment.Documents.Models;

namespace RagExperiment.Chunking.Models;

/// <summary>An ordered variant validated against its canonical source document.</summary>
public sealed class ChunkingResult
{
    public ChunkingResult(NormalizedDocument document, string variantId,
        string strategyId, string strategyVersion, IEnumerable<Chunk> chunks,
        IEnumerable<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(variantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyVersion);
        ArgumentNullException.ThrowIfNull(chunks);
        var copy = chunks.ToArray();
        var byId = new Dictionary<string, Chunk>(StringComparer.Ordinal);
        var nodeIds = document.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < copy.Length; index++)
        {
            var chunk = copy[index];
            if (chunk is null) throw new ArgumentException("Chunks cannot contain null.", nameof(chunks));
            if (!byId.TryAdd(chunk.ChunkId, chunk))
                throw new ArgumentException("Chunk IDs must be unique.", nameof(chunks));
            if (!StringComparer.Ordinal.Equals(chunk.DocumentId, document.DocumentId) || chunk.Sequence != index)
                throw new ArgumentException("Chunks must belong to the document and have contiguous ordered sequences.", nameof(chunks));
            if (chunk.SourceSpans.Any(span => span.End > document.Text.Length))
                throw new ArgumentException("Chunk spans must be within the canonical text.", nameof(chunks));
            if (chunk.SourceNodeIds.Any(id => !nodeIds.Contains(id)))
                throw new ArgumentException("Source node references must exist in the document.", nameof(chunks));
        }
        foreach (var chunk in copy)
        {
            if (chunk.ParentChunkId is not { } parentId) continue;
            if (!byId.TryGetValue(parentId, out var parent))
                throw new ArgumentException("Every parent chunk must exist in the result.", nameof(chunks));
            if ((long)parent.Level + 1 != chunk.Level)
                throw new ArgumentException("A child's level must be its parent's level plus one.", nameof(chunks));
        }
        // Strictly decreasing nonnegative levels along parent links make cycles impossible.
        DocumentId = document.DocumentId;
        RevisionId = document.RevisionId;
        VariantId = variantId;
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        Chunks = Array.AsReadOnly(copy);
        Warnings = Chunk.CopyStrings(warnings ?? []);
    }

    /// <summary>Identidad lógica del documento normalizado del que procede este resultado.</summary>
    public string DocumentId { get; }
    /// <summary>Identificador de la revisión normalizada usada como fuente, para distinguir resultados sobre versiones distintas del documento.</summary>
    public string RevisionId { get; }
    /// <summary>Identificador explícito de esta variante de chunking. Permite distinguir alternativas sobre una misma revisión; no se genera automáticamente.</summary>
    public string VariantId { get; }
    /// <summary>Identificador de la estrategia que produjo los chunks, suministrado al construir el resultado.</summary>
    public string StrategyId { get; }
    /// <summary>Versión declarada de la estrategia, conservada para distinguir resultados de implementaciones diferentes.</summary>
    public string StrategyVersion { get; }
    /// <summary>Chunks en el orden recibido, con secuencias contiguas desde cero. La colección se copia, es de solo lectura y puede estar vacía.</summary>
    public IReadOnlyList<Chunk> Chunks { get; }
    /// <summary>Advertencias informativas suministradas por el llamador, por ejemplo ausencia de texto. Se copian; el modelo no las genera automáticamente.</summary>
    public IReadOnlyList<string> Warnings { get; }
}
