# Estado del arte del chunking para RAG — Octubre 2026

**Fecha de revisión:** 6 de octubre de 2026  
**Contexto:** Documento de referencia para `rag-experiment`

## 1. Resumen ejecutivo

El estado del arte del *chunking* para Retrieval-Augmented Generation (RAG) ya no puede describirse como una evolución lineal desde *fixed-size chunking* hacia *semantic chunking*.

La tendencia actual es:

> **structure-aware segmentation + contextualización + recuperación multi-granular o jerárquica + selección adaptativa según documento y consulta**

Una conclusión especialmente relevante de la literatura reciente es que el *semantic chunking* basado solamente en similitud entre embeddings de oraciones no demuestra una superioridad consistente frente a estrategias más simples. En NAACL 2025, Qu et al. evaluaron *semantic chunking* frente a *fixed-size chunking* sobre recuperación de documentos, recuperación de evidencia y generación basada en retrieval, concluyendo que el costo computacional adicional no produce beneficios suficientemente consistentes como para asumirlo como opción superior por defecto [1].

En 2026, una evaluación más amplia de estrategias de chunking volvió a mostrar que el método óptimo depende del escenario de retrieval. En *in-corpus retrieval*, estrategias simples basadas en estructura pueden superar métodos guiados por LLM, mientras que técnicas como LumberChunker pueden funcionar mejor en escenarios de búsqueda dentro de un documento largo [2].

Por lo tanto, la pregunta relevante ya no es únicamente:

> ¿Cuál es el tamaño óptimo de chunk?

sino:

> ¿Cuál debe ser la unidad de segmentación, la unidad de indexación, la unidad de recuperación y la unidad de contexto enviada al modelo para cada tipo de documento y consulta?

---

## 2. El cambio conceptual: segmentación, indexación, retrieval y contexto ya no son necesariamente la misma unidad

En un RAG clásico se utiliza el mismo fragmento de texto para todas las etapas:

```text
Document
   ↓
Chunk
   ↓
Embedding
   ↓
Vector DB
   ↓
Retrieval
   ↓
LLM
```

El `Chunk` funciona simultáneamente como:

- unidad de segmentación;
- unidad de embedding;
- unidad de retrieval;
- unidad de contexto para generación.

La investigación reciente tiende a desacoplar estas funciones.

Un modelo más general es:

```text
Document
   │
   ├─ Atomic units
   │     sentences / paragraphs / sections / propositions
   │
   ├─ Retrieval representations
   │     dense vectors / BM25 / contextualized representations
   │
   └─ Generation context
         parent section / neighboring chunks /
         dynamically assembled context
```

FreeChunker, HiChunk y los sistemas *parent-child* son ejemplos claros de esta tendencia [3][4][5].

Este desacoplamiento permite optimizar cada etapa de manera independiente.

---

## 3. Fixed-size chunking continúa siendo un baseline relevante

El *fixed-size chunking* no está obsoleto. Sigue siendo necesario como baseline porque:

- es simple;
- es determinista;
- tiene costo bajo;
- permite estudiar aisladamente el impacto del tamaño del chunk;
- en varios benchmarks compite favorablemente con técnicas más complejas.

Configuraciones típicas:

```text
256 tokens
512 tokens
1024 tokens
```

preferentemente evitando, cuando sea posible, cortes arbitrarios dentro de oraciones.

No existe un tamaño óptimo universal.

Bhat et al. analizaron diferentes tamaños de chunk y modelos de embeddings y encontraron que preguntas factuales o con respuestas concisas pueden favorecer chunks pequeños, aproximadamente de 64–128 tokens, mientras que tareas que requieren más contexto pueden favorecer tamaños de 512–1024 tokens. También observaron que diferentes modelos de embeddings presentan sensibilidades distintas al tamaño del chunk [6].

Otro estudio industrial presentado en EACL 2026 encontró, para un corpus concreto de artículos de soporte, mejoras de Recall@10 y Recall@50 al aumentar el tamaño de chunk desde 512 hasta 4096 tokens [7]. Esto no implica que 4096 tokens sea un tamaño recomendado en general; demuestra que el tamaño óptimo depende del corpus, del tipo de pregunta y del retriever.

Por lo tanto:

