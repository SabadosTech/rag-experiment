namespace RagExperiment.Documents.Models;

/// <summary>Clasifica la función estructural de un nodo; no determina cómo se dividirá en chunks.</summary>
public enum DocumentNodeKind
{
    /// <summary>Raíz que representa el documento completo.</summary>
    Document,
    /// <summary>Contenedor de contenido, como capítulo, recurso XHTML o sección abierta por un heading.</summary>
    Section,
    /// <summary>Texto de un encabezado; su sección contenedora puede incluir los párrafos posteriores.</summary>
    Heading,
    /// <summary>Bloque de prosa o texto significativo fuera de otros bloques reconocidos.</summary>
    Paragraph,
    /// <summary>Oración individual; el lector EPUB actual no segmenta párrafos en oraciones.</summary>
    Sentence,
    /// <summary>Lista completa; sus elementos se representan en el texto con saltos de línea.</summary>
    List,
    /// <summary>Tabla completa, con separadores de filas y celdas en el texto extraído.</summary>
    Table,
    /// <summary>Bloque de código o texto preformateado con espacios e indentación significativos.</summary>
    Code,
    /// <summary>Unidad que no corresponde a los tipos anteriores.</summary>
    Other
}
