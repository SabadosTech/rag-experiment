namespace RagExperiment.Documents.Models;

/// <summary>Unidad estructural que referencia texto del documento mediante un span, sin duplicarlo.</summary>
public sealed class DocumentNode
{
    /// <summary>Crea un nodo; el documento contenedor valida sus límites y relaciones con los demás nodos.</summary>
    /// <param name="nodeId">ID no vacío, único dentro del documento; no necesita coincidir con el id XHTML original.</param>
    /// <param name="kind">Tipo de unidad: sección, heading, párrafo, lista, tabla o código, entre otros.</param>
    /// <param name="span">Rango UTF-16 sobre Text del documento contenedor; no sobre el archivo o el XHTML.</param>
    /// <param name="parentNodeId">ID del padre en la misma colección, o null para un nodo sin padre.</param>
    /// <param name="source">Procedencia original opcional, independiente de las posiciones del texto extraído.</param>
    public DocumentNode(string nodeId, DocumentNodeKind kind, TextSpan span,
        string? parentNodeId = null, SourceLocator? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (parentNodeId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(parentNodeId);
        if (StringComparer.Ordinal.Equals(nodeId, parentNodeId))
            throw new ArgumentException("A node cannot be its own parent.", nameof(parentNodeId));
        NodeId = nodeId;
        Kind = kind;
        Span = span;
        ParentNodeId = parentNodeId;
        Source = source;
    }

    /// <summary>Identificador del nodo, único dentro del documento. Permite referenciarlo desde otros nodos y chunks.</summary>
    public string NodeId { get; }
    /// <summary>Tipo de unidad documental representada, por ejemplo heading, párrafo, tabla o código.</summary>
    public DocumentNodeKind Kind { get; }
    /// <summary>Rango del nodo sobre el texto del documento que lo contiene: extraído en ParsedDocument o canónico en NormalizedDocument.</summary>
    public TextSpan Span { get; }
    /// <summary>Identificador del nodo padre dentro del mismo documento. Es null cuando el nodo no tiene padre.</summary>
    public string? ParentNodeId { get; }
    /// <summary>Localización opcional en la fuente original. Complementa el span sin establecer un mapeo completo entre textos.</summary>
    public SourceLocator? Source { get; }
}
