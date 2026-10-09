using RagExperiment.Chunking.Models;
using RagExperiment.Documents.Models;

namespace RagExperiment.Chunking.Abstractions;

/// <summary>A provider-independent strategy; implementations receive configuration by constructor.</summary>
public interface IChunkingStrategy
{
    string StrategyId { get; }
    string StrategyVersion { get; }

    Task<ChunkingResult> ChunkAsync(
        NormalizedDocument document,
        CancellationToken cancellationToken = default);
}
