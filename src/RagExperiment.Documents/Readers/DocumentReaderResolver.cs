using RagExperiment.Documents.Abstractions;

namespace RagExperiment.Documents.Readers;

/// <summary>Selecciona una estrategia registrada por formato, sin depender de bibliotecas o extensiones de archivo.</summary>
public sealed class DocumentReaderResolver
{
    private readonly Dictionary<string, IDocumentReader> readers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Construye el registro y rechaza claves duplicadas, incluso si solo difieren en mayúsculas.</summary>
    /// <param name="readers">Lectores suministrados por la aplicación; debe existir a lo sumo uno por FormatId.</param>
    public DocumentReaderResolver(IEnumerable<IDocumentReader> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);
        foreach (var reader in readers)
        {
            ArgumentNullException.ThrowIfNull(reader);
            ArgumentException.ThrowIfNullOrWhiteSpace(reader.FormatId);
            if (!this.readers.TryAdd(reader.FormatId, reader))
                throw new ArgumentException($"Multiple readers are registered for '{reader.FormatId}'.", nameof(readers));
        }
    }

    /// <summary>Obtiene el lector registrado o falla si el formato no está soportado.</summary>
    /// <param name="formatId">Clave no vacía como epub; la comparación ignora mayúsculas y no interpreta rutas.</param>
    /// <returns>Instancia registrada para el formato solicitado.</returns>
    public IDocumentReader Resolve(string formatId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formatId);
        return readers.TryGetValue(formatId, out var reader)
            ? reader : throw new NotSupportedException($"Document format '{formatId}' is not supported.");
    }
}