> **`chunk_size` debe tratarse como un hiperparámetro experimental del sistema, no como una constante arquitectónica.**

---

## 4. Recursive y boundary-aware chunking

El *recursive chunking* continúa siendo una estrategia práctica.

La idea es intentar dividir el contenido usando separadores progresivamente más finos:

```text
section
    ↓
paragraph
    ↓
sentence
    ↓
tokens / characters
```

En lugar de dividir cada `N` tokens independientemente de la estructura, se intenta preservar unidades naturales siempre que no excedan el tamaño máximo.

Es una estrategia especialmente adecuada como baseline porque combina:

- bajo costo;
- implementación sencilla;
- mejor preservación estructural que el corte estrictamente fijo.

Sin embargo, no debe confundirse con *semantic chunking*: el recursive splitter utiliza reglas estructurales o sintácticas, no similitud semántica entre embeddings.

---

## 5. Structure-aware chunking

Una de las tendencias más claras consiste en preservar la estructura explícita del documento:

```text
Document
 ├─ Section
 │   ├─ Subsection
 │   │   ├─ Paragraph
 │   │   ├─ List
 │   │   ├─ Table
 │   │   └─ Code block
```

Los límites documentales tienen prioridad y el tamaño máximo funciona como restricción secundaria.

AutoChunker, presentado en ACL 2025, utiliza una representación estructurada del documento y conserva la jerarquía mediante una estructura en árbol. Además propone evaluar los chunks considerando reducción de ruido, completitud, coherencia contextual, relevancia para la tarea y retrieval [8].

Microsoft también utiliza actualmente un enfoque de este tipo en Azure AI Search. Su *Document Layout skill* detecta estructura documental y puede producir chunks basados en encabezados, párrafos y oraciones, preservando además información jerárquica de headings para indexación [9].

Para documentos como:

- Markdown;
- HTML;
- documentación técnica;
- libros;
- papers;
- manuales;
- especificaciones;

**structure-aware chunking constituye un baseline más razonable que cortar exclusivamente por cantidad de tokens.**

---

## 6. Semantic chunking: útil, pero no superior por defecto

El semantic chunking clásico suele funcionar aproximadamente así:

```text
sentence embeddings
        ↓
similarity(Si, Si+1)
        ↓
detect semantic discontinuity
        ↓
chunk boundary
```

La intuición es correcta: cuando la similitud semántica entre segmentos consecutivos cae, posiblemente exista un cambio de tema.

Sin embargo, los resultados recientes obligan a ser cautelosos.

Qu et al., en NAACL 2025, compararon semantic chunking con fixed-size chunking y concluyeron que el incremento de costo no se traduce en mejoras suficientemente consistentes en retrieval y answer generation [1].

La evaluación de Zhou et al. en 2026 vuelve a mostrar que estrategias simples basadas en estructura pueden superar métodos más sofisticados para *in-corpus retrieval* [2].

Por lo tanto, semantic chunking debería considerarse:

> **una estrategia experimental que debe demostrar mejoras sobre el corpus real, no la opción avanzada por defecto.**

En `rag-experiment` debería implementarse porque sirve como comparación importante, pero no debería condicionar la arquitectura central.

---

## 7. Contextual Retrieval / contextualized chunks

El chunking puede destruir contexto.

Por ejemplo:

```text
It increased by 17% compared with the previous version.
```

El fragmento aislado no indica:

- qué aumentó;
- qué producto o sistema se describe;
- qué versión;
- de qué documento proviene.

Anthropic propuso *Contextual Retrieval*, donde cada chunk recibe una breve contextualización generada a partir del documento completo antes de ser indexado [10].

Conceptualmente:

```text
Context:
This section discusses SQL Server 2025 Resource Governor
changes to I/O governance.

Chunk:
It increased by 17% compared with the previous version.
```

El texto contextualizado se utiliza tanto:

- para generar embeddings;
- como para construir el índice léxico/BM25.

Anthropic reportó, en sus propios experimentos, una reducción de la tasa de fallos de retrieval@20 del 5,7 % al 3,7 % utilizando contextual embeddings, hasta 2,9 % al combinar contextual embeddings con Contextual BM25 y hasta 1,9 % agregando reranking [10].

