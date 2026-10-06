# rag-experiment

Repositorio de pruebas con RAG de la comunidad.

Laboratorio experimental con .NET 10 para recuperación de información, generación
aumentada por recuperación (RAG), búsqueda semántica, búsqueda léxica, recuperación
híbrida, recuperación basada en conceptos y evaluación.
Los experimentos explorarán la ingesta y división de documentos en fragmentos,
BM25, búsqueda vectorial, representaciones vectoriales (embeddings), extracción de
conceptos, transformación y expansión de consultas, búsqueda híbrida, fusión por
rangos recíprocos (RRF), reordenamiento de resultados, respuestas con RAG y evaluación.

## Estado

**Arquitectura inicial / estructura básica del proyecto.** Todavía no se han
implementado algoritmos de RAG, llamadas a modelos, análisis de documentos, índices
ni métricas de evaluación. Las bibliotecas no contienen clases de dominio de relleno.
Ambas aplicaciones verifican el inicio y finalizan correctamente.

Está acordado el [diseño de lectura y chunking de documentos](docs/document-processing-design.md)
para TXT, EPUB y Markdown, con estructura, metadatos y variantes comparables.
La definición es documental; su implementación queda pendiente.

## Arquitectura

| Proyecto | Función | Dependencias directas de proyectos |
| --- | --- | --- |
| RagExperiment.Core | Primitivas y contratos de dominio compartidos y pequeños | Ninguna |
| RagExperiment.Documents | Carga, análisis, normalización y división en fragmentos | Core |
| RagExperiment.Indexing | Indexación y almacenamiento vectorial, léxico, de conceptos y de metadatos | Core |
| RagExperiment.Retrieval | Recuperación independiente, transformación de consultas, fusión y reordenamiento | Core, Indexing |
| RagExperiment.Concepts | Extracción, normalización y relaciones entre conceptos | Core |
| RagExperiment.Generation | Construcción de instrucciones y generación de respuestas después de la recuperación | Core |
| RagExperiment.Evaluation | Evaluación de recuperación; futura evaluación independiente de respuestas | Core, Retrieval |
| RagExperiment.Cli | Aplicación principal de experimentos y raíz de composición | Core, Retrieval, Concepts, Generation, Evaluation |
| RagExperiment.Ingestion | Aplicación de ingesta y raíz de composición | Core, Documents, Indexing, Concepts |
| RagExperiment.Tests | Pruebas rápidas y aisladas; actualmente una prueba básica de infraestructura | Generation |
| RagExperiment.IntegrationTests | Futuras pruebas de infraestructura y componentes reales; actualmente vacío | Ninguna |

Las bibliotecas están en `src/`, las aplicaciones en `apps/` y las pruebas en
`tests/`. Core no referencia otros proyectos. Las dependencias apuntan hacia Core;
Retrieval utiliza Indexing y Evaluation utiliza Retrieval. Generation recibe el
contexto recuperado y no realiza la recuperación. La evaluación de recuperación
es independiente de las respuestas generadas y de las llamadas a modelos de lenguaje.
Las referencias entre proyectos deben permanecer libres de ciclos.

## Acceso a IA, inyección de dependencias y registro de eventos

Microsoft.Extensions.AI proporciona los contratos estándar `IChatClient` e
`IEmbeddingGenerator<TInput, TEmbedding>`. Generation referencia únicamente
`Microsoft.Extensions.AI.Abstractions`; la prueba de infraestructura verifica que
ambos contratos estén disponibles. No se introducen interfaces redundantes como
`ILLMProvider` o `IEmbeddingProvider`. Los paquetes de IA se agregarán a otros
proyectos solo cuando sean necesarios.

Cli e Ingestion crean el host genérico de .NET (Generic Host) y administran la
inyección de dependencias, la configuración, el registro de eventos, la selección
de proveedores y el ciclo de vida de la aplicación. Un servicio alojado de inicio,
con dependencias inyectadas, comprueba que la configuración y `ILogger<T>` funcionen.
Las bibliotecas deben usar inyección por constructor, sin crear contenedores ni
resolver dependencias ordinarias mediante `IServiceProvider`. Todavía no se
registran proveedores de modelos ni servicios de las bibliotecas.

