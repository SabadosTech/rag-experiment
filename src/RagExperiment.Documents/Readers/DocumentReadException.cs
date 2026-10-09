namespace RagExperiment.Documents.Readers;

/// <summary>La fuente no pudo interpretarse como documento admitido; conserva la causa para diagnóstico.</summary>
/// <param name="message">Descripción del error de interpretación y del documento afectado.</param>
/// <param name="innerException">Error original del parser, del archivo comprimido o de la validación de formato.</param>
public sealed class DocumentReadException(string message, Exception innerException)
    : Exception(message, innerException);