Estos números corresponden a experimentos de Anthropic y no deben interpretarse como resultados universales.

Arquitectónicamente conviene conservar representaciones separadas:

```text
RawChunk
ContextualizedChunk
EmbeddingText
SearchText
```

Esto evita modificar irreversiblemente el contenido original.

---

## 8. Late Chunking

Late Chunking cambia el orden tradicional entre chunking y embedding.

### Pipeline tradicional

```text
document
   ↓ split
chunks
   ↓ embedding
chunk embeddings
```

### Late Chunking

```text
document
   ↓ long-context embedding model
token representations
   ↓ chunk boundaries
pool representations for each chunk
   ↓
context-aware chunk embeddings
```

Günther et al. propusieron esta técnica para evitar que cada chunk sea embebido sin conocimiento de su contexto global [11].

El modelo procesa primero una ventana larga del documento y genera representaciones token-level. Posteriormente se realiza pooling solamente sobre los tokens correspondientes a cada chunk.

La ventaja es que el embedding resultante conserva información contextual procedente de otras partes del documento.

La principal restricción práctica es que no puede implementarse sobre cualquier API estándar de embeddings: requiere acceso a representaciones internas antes del pooling o un modelo/servicio que implemente específicamente el procedimiento.

Por lo tanto, Late Chunking es interesante para `rag-experiment`, pero debería permanecer desacoplado del diseño central.

---

## 9. Parent-child retrieval y Small-to-Big retrieval

Una estrategia particularmente útil consiste en diferenciar:

- **unidad pequeña para retrieval**;
- **unidad más grande para generación**.

Por ejemplo:

```text
Child: 150–300 tokens
Parent: 800–2000 tokens
```

Pipeline:

```text
Query
  ↓
retrieve child
  ↓
identify parent
  ↓
return parent or surrounding context
  ↓
LLM
```

Esto permite combinar:

- precisión del retrieval fino;
- contexto suficiente para generación.

H-RAG, presentado en SemEval 2026, utiliza explícitamente child-level retrieval y parent-level context reconstruction [5].

HiChunk también trabaja con estructuras jerárquicas y Auto-Merge Retrieval, permitiendo ajustar dinámicamente la granularidad recuperada [4].

Este patrón es especialmente atractivo porque **desacopla retrieval precision de generation context**.

---

## 10. Hierarchical chunking

El siguiente paso consiste en representar explícitamente diferentes niveles del documento:

```text
Document
    │
    ├── Chapter
    │     │
    │     ├── Section
    │     │      │
    │     │      ├── Paragraph
    │     │      └── Paragraph
    │     │
    │     └── Section
    │
    └── Chapter
```

Cada nivel puede poseer diferentes representaciones:

```text
text
summary
embedding
concepts
metadata
```

RAPTOR fue uno de los antecedentes importantes de este enfoque. Construye recursivamente un árbol mediante embeddings, clustering y resúmenes, permitiendo recuperar información en diferentes niveles de abstracción [12].

HiChunk avanza específicamente sobre la evaluación y generación de jerarquías documentales para RAG. El trabajo introduce además HiCBench, un benchmark diseñado para evaluar calidad de chunking con anotaciones jerárquicas y preguntas con evidencia más densa que los benchmarks RAG tradicionales [4].

La jerarquía resulta especialmente útil para:

- libros;
- papers extensos;
- documentación técnica;
- manuales;
- documentos regulatorios;
- colecciones con secciones fuertemente estructuradas.

---

## 11. Proposition-level retrieval

Dense X Retrieval estudió explícitamente cuál debería ser la granularidad del retrieval y propuso utilizar **propositions** como unidad indexable [13].

Una proposición intenta representar un hecho atómico y autocontenido.

Ejemplo:

```text
Original paragraph:

SQL Server 2022 introduced feature X, which improves Y
when configuration Z is enabled.
```

Extracción:

```text
P1: SQL Server 2022 introduced feature X.
P2: Feature X improves Y.
P3: Feature X requires configuration Z.
```

El trabajo muestra que las propositions pueden mejorar retrieval frente a unidades más grandes en tareas donde la evidencia relevante es fina [13].

Para una arquitectura general no resulta conveniente reemplazar completamente los chunks por propositions.

Es preferible conservar una jerarquía:

```text
Document
 └─ Section
      └─ Chunk
           ├─ concepts[]
           ├─ propositions[]
           └─ entities[]
```

De esta manera, propositions y concepts funcionan como representaciones adicionales recuperables.

---

## 12. LLM-based / agentic chunking

Otra línea utiliza modelos de lenguaje para determinar límites de segmentos.

### LumberChunker

LumberChunker procesa secuencias de pasajes y solicita a un LLM detectar el punto en el que cambia el contenido o tema. En su benchmark GutenQA, basado en libros narrativos de Project Gutenberg, reportó una mejora del 7,37 % en DCG@20 sobre el mejor baseline evaluado [14].

### MoC

MoC (*Mixtures of Text Chunking Learners*), presentado en ACL 2025, propone utilizar modelos para generar reglas estructuradas de segmentación. También introduce métricas específicas para evaluar chunking, como *Boundary Clarity* y *Chunk Stickiness* [15].

### AutoChunker

AutoChunker también utiliza modelos de lenguaje para identificar unidades lógicas preservando la estructura jerárquica [8].

Estos enfoques pueden resultar útiles cuando:

- el documento posee estructura implícita;
- la semántica no coincide con headings explícitos;
- los cambios temáticos son complejos.

Sin embargo, agregan:

- costo de inferencia durante ingestión;
- dependencia de modelo;
- menor determinismo;
- complejidad de evaluación.

La evidencia disponible no permite asumir que LLM-based chunking sea superior en todos los escenarios [2].

---

## 13. Discourse-aware retrieval

ACL 2026 presentó *Beyond Chunking: Discourse-Aware Hierarchical Retrieval for Long Document Question Answering* [16].

El trabajo utiliza **Rhetorical Structure Theory (RST)** para modelar relaciones discursivas.

En lugar de tratar el documento como una secuencia plana:

```text
paragraph
paragraph
paragraph
```

puede representarse conceptualmente como:

```text
claim
 ├─ evidence
 ├─ elaboration
 └─ contrast
```

El sistema construye representaciones jerárquicas guiadas por discurso y realiza retrieval sobre esa estructura.

Los autores reportan mejoras consistentes en cuatro datasets, diferentes géneros e idiomas [16].

Esta línea refuerza una idea importante:

> Los límites semánticos relevantes no necesariamente coinciden con la distancia entre embeddings de párrafos consecutivos.

Para una primera implementación de `rag-experiment` probablemente sea demasiado compleja, pero merece permanecer dentro del radar experimental.

---

## 14. Adaptive Chunking

Una de las líneas más importantes de 2026 consiste en abandonar la idea de una estrategia única de chunking para todo el corpus.

El trabajo *Adaptive Chunking: Optimizing Chunking-Method Selection for RAG*, presentado en LREC 2026, selecciona la estrategia de chunking según características del documento [17].

Propone cinco métricas intrínsecas:

- **References Completeness (RC)**
- **Intrachunk Cohesion (ICC)**
- **Document Contextual Coherence (DCC)**
- **Block Integrity (BI)**
- **Size Compliance (SC)**

El sistema utiliza estas métricas para seleccionar el método más adecuado para cada documento.

En su evaluación, el enfoque adaptativo incrementó la corrección de respuestas aproximadamente de 62–64 % a 72 % y aumentó el número de preguntas contestadas correctamente de 49 a 65 [17].

Arquitectónicamente, esto sugiere algo como:

```text
Document
   ↓
document analysis
   ↓
select chunking strategy
   ↓
chunk
```

Por lo tanto, en un framework experimental conviene modelar el chunking mediante estrategias intercambiables en lugar de fijar una implementación global.

---

## 15. Query-adaptive y cross-granularity retrieval

### FreeChunker

FreeChunker, presentado en Findings of ACL 2026, cuestiona la necesidad de crear chunks rígidos durante ingestión [3].

Utiliza oraciones como unidades atómicas y permite recuperar combinaciones arbitrarias de ellas.

Conceptualmente:

```text
S1
S2
S3
S4
S5

query A → S2
query B → S2 + S3 + S4
query C → S1 + S2
```

Esto desplaza el problema desde:

```text
static chunk segmentation
```

hacia:

```text
dynamic context construction
```