Serilog se configura en cada aplicación con una única salida a consola que muestra
fecha y hora, nivel, contexto de origen, mensaje y excepción. Las clases de las
aplicaciones y las futuras clases de las bibliotecas utilizan `ILogger<T>`; las
bibliotecas no referencian Serilog. El paquete de alojamiento proporciona las
dependencias estándar de inyección de dependencias, configuración y registro.

Semantic Kernel, LangChain, Kernel Memory y otros marcos de RAG de alto nivel se
excluyen intencionalmente para mantener el control explícito de la recuperación.
Por ahora se priorizan las abstracciones estándar de Microsoft y un conjunto mínimo
de dependencias. El acceso a IA utiliza Microsoft.Extensions.AI.Abstractions sin
incorporar SDK ni adaptadores específicos de proveedores. Serilog y xUnit se mantienen
como dependencias de registro y pruebas ya acordadas.

Indexing referencia Lucene.Net 4.8.0-beta00018 para los futuros índices léxicos y
experimentos con BM25. Se acepta explícitamente esta versión beta de Apache;
su versión se administra centralmente. Todavía no se crean índices ni se implementa
la recuperación. Los módulos adicionales de Lucene.NET se agregarán cuando sean necesarios.

OpenAI, Microsoft.Extensions.AI.OpenAI, Qdrant.Client, ONNX Runtime y
los paquetes de evaluación de IA quedan pendientes hasta que una implementación
concreta los necesite y se decida incorporarlos. Esta estructura inicial
no configura ni ejecuta los archivos locales existentes en `qdrant/`.

## Compilación y ejecución

Usar el SDK de .NET 10. Todos los proyectos heredan `net10.0`, los tipos de
referencia que admiten valores nulos y las importaciones implícitas desde
`Directory.Build.props`. Las versiones de los paquetes se centralizan en
`Directory.Packages.props`.

Desde la raíz del repositorio:

```sh
dotnet restore
dotnet build
dotnet test
dotnet run --project apps/RagExperiment.Cli
dotnet run --project apps/RagExperiment.Ingestion
```

La prueba básica de xUnit valida únicamente la infraestructura. El proyecto de
pruebas de integración todavía no contiene pruebas ni requiere servicios externos.

## Configuración y secretos

El host estándar carga `appsettings.json`, la configuración opcional específica
del entorno, las variables de entorno y los argumentos de la línea de comandos.
Los archivos de configuración se copian al directorio de salida y se cargan desde
el directorio del ejecutable, incluso al ejecutar desde la raíz del repositorio.
Establecer `DOTNET_ENVIRONMENT=Development` para habilitar
`appsettings.Development.json` y los secretos de usuario opcionales de .NET
(User Secrets).

Usar el `UserSecretsId` de cada aplicación o variables de entorno para las
credenciales. Por ejemplo, `Application__Name` reemplaza `Application:Name`.
Nunca incluir claves, tokens, credenciales ni configuración específica del equipo
en archivos JSON versionados. Los archivos ignorados `appsettings.*.Local.json`
no se cargan automáticamente; usar variables de entorno o User Secrets para
sobrescribir la configuración local.

## Datos y reproducibilidad

- `data/documents/`: documentos de origen y corpus.
- `data/datasets/`: preguntas de evaluación y juicios de relevancia.
- `data/experiments/`: salidas y resultados de los experimentos.

Estos directorios contienen únicamente archivos `.gitkeep` y no se ignoran por
completo; los conjuntos de datos podrán versionarse intencionalmente más adelante.
Revisar los datos antes de agregarlos a Git.

Un futuro `ExperimentRun` debería registrar el corpus y su versión; la estrategia
y los parámetros de división en fragmentos; el proveedor, el modelo y las
dimensiones de las representaciones vectoriales; la configuración de recuperación
léxica y vectorial; la configuración de extracción de conceptos; la estrategia de
fusión y los parámetros de RRF; el componente de reordenamiento y TopK; los tiempos
y las métricas; y la información de la aplicación y su versión.
Esto permitirá comparaciones reproducibles y objetivas. Todavía no está implementado.
