namespace RagExperiment.Documents.Models;

/// <summary>Rango de texto [Start, End), medido en unidades UTF-16 de un string de .NET.</summary>
public readonly record struct TextSpan
{
    /// <summary>Crea un rango no negativo; el documento valida que el fin no exceda su texto.</summary>
    /// <param name="start">Índice inicial inclusivo desde cero, en unidades UTF-16, no bytes ni tokens.</param>
    /// <param name="length">Longitud UTF-16 no negativa; cero representa un rango vacío.</param>
    public TextSpan(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length > int.MaxValue - start)
            throw new ArgumentOutOfRangeException(nameof(length), "The end must fit in an Int32.");
        Start = start;
        Length = length;
    }

    /// <summary>Posición inicial inclusiva, basada en cero y medida en unidades UTF-16 de .NET sobre el texto correspondiente.</summary>
    public int Start { get; }
    /// <summary>Cantidad de unidades UTF-16 del rango. Puede ser cero; no representa tokens ni caracteres Unicode completos.</summary>
    public int Length { get; }
    /// <summary>Posición final exclusiva, calculada como Start + Length. El rango abarca desde Start hasta antes de End.</summary>
    public int End => Start + Length;
}