El trabajo evalúa el método sobre LongBench V2 y reporta ventajas en retrieval y eficiencia frente a métodos de chunking estático [3].

### Query-Adaptive Semantic Chunking

QASC, presentado como preprint en 2026, incorpora la query al proceso de construcción del contexto. Identifica oraciones relevantes mediante similitud query-sentence y expande dinámicamente una ventana contextual alrededor de ellas [18].

La evidencia de QASC debe considerarse más preliminar que los trabajos publicados en ACL/LREC, pero resulta representativa de la dirección de investigación.

---

## 16. Documentos complejos y multimodalidad

Para documentos que contienen:

- tablas;
- gráficos;
- diagramas;
- múltiples columnas;
- footnotes;
- imágenes;
- bloques de código;

el problema no puede reducirse a chunking textual.

El parser debe preservar estructura y tipo de elemento antes de decidir cómo generar unidades recuperables.

Microsoft Azure AI Search, por ejemplo, permite utilizar Document Layout para identificar headings y contenido estructural antes de generar los chunks y embeddings [9].

La separación conceptual recomendable es:

```text
Source document
      ↓
Parsing / layout extraction
      ↓
Structured document model
      ↓
Chunking
      ↓
Index representations
```

Por lo tanto:

> **Parsing y chunking deben ser subsistemas diferentes.**

---

## 17. Comparación resumida

| Técnica | Situación en octubre de 2026 | Prioridad experimental |
|---|---|---|
| Fixed token | Baseline imprescindible | Alta |
| Recursive | Baseline práctico | Alta |
| Sentence / paragraph | Baseline estructural | Alta |
| Structure-aware | Opción fuerte por defecto | Muy alta |
| Semantic breakpoint | Evidencia mixta | Media |
| Contextualized chunks | Muy relevante | Muy alta |
| Late Chunking | Interesante, dependiente del modelo | Media |
| Parent-child | Patrón fuerte | Muy alta |
| Hierarchical | Relevante para documentos largos | Alta |
| LLM / agentic chunking | Útil pero costoso | Media |
| Proposition-level | Muy útil para factual / multi-hop | Alta |
| Discourse-aware | Investigación avanzada | Baja inicialmente |
| Adaptive | Dirección importante en 2026 | Alta |
| Query-adaptive / FreeChunker | Frontera actual | Experimental |

---

## 18. Propuesta inicial para `rag-experiment`

No conviene implementar simultáneamente todas las variantes.

Una primera batería experimental podría contener:

### Experimento 1 — Fixed-size baseline

Probar, por ejemplo:

```text
256 tokens
512 tokens
1024 tokens
```

con y sin overlap.

### Experimento 2 — Structure-aware

Prioridad:

```text
headings
    ↓
paragraphs
    ↓
sentences
    ↓
token limit
```

preservando:

- listas;
- código;
- tablas;
- metadata jerárquica.

### Experimento 3 — Semantic breakpoint

Implementarlo como comparación contra fixed y structure-aware.

### Experimento 4 — Contextual enrichment

Mantener el mismo chunk pero comparar:

```text
RawChunk
vs.
ContextualizedChunk
```

tanto para dense retrieval como para BM25.

### Experimento 5 — Parent-child retrieval

Por ejemplo:

```text
child ≈ 150–300 tokens
parent ≈ 800–2000 tokens
```

Recuperar children y reconstruir contexto a nivel parent.

### Experimento 6 — Concepts y propositions

Mantener:

```text
Chunk
 ├─ concepts[]
 ├─ propositions[]
 └─ entities[]
```

como representaciones adicionales de búsqueda.

### Experimento 7 — Hierarchical / Late / Adaptive

Agregar estos enfoques después de disponer de un benchmark reproducible sobre los anteriores.

---

## 19. Evaluación

La calidad de chunking no debería medirse únicamente mediante calidad de la respuesta final.

Conviene separar:

```text
Segmentation quality
        ↓
Retrieval quality
        ↓
Reranking quality
        ↓
Generation quality
```

Para retrieval:

```text
Recall@k
MRR
nDCG@k
HitRate@k
```

También deberían medirse variables operativas:

```text
number of chunks
number of vectors
index size
tokens retrieved
retrieval latency
ingestion latency
ingestion cost
query cost
```

