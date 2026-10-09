# RagExperiment.Chunking

Contratos y modelos de chunking independientes de indexación, retrieval y proveedores.

## Estado y flujo

**ParsedDocument → NormalizedDocument → IChunkingStrategy → ChunkingResult**

Implementa Chunk, ChunkingResult e IChunkingStrategy. Los algoritmos están pendientes.
Su única referencia directa es RagExperiment.Documents. Documents no depende de
Chunking y los modelos de chunks permanecen en esta biblioteca.

## Contratos

IChunkingStrategy declara StrategyId, StrategyVersion y ChunkAsync, que recibe
NormalizedDocument y CancellationToken y devuelve Task<ChunkingResult>.
La configuración de cada futura estrategia se recibirá por constructor.
Las estrategias locales podrán devolver tareas completadas.

Chunk conserva texto canónico, identidad, secuencia, padre/nivel, nodos fuente,
spans UTF-16, HeadingPath y metadata JSON. Los headings no se insertan en el texto.
Las propiedades son de solo lectura; se copian colecciones y se clonan valores JSON.

ChunkingResult recibe el documento normalizado para validar spans y nodos fuente;
solo conserva sus IDs de documento y revisión. Además registra variante, estrategia,
versión, chunks y advertencias. No ordena ni genera IDs automáticamente.
La secuencia global comienza en cero y debe ser contigua. Un padre debe pertenecer
al resultado; el nivel de un hijo es el del padre más uno.

Los IDs de documento lógico, revisión procesada, variante y chunk se suministran
explícitamente. Hashing y generación determinista quedan pendientes.

## Estrategias previstas

| Estrategia | Preparación futura |
| --- | --- |
| FixedTokenChunker | Tokenización y configuración cuando se implemente; totalmente postergadas ahora. |
| RecursiveChunker | Límites progresivos; opciones concretas pendientes. |
| StructureAwareChunker | Nodos, tipos, jerarquía y spans documentales. |
| SemanticBreakpointChunker | Dependencia mediante una abstracción independiente de proveedor, aún sin crear. |

No hay clases incompletas, opciones vacías ni registro AddChunking. La biblioteca
no crea contenedores DI ni agrega infraestructura de logging.

## Fuera de alcance

Parsing, normalización ejecutable, algoritmos, tokenización, embeddings, Qdrant,
Lucene/BM25, retrieval, reranking, generación, concepts, propositions y evaluación RAG.
La representación padre/hijo no implementa chunking jerárquico ni retrieval.

Ver el [diseño documental](../../docs/document-processing-design.md).
