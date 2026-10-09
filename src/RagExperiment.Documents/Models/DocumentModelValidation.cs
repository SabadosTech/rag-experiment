using System.Collections.ObjectModel;
using System.Text.Json;

namespace RagExperiment.Documents.Models;

/// <summary>Validaciones y copias defensivas compartidas por las representaciones documentales.</summary>
internal static class DocumentModelValidation
{
    /// <summary>Copia nodos y valida límites, orden, unicidad, existencia de padres y ausencia de ciclos.</summary>
    /// <param name="text">Texto sobre el que se interpretan todos los spans.</param>
    /// <param name="nodes">Nodos en el orden recibido; no se reordenan ni se infiere jerarquía.</param>
    /// <returns>Colección de solo lectura independiente de la colección original.</returns>
    internal static IReadOnlyList<DocumentNode> CopyNodes(string text, IEnumerable<DocumentNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var copy = nodes.ToArray();
        var byId = new Dictionary<string, DocumentNode>(StringComparer.Ordinal);
        var previousStart = 0;
        foreach (var node in copy)
        {
            if (node is null) throw new ArgumentException("Nodes cannot contain null.", nameof(nodes));
            if (!byId.TryAdd(node.NodeId, node))
                throw new ArgumentException("Node IDs must be unique.", nameof(nodes));
            if (node.Span.End > text.Length || node.Span.Start < previousStart)
                throw new ArgumentException("Node spans must be in text bounds and ordered by start.", nameof(nodes));
            previousStart = node.Span.Start;
        }
        foreach (var node in copy)
            if (node.ParentNodeId is { } parent && !byId.ContainsKey(parent))
                throw new ArgumentException("Every parent must exist in the document.", nameof(nodes));

        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in copy)
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            var current = node;
            while (!visited.Contains(current.NodeId))
            {
                if (!path.Add(current.NodeId))
                    throw new ArgumentException("Node parent relationships cannot contain cycles.", nameof(nodes));
                if (current.ParentNodeId is null) break;
                current = byId[current.ParentNodeId];
            }
            visited.UnionWith(path);
        }
        return Array.AsReadOnly(copy);
    }

    /// <summary>Clona valores JSON y evita cambios posteriores desde el diccionario original.</summary>
    /// <param name="metadata">Atributos con claves no vacías y JSON definido; null produce un diccionario vacío.</param>
    /// <returns>Diccionario de solo lectura válido incluso si se libera el JsonDocument original.</returns>
    internal static IReadOnlyDictionary<string, JsonElement> CopyMetadata(
        IReadOnlyDictionary<string, JsonElement>? metadata)
    {
        var copy = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (metadata is not null)
            foreach (var (key, value) in metadata)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(key);
                if (value.ValueKind == JsonValueKind.Undefined)
                    throw new ArgumentException("Metadata values must be valid JSON.", nameof(metadata));
                copy.Add(key, value.Clone());
            }
        return new ReadOnlyDictionary<string, JsonElement>(copy);
    }
}