HiChunk destaca además un problema metodológico: muchos benchmarks RAG poseen evidencia demasiado dispersa o escasa para evaluar adecuadamente la calidad específica del chunking [4].

Esto implica que `rag-experiment` debería tener datasets y preguntas donde sea posible determinar explícitamente qué fragmentos contienen la evidencia necesaria.

---

## 20. Modelo conceptual recomendado

Una representación suficientemente flexible podría ser:

```text
Document
 └─ Section
      └─ Chunk
           ├─ concepts[]
           ├─ propositions[]
           ├─ entities[]
           └─ embeddings[]
```

Metadatos recomendables:

```text
DocumentId
SectionId
ParentChunkId
ChunkId

RawText
ContextualizedText
EmbeddingText
SearchText

HeadingPath[]
Concepts[]
Propositions[]
Entities[]

StartOffset
EndOffset
TokenCount
```

Esto permite experimentar posteriormente con:

- fixed chunking;
- recursive chunking;
- semantic chunking;
- structure-aware chunking;
- contextual retrieval;
- parent-child;
- hierarchical retrieval;
- BM25;
- dense retrieval;
- hybrid retrieval;
- concepts;
- propositions;

sin rediseñar el modelo documental.

---

## 21. Conclusión

La investigación hasta octubre de 2026 muestra que el problema de chunking ya no consiste solamente en encontrar un punto óptimo donde cortar un documento.

Las tendencias más relevantes son:

1. **preservar estructura documental;**
2. **desacoplar unidad de indexación, retrieval y contexto de generación;**
3. **trabajar con múltiples granularidades;**
4. **contextualizar las unidades recuperables;**
5. **seleccionar dinámicamente granularidad o estrategia;**
6. **evaluar chunking independientemente del modelo generativo;**
7. **tratar concepts, propositions, headings y metadata como representaciones complementarias.**

Para `rag-experiment`, la estrategia más adecuada es comenzar con baselines simples y reproducibles y construir sobre ellos una arquitectura que permita experimentar con granularidad, estructura, contextualización y retrieval jerárquico.

El objetivo no debería ser descubrir **el mejor chunker universal**, sino determinar:

> **qué representación y granularidad producen el mejor retrieval para cada clase de documento y cada tipo de consulta.**

---

# Referencias

1. Qu, R., Tu, R., & Bao, F. S. (2025). **Is Semantic Chunking Worth the Computational Cost?** Findings of NAACL 2025.  
   https://aclanthology.org/2025.findings-naacl.114/  
   DOI: https://doi.org/10.18653/v1/2025.findings-naacl.114

2. Zhou, Y., Wang, S., Koopman, B., & Zuccon, G. (2026). **Beyond Chunk-Then-Embed: A Comprehensive Taxonomy and Evaluation of Document Chunking Strategies for Information Retrieval.**  
   https://arxiv.org/abs/2602.16974

3. Wenxuan, Z., Jiang, Y.-H., Cao, Y., & Wu, Y. (2026). **FreeChunker: A Cross-Granularity Chunking Framework.** Findings of ACL 2026.  
   https://aclanthology.org/2026.findings-acl.730/  
   DOI: https://doi.org/10.18653/v1/2026.findings-acl.730

4. Lu, W., Chen, K., Shen, Z., Qiao, R., & Sun, X. (2026). **HiChunk: Evaluating and Enhancing Retrieval Augmented Generation with Hierarchical Chunking.** ACL 2026.  
   https://aclanthology.org/2026.acl-long.1372/  
   DOI: https://doi.org/10.18653/v1/2026.acl-long.1372

5. Elchafei, P., Emam, H., Alansary, M., Swain, M., & Schedl, M. (2026). **H-RAG at SemEval-2026 Task 8: Hierarchical Parent–Child Retrieval for Multi-Turn RAG Conversations.**  
   https://aclanthology.org/2026.semeval-1.155/  
   DOI: https://doi.org/10.18653/v1/2026.semeval-1.155

6. Bhat, S. R., Rudat, M., Spiekermann, J., & Flores-Herr, N. (2025). **Rethinking Chunk Size For Long-Document Retrieval: A Multi-Dataset Analysis.**  
   https://arxiv.org/abs/2505.21700

