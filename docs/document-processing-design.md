# Contratos de lectura y chunking de documentos

## Estado y arquitectura

Los modelos documentales y de chunks, sus validaciones y el contrato de estrategia
están implementados. También están implementados IDocumentReader, EpubDocumentReader
y DocumentReaderResolver. Normalizadores, lectores Markdown/TXT y algoritmos de
chunking siguen pendientes.

**Archivo → lectura → ParsedDocument → normalización → NormalizedDocument → chunking → ChunkingResult**

- RagExperiment.Documents contiene la representación documental, lectura EPUB y futura normalización.

- RagExperiment.Chunking depende de Documents y contiene los modelos de chunks e IChunkingStrategy.

Documents no depende de Chunking.
Estos modelos permanecen en sus respectivas bibliotecas;
Core no recibe tipos anticipados.
La aplicación de ingesta será la raíz de composición.

## Modelos implementados

| Modelo | Datos y significado |
| --- | --- |
| ParsedDocument | Identificador lógico, texto extraído, nodos ordenados y metadata. |
| NormalizedDocument | Identificador lógico, revisión explícita, texto canónico, nodos actualizados y metadata. |
| DocumentNode | ID, tipo, padre opcional, span y localizador de origen opcional. |
| DocumentNodeKind | Documento, sección, heading, párrafo, sentencia, lista, tabla, código u otro. |
| TextSpan | Inicio inclusivo y longitud en unidades UTF-16; fin exclusivo. |
| SourceLocator | Líneas originales y recurso/ancla opcionales. |
| Chunk | ID, documento, secuencia, texto canónico, nivel, padre opcional, nodos fuente, spans, headings y metadata. |
| ChunkingResult | Documento, revisión, variante, estrategia y versión, chunks ordenados y advertencias. |

Los tipos tienen propiedades de solo lectura y copian las colecciones recibidas.
La metadata es un diccionario de JsonElement clonados: admite valores JSON y
permanece válida tras liberar el JsonDocument original. Identidad y procedencia
son campos separados; la metadata no los reemplaza.

### Estructura y posiciones

Los nodos se almacenan en una colección con referencias al padre por ID. Se ordenan
por inicio del span, admitiendo posiciones iguales y spans anidados. Los padres
deben existir; los IDs son únicos y no se permiten ciclos. No se exige que el padre
aparezca antes que el hijo ni se agrega inferencia estructural.

En ParsedDocument los spans apuntan al texto extraído. En NormalizedDocument y los
chunks apuntan al texto canónico. El normalizador futuro deberá actualizar las
posiciones. Los rangos no son offsets de bytes ni índices de tokens o grafemas.
No existe un mapeo completo entre texto extraído y normalizado.

SourceLocator conserva líneas de TXT/Markdown o recurso interno y ancla de EPUB
cuando estén disponibles. Las líneas son inclusivas y comienzan en uno; una línea
final requiere una inicial y no puede precederla. No se prometen páginas.

El texto del chunk es canónico; los headings acompañan como contexto y no se
insertan automáticamente. Los spans pueden referirse a varios rangos. No se
impone concatenación literal ni cobertura completa del documento en estos modelos:
esas políticas se definirán con los algoritmos.

### Identidad y jerarquía

Documento lógico, revisión, variante y chunk tienen identidades explícitas
suministradas por el llamador. No hay hashing ni generación automática.
La identidad lógica no se deriva de una ruta de archivo.

Sequence es global dentro del resultado, comienza en cero y es contigua. Los
chunks raíz tienen Level cero y ningún padre. Cada hijo referencia un chunk del
mismo resultado y tiene el nivel del padre más uno. Esto impide ciclos sin
implementar una estrategia jerárquica ni parent-child retrieval.

El constructor de ChunkingResult recibe un NormalizedDocument para validar
pertenencia, referencias a nodos y spans dentro del texto. Conserva DocumentId y
RevisionId, sin retener el documento completo. No reordena los chunks recibidos.
Resultados vacíos son válidos; las advertencias son explícitas, no automáticas.
Las colecciones de referencias y spans pueden estar vacías; los algoritmos futuros
definirán sus requisitos de cobertura y trazabilidad.

## Contrato implementado

IChunkingStrategy expone StrategyId, StrategyVersion y:

```csharp
Task<ChunkingResult> ChunkAsync(
    NormalizedDocument document,
    CancellationToken cancellationToken = default);
```

Las estrategias locales podrán devolver tareas completadas sin Task.Run. El mismo
contrato permitirá operaciones asíncronas futuras. La configuración se inyectará
por constructor en cada implementación; no existe una bolsa genérica de opciones.
Las implementaciones deberán respetar la cancelación.

Todavía no hay estrategias ejecutables, registro AddChunking, contenedor DI propio
ni contratos implementados de normalizadores. El contrato de lectura se describe abajo.

## Estrategias previstas

| Estrategia futura | Dependencias conceptuales |
| --- | --- |
| FixedTokenChunker | Mecanismo de tokenización y opciones; ambos postergados por completo. |
| RecursiveChunker | Límites progresivos de estructura/texto; política de tamaños pendiente. |
| StructureAwareChunker | Tipos de nodos, jerarquía y spans documentales. |
| SemanticBreakpointChunker | Servicio futuro mediante abstracción independiente de proveedor. |

