namespace RagExperiment.Documents.Models;

/// <summary>Coordenadas opcionales en la fuente original; complementan el span del texto procesado.</summary>
public sealed class SourceLocator
{
    /// <summary>Crea una referencia de procedencia sin cargar el recurso ni calcular posiciones de texto.</summary>
    /// <param name="startLine">Línea inicial inclusiva desde uno, o null si se desconoce; normalmente null en EPUB.</param>
    /// <param name="endLine">Línea final inclusiva; requiere startLine y no puede ser menor que ella.</param>
    /// <param name="resource">Ruta del recurso original, por ejemplo OPS/chapter.xhtml dentro del EPUB, o null.</param>
    /// <param name="anchor">ID del elemento original dentro del recurso, sin el prefijo #, o null si no existe.</param>
    public SourceLocator(int? startLine = null, int? endLine = null,
        string? resource = null, string? anchor = null)
    {
        if (startLine is <= 0) throw new ArgumentOutOfRangeException(nameof(startLine));
        if (endLine is <= 0) throw new ArgumentOutOfRangeException(nameof(endLine));
        if (endLine.HasValue && (!startLine.HasValue || endLine.Value < startLine.Value))
            throw new ArgumentException("An end line requires a start line and cannot precede it.", nameof(endLine));
        if (resource is not null) ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        if (anchor is not null) ArgumentException.ThrowIfNullOrWhiteSpace(anchor);
        StartLine = startLine;
        EndLine = endLine;
        Resource = resource;
        Anchor = anchor;
    }

    /// <summary>Primera línea de la unidad en la fuente original, inclusiva y numerada desde uno. Es null si no se conoce.</summary>
    public int? StartLine { get; }
    /// <summary>Última línea inclusiva en la fuente original. Es opcional; si se indica, requiere StartLine y no puede precederla.</summary>
    public int? EndLine { get; }
    /// <summary>Recurso de origen opcional, por ejemplo el archivo interno de un EPUB. No implica cargar ese recurso.</summary>
    public string? Resource { get; }
    /// <summary>Ancla opcional que identifica una ubicación en la fuente, por ejemplo el ID de un elemento dentro de un recurso EPUB.</summary>
    public string? Anchor { get; }
}