7. **EACL 2026 Industry Track — chunk-size comparison on support/help articles.** Table 6 reports Recall@10 and Recall@50 for 512, 1024, 2048 and 4096-token chunks.  
   https://aclanthology.org/2026.eacl-industry.13.pdf

8. Jain, A., Aggarwal, P., & Saladi, A. (2025). **AutoChunker: Structured Text Chunking and its Evaluation.** ACL 2025 Industry Track.  
   https://aclanthology.org/2025.acl-industry.69/  
   DOI: https://doi.org/10.18653/v1/2025.acl-industry.69

9. Microsoft. **Chunk and Vectorize by Document Layout (Document Layout Skill) — Azure AI Search.**  
   https://learn.microsoft.com/en-us/azure/search/search-how-to-semantic-chunking

10. Anthropic. **Contextual Retrieval in AI Systems.**  
    https://www.anthropic.com/engineering/contextual-retrieval

11. Günther, M., Mohr, I., Williams, D. J., Wang, B., & Xiao, H. (2024). **Late Chunking: Contextual Chunk Embeddings Using Long-Context Embedding Models.**  
    https://arxiv.org/abs/2409.04701

12. Sarthi, P., Abdullah, S., Tuli, A., Khanna, S., Goldie, A., & Manning, C. D. (2024). **RAPTOR: Recursive Abstractive Processing for Tree-Organized Retrieval.** ICLR 2024.  
    https://proceedings.iclr.cc/paper_files/paper/2024/hash/8a2acd174940dbca361a6398a4f9df91-Abstract-Conference.html  
    https://arxiv.org/abs/2401.18059

13. Chen, T., Wang, H., Chen, S., Yu, W., Ma, K., Zhao, X., Zhang, H., & Yu, D. (2024). **Dense X Retrieval: What Retrieval Granularity Should We Use?** EMNLP 2024.  
    https://aclanthology.org/2024.emnlp-main.845/  
    DOI: https://doi.org/10.18653/v1/2024.emnlp-main.845

14. Duarte, A. V., Marques, J., Graça, M., Freire, M., Li, L., & Oliveira, A. L. (2024). **LumberChunker: Long-Form Narrative Document Segmentation.**  
    https://arxiv.org/abs/2406.17526

15. Zhao, J., Ji, Z., Fan, Z., Wang, H., Niu, S., Tang, B., Xiong, F., & Li, Z. (2025). **MoC: Mixtures of Text Chunking Learners for Retrieval-Augmented Generation System.** ACL 2025.  
    https://aclanthology.org/2025.acl-long.258/  
    DOI: https://doi.org/10.18653/v1/2025.acl-long.258

16. Chen, H., Yang, Y., Li, Y., Zhang, M., Hu, B., & Zhang, M. (2026). **Beyond Chunking: Discourse-Aware Hierarchical Retrieval for Long Document Question Answering.** ACL 2026.  
    https://aclanthology.org/2026.acl-long.829/  
    DOI: https://doi.org/10.18653/v1/2026.acl-long.829

17. de Moura Júnior, P. R., Lelong, J., & Blangero, A. (2026). **Adaptive Chunking: Optimizing Chunking-Method Selection for RAG.** LREC 2026.  
    https://aclanthology.org/2026.lrec-1.903/  
    DOI: https://doi.org/10.63317/3n8eu2phsvmc  
    Código: https://github.com/ekimetrics/adaptive-chunking

18. Rastogi, M. (2026). **Query-Adaptive Semantic Chunking for Retrieval-Augmented Generation: A Dynamic Strategy with Contextual Window Expansion.**  
    https://arxiv.org/abs/2605.22834

---

## Referencias de implementación adicionales

- Microsoft Azure AI Search — **Chunk Documents**  
  https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-chunk-documents

- Microsoft Azure AI Search — **Search Over Markdown Blobs**  
  https://learn.microsoft.com/en-us/azure/search/search-how-to-index-azure-blob-markdown

- HiChunk — implementación de referencia  
  https://github.com/TencentCloudADP/hichunk

- RAPTOR — implementación oficial  
  https://github.com/parthsarthi03/raptor

- Adaptive Chunking — implementación oficial  
  https://github.com/ekimetrics/adaptive-chunking