Se documentan sin clases incompletas, opciones vacías, thresholds, tamaños ni
overlaps. No se crea una abstracción de tokenización ni una abstracción semántica.
El contrato central no depende de OpenAI, Microsoft.Extensions.AI, Qdrant o Lucene.

## Lectura y normalización futuras

Se conserva como referencia el objetivo de trabajar con archivos locales TXT,
EPUB y Markdown. EPUB ya usa VersOne.Epub 3.3.6 y HtmlAgilityPack 1.13.0 dentro
de Documents; TXT y Markdown quedan pendientes.
IDocumentNormalizer sigue siendo una propuesta para otra etapa;
el anterior IDocumentChunker/ChunkSet se reemplaza por IChunkingStrategy/ChunkingResult.

- TXT: conservar párrafos sin inferir capítulos ni headings.
- Markdown: conservar headings, jerarquía, párrafos, listas y código; conservar
  destinos de enlaces e imágenes sin cargar recursos externos.
- EPUB: conservar orden de lectura, capítulos, headings y metadata; excluir
  imágenes y navegación del contenido principal.
- Normalización: limpieza conservadora, preservando acentos, puntuación,
  mayúsculas e indentación significativa. No aplicar stemming ni stopwords.

Título, autores, idioma y etiquetas podrán conservarse en metadata. Para los
futuros lectores se mantiene la intención de dar precedencia a los datos del corpus
sobre los extraídos, separando procedencia del procesamiento de atributos personalizados.
El lector EPUB combina metadata por clave: los datos del corpus prevalecen sobre
los extraídos, sin mezcla profunda.

La futura reproducibilidad distinguirá documento, revisión y variante; cambiar
estrategia, versión o parámetros deberá permitir conservar variantes comparables.
La generación determinista de IDs, configuración efectiva y persistencia se
definirán con las implementaciones, sin usar fechas para determinar identidad.

Los futuros tests de procesamiento deberán comprobar conservación de Unicode,
estructura y procedencia, cobertura del texto y límites de las estrategias.
En lotes se prevé continuar ante fallos individuales y reportar formatos no
admitidos, archivos ilegibles o EPUB corruptos/protegidos. El procesamiento en lotes
todavía no se implementa; la lectura individual EPUB ya reporta esos errores.

## Validación de esta etapa y límites

Las pruebas xUnit construyen modelos en memoria y comprueban identidad, orden,
jerarquía, spans UTF-16, localizadores, metadata JSON e inmutabilidad.
También se prueban EPUB generados localmente, extracción XHTML, errores, resolver
extensible y lectura de archivos desde Ingestion. No requieren infraestructura externa.

Quedan pendientes parsing Markdown/TXT, normalización ejecutable, algoritmos, opciones de chunking,
tokenización, embeddings, indexación, retrieval, enriquecimiento, generación y
evaluación RAG. No se crean interfaces para esos subsistemas.

## Lectura EPUB implementada

IDocumentReader expone FormatId y ReadAsync(Stream, DocumentReadContext,
CancellationToken), que devuelve Task<ParsedDocument>. La aplicación decide ruta,
apertura, formato e identidad lógica. DocumentReadContext copia metadata del corpus.
DocumentReaderResolver recibe lectores por constructor, selecciona por FormatId sin
distinguir mayúsculas y rechaza duplicados/desconocidos. Un lector Markdown o un
adaptador EPUB alternativo puede sustituirse sin modificar los modelos o chunkers.

EpubDocumentReader usa OpenBookAsync y GetReadingOrderAsync y carga solo XHTML
necesario. El stream debe ser legible y posicionable, ubicado en cero; pertenece al
llamador y queda abierto ante éxito o fallo. No descarga recursos externos.

Cada recurso genera una sección. Secciones XHTML explícitas y headings h1–h6
forman jerarquías locales; headings cierran ante otro de nivel igual o superior
dentro del mismo contenedor. El índice no crea otra jerarquía y puede faltar.
El lector omite documentos/elementos de navegación, imágenes, scripts y estilos.
Representa párrafos, headings, listas, tablas y pre como nodos, con texto único
y spans calculados al emitirlo. Los IDs locales dependen del recorrido, no de fechas
ni rutas físicas. La procedencia conserva recurso interno e id XHTML disponible.

Whitespace HTML se colapsa fuera de pre; bloques se separan con dos LF, br con LF,
elementos de lista/filas con LF y celdas con tabuladores. Pre conserva indentación
y normaliza finales de línea a LF. Esto es extracción; aún requiere normalización
común antes del chunking. No interpreta CSS ni reproduce una página renderizada.

El lector falla sin resultado parcial ante fuente corrupta, contenido principal
ausente/remoto/cifrado o XHTML sin body. DocumentReadException conserva la causa;
argumentos, I/O y cancelación mantienen sus tipos. La cancelación se comprueba entre
llamadas a VersOne y durante extracción; no interrumpe una llamada interna sin token.
La metadata extraída usa title, authors (array), language (primer idioma) y description.
Consultar los README de Documents e Ingestion para ejemplos ejecutables.
