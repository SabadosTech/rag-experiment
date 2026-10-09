# RagExperiment.Documents

Representación documental para la ingesta: texto extraído y canónico, identidad,
nodos estructurados, metadata y procedencia opcional.

## Estado

Implementa modelos inmutables en Models/: ParsedDocument, NormalizedDocument,
DocumentNode, DocumentNodeKind, TextSpan y SourceLocator. Los constructores validan
IDs, spans, orden y relaciones de los nodos. Las colecciones se copian y los
valores JSON se clonan.

Los spans son rangos UTF-16 sobre el texto de cada representación. Los nodos se
ordenan por inicio, admiten rangos anidados y relacionan padres mediante IDs.
Los localizadores opcionales apuntan al archivo fuente sin prometer un mapeo
completo entre texto extraído y normalizado.

## Responsabilidades y dependencias

Lectura EPUB y extracción XHTML están implementadas con VersOne.Epub 3.3.6 y
HtmlAgilityPack 1.13.0. La normalización y los lectores Markdown/TXT siguen pendientes.
La única referencia de proyecto es RagExperiment.Core; las dependencias de parsing
quedan encapsuladas dentro de Documents.

El chunking pertenece a RagExperiment.Chunking, que consume NormalizedDocument.
Documents no referencia Chunking. Indexación, retrieval y generación quedan fuera.

Ver el [diseño documental](../../docs/document-processing-design.md).

## Lectores extensibles

`IDocumentReader` recibe un `Stream`, `DocumentReadContext` y `CancellationToken`,
y devuelve `ParsedDocument`. El contexto contiene la identidad lógica y metadata
del corpus, copiada defensivamente. Los valores del corpus prevalecen por clave
sobre la metadata extraída, sin mezcla profunda. EPUB extrae `title`, `authors`
(array), `language` (primer idioma) y `description` cuando están disponibles.

`EpubDocumentReader` adapta VersOne sin exponer sus tipos. Un futuro lector
Markdown implementará el mismo contrato. `DocumentReaderResolver` selecciona
lectores por `FormatId`, ignorando mayúsculas y rechazando duplicados y formatos
desconocidos. La aplicación registra lectores e interpreta extensiones; la biblioteca
no deduce identidad, título ni formato desde rutas.

```csharp
IDocumentReader reader = new EpubDocumentReader();
await using var content = File.OpenRead(path);
ParsedDocument document = await reader.ReadAsync(
    content, new DocumentReadContext("libro-123"), cancellationToken);
```

El llamador conserva la propiedad del stream, que permanece abierto incluso ante
errores. EPUB requiere un stream legible y posicionable ubicado en cero. Para
repetir una lectura debe reposicionarse explícitamente.

## Extracción EPUB y límites

El lector recorre el spine bajo demanda, sin cargar imágenes/fuentes ni descargar
recursos externos. Cada recurso tiene una sección; las secciones XHTML explícitas
y headings h1–h6 crean la jerarquía interna. El índice no crea otra jerarquía y
puede estar ausente. Se omite el documento de navegación y los elementos nav,
script, style e imágenes. No se ejecuta CSS ni se reproduce la presentación visual.

El texto se emite una sola vez, con spans UTF-16 calculados durante la extracción:
doble salto entre bloques, salto por br/elemento de lista/fila, tabulador entre
celdas. Whitespace HTML se colapsa fuera de pre; pre preserva indentación y espacios
con finales de línea LF. Se decodifican entidades y se conserva texto de enlaces.
Los nodos de lista y tabla representan bloques completos, no cada celda o elemento.
Los IDs locales son deterministas según el recorrido. SourceLocator conserva el
recurso interno y el id XHTML cuando existe, sin inventar líneas o páginas.

EPUB corrupto, XHTML principal cifrado, recurso principal ausente/remoto y cuerpo
XHTML ausente producen DocumentReadException con la causa original. No se devuelve
un documento parcial. La ofuscación de fuentes no impide leer texto no cifrado.
Errores de argumentos, I/O y cancelación mantienen sus tipos. Cancelación se observa
entre operaciones de VersOne y durante la extracción; sus operaciones internas no
aceptan CancellationToken y pueden terminar antes de observarla.

El resultado aún es texto **extraído**, no NormalizedDocument. No incluye
normalización común, chunking, persistencia ni reconstrucción de capítulos desde
el índice. Markdown y TXT se incorporarán como nuevas estrategias de lectura.
