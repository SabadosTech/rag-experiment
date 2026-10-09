using System.Runtime.ExceptionServices;

namespace RagExperiment.Documents.Readers.Epub;

/// <summary>Permite cerrar el adaptador sin cerrar el stream original y conserva fallos de la fuente para relanzarlos.</summary>
/// <param name="inner">Stream del llamador al que se delegan lectura y posicionamiento; no se libera con este wrapper.</param>
internal sealed class LeaveOpenStream(Stream inner) : Stream
{
    /// <summary>Último error real de I/O o cancelación de la fuente, con su stack original; null si no ocurrió ninguno.</summary>
    public ExceptionDispatchInfo? SourceFailure { get; private set; }
    /// <summary>Indica si el stream original permite leer.</summary>
    public override bool CanRead => inner.CanRead;
    /// <summary>Indica si el stream original permite cambiar de posición, requisito de lectura ZIP.</summary>
    public override bool CanSeek => inner.CanSeek;
    /// <summary>Siempre false: el lector EPUB no puede modificar el contenido original.</summary>
    public override bool CanWrite => false;
    /// <summary>Longitud del EPUB en bytes; no es la longitud UTF-16 del documento extraído.</summary>
    public override long Length => ReadSource(() => inner.Length);
    /// <summary>Posición actual en bytes del stream original; los spans documentales usan otras coordenadas.</summary>
    public override long Position { get => ReadSource(() => inner.Position); set => ReadSource(() => inner.Position = value); }
    /// <summary>Delega Flush al stream original; no habilita operaciones de escritura.</summary>
    public override void Flush() => inner.Flush();
    /// <summary>Lee bytes y conserva posibles fallos de la fuente sin reinterpretarlos como corrupción EPUB.</summary>
    /// <param name="buffer">Array de destino para los bytes leídos.</param>
    /// <param name="offset">Índice inicial dentro de buffer, no posición dentro del EPUB.</param>
    /// <param name="count">Cantidad máxima de bytes solicitada; el retorno indica cuántos se leyeron.</param>
    public override int Read(byte[] buffer, int offset, int count) => ReadSource(() => inner.Read(buffer, offset, count));
    /// <summary>Lee bytes directamente en un span y conserva los errores originales de la fuente.</summary>
    /// <param name="buffer">Memoria de destino cuya longitud limita la lectura.</param>
    public override int Read(Span<byte> buffer)
    {
        try { return inner.Read(buffer); }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            SourceFailure = ExceptionDispatchInfo.Capture(exception);
            throw;
        }
    }
    /// <summary>Adapta la lectura asíncrona de array a la variante basada en Memory.</summary>
    /// <param name="buffer">Array de destino.</param>
    /// <param name="offset">Índice inicial de la región de destino.</param>
    /// <param name="count">Cantidad máxima de bytes que pueden escribirse en esa región.</param>
    /// <param name="cancellationToken">Cancelación propagada a la lectura del stream original.</param>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <summary>Lee bytes de forma asíncrona y conserva fallos de I/O o cancelación con su stack.</summary>
    /// <param name="buffer">Memoria de destino; su longitud es el máximo de bytes solicitado.</param>
    /// <param name="cancellationToken">Cancelación de esta operación de lectura.</param>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try { return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            SourceFailure = ExceptionDispatchInfo.Capture(exception);
            throw;
        }
    }
    /// <summary>Cambia la posición; una solicitud anterior al inicio se considera corrupción del ZIP.</summary>
    /// <param name="offset">Desplazamiento en bytes respecto del origen indicado; puede ser negativo si el resultado es válido.</param>
    /// <param name="origin">Referencia del desplazamiento: inicio, posición actual o fin del stream.</param>
    /// <returns>Nueva posición absoluta en bytes.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        var start = origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => Position,
            SeekOrigin.End => Length,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        // A malformed ZIP can request a negative position. This is invalid archive data,
        // rather than a failure of the caller's storage device.
        if (offset < -start) throw new InvalidDataException("ZIP requested a position before the beginning of the source.");
        return ReadSource(() => inner.Seek(offset, origin));
    }
    /// <summary>Rechaza modificar el tamaño del archivo porque el adaptador es de solo lectura.</summary>
    /// <param name="value">Longitud solicitada en bytes; nunca se aplica.</param>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <summary>Rechaza escribir para proteger el contenido original.</summary>
    /// <param name="buffer">Bytes que se intentarían escribir; no se consumen.</param>
    /// <param name="offset">Índice inicial en buffer; no se aplica.</param>
    /// <param name="count">Cantidad solicitada de bytes; no se escribe ninguno.</param>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Delega una operación y captura solo errores reales de I/O o cancelación para su posterior relanzamiento.</summary>
    /// <typeparam name="T">Tipo del resultado de la operación delegada.</typeparam>
    /// <param name="operation">Lectura o acceso a posición/longitud del stream original.</param>
    private T ReadSource<T>(Func<T> operation)
    {
        try { return operation(); }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            SourceFailure = ExceptionDispatchInfo.Capture(exception);
            throw;
        }
    }
}
