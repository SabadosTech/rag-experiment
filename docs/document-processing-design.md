# Interfaces de lectura y chunking de documentos

Estado: diseño acordado, pendiente de implementación. Este documento no incorpora
código, selecciona bibliotecas ni define embeddings, vector stores, BM25 o
extracción de conceptos.

## Objetivo y arquitectura

Procesar archivos locales TXT, EPUB y Markdown, conservar su estructura y producir
variantes comparables de chunking en memoria.

**Archivo → lectura → documento estructurado → normalización → chunking → conjunto de fragmentos**

Los contratos de procesamiento pertenecen a `RagExperiment.Documents`; los modelos
compartidos con etapas posteriores pertenecen a `RagExperiment.Core`. La aplicación
de ingesta seleccionará y coordinará componentes mediante inyección de dependencias.
No se agrega una interfaz de orquestación ni dependencias hacia los índices.

## Contratos de procesamiento

| Interfaz propuesta | Entrada y salida | Responsabilidad |
| --- | --- | --- |
| `IDocumentReader` | `DocumentSource` → `ParsedDocument` | Interpretar el formato y extraer texto, estructura, metadatos y referencias de origen. Declarar formatos admitidos, identificador y versión. |
| `IDocumentNormalizer` | `ParsedDocument` → `NormalizedDocument` | Aplicar limpieza conservadora y actualizar las posiciones de los bloques. Declarar identificador y versión. |
| `IDocumentChunker` | `NormalizedDocument` + configuración → `ChunkSet` | Aplicar una estrategia intercambiable, conservar procedencia y registrar algoritmo, versión y parámetros efectivos. |

Los lectores serán específicos de TXT, EPUB y Markdown. La lectura será asíncrona;
normalización y chunking podrán ser síncronos porque operan en memoria. Todas las
operaciones admitirán cancelación.

## Modelos y datos conservados

| Modelo propuesto | Datos y significado |
| --- | --- |
| `DocumentSource` | Identificador lógico estable, ruta local, formato explícito opcional y metadatos del corpus. La ruta no define la identidad. |
| `ParsedDocument` | Texto extraído, bloques ordenados, jerarquía de secciones, metadatos y procedencia de lectura. |
| `NormalizedDocument` | Texto canónico, estructura con posiciones actualizadas e identidad de la revisión procesada. |
| `DocumentBlock` | Identificador, tipo (encabezado, párrafo, lista, código u otro), sección padre, rango en el texto y localizador de origen. |
| `DocumentChunk` | Identificador, orden, texto, rangos de origen, secciones involucradas y referencias a documento, revisión y variante. |
| `ChunkSet` | Fragmentos de una variante, configuración efectiva, versiones de procesamiento y advertencias. |

### Metadatos

Título, autores, idioma y etiquetas serán campos comunes. Los atributos personalizados
admitirán valores simples serializables. Los valores aportados por el corpus
prevalecen sobre los extraídos del archivo. La procedencia del procesamiento se
mantiene separada y no puede sobrescribirse mediante atributos personalizados.

### Localización y contexto

Los rangos tienen inicio inclusivo y fin exclusivo, medidos en unidades UTF-16 de
.NET. En el documento leído se refieren al texto extraído; después de normalizar,
los rangos de bloques y fragmentos se refieren al texto normalizado.

Conservar referencias al archivo cuando estén disponibles: líneas para TXT y
Markdown; recurso interno, capítulo y ancla para EPUB. No se prometen páginas ni
offsets de bytes. Los destinos de enlaces e imágenes de Markdown se conservan como
datos de origen, sin cargar recursos externos.

El texto de cada fragmento procede del documento normalizado. Los títulos y la
jerarquía acompañan al fragmento como contexto; no se insertan automáticamente en
su texto.

### Identidad y reproducibilidad

Distinguir documento lógico, revisión del contenido procesado, variante de chunking
y fragmento. La revisión considera contenido, metadatos efectivos y versiones de
lectura y normalización. Los identificadores de variantes y fragmentos serán
deterministas; las fechas de ejecución quedan fuera de su cálculo.

Cada ejecución usa configuración explícita. Cambiar estrategia, versión o parámetros
produce otra variante y permite conservar ambas para comparación. La persistencia
de estas salidas se definirá en otra etapa.

## Comportamiento por formato y normalización

- **TXT:** conservar párrafos, sin inferir capítulos ni encabezados.
- **Markdown:** conservar encabezados y jerarquía, párrafos, listas y bloques de
  código. Extraer texto visible de enlaces e imágenes y registrar sus destinos.
- **EPUB:** leer contenido textual en orden de lectura, conservar capítulos y
  encabezados disponibles y extraer metadatos del libro. Excluir imágenes y evitar
  interpretar navegación como contenido principal.
- **Normalización:** unificar saltos de línea y retirar marcado de presentación
  durante la extracción. Preservar mayúsculas, acentos, puntuación e indentación
  significativa de código. No aplicar stemming, eliminación de stopwords ni
  limpieza específica de un índice.

## Estrategias iniciales de chunking

### Tamaño fijo

Tamaño máximo y solapamiento configurables en unidades UTF-16. Validar tamaño
positivo y `0 ≤ solapamiento < tamaño`. Evitar cortes dentro de pares sustitutos
Unicode. La configuración efectiva se conserva junto con los resultados.

### Estructural

Configurar límites por párrafo, sección o capítulo y un tamaño máximo. Agrupar
unidades consecutivas dentro del límite elegido. Dividir unidades demasiado largas
por párrafos y finalmente por tamaño fijo. Esta estrategia inicial no usa
solapamiento.

## Fallos y criterios de aceptación

- Leer ejemplos pequeños de los tres formatos y verificar orden, estructura,
  metadatos y procedencia.
- Comprobar conservación de acentos, Unicode, listas y código.
- Verificar cobertura del texto, tamaños, solapamiento, orden y división de
  secciones extensas.
- Repetir el procesamiento y obtener las mismas identidades y fragmentos; cambiar
  parámetros y obtener una variante distinta.
- Validar que cada rango corresponde al texto del fragmento y permite rastrear su
  sección de origen.
- Rechazar configuraciones inválidas antes de procesar. Reportar archivos ilegibles,
  formatos no admitidos y EPUB corruptos o protegidos como errores del documento.
- Para documentos sin texto, devolver un conjunto vacío con advertencia. Los
  metadatos opcionales ausentes no impiden procesar.
- En lotes, continuar con otros archivos ante un fallo individual y registrar el
  resultado de cada documento.

## Límites de esta etapa

La primera versión asume procesamiento en memoria. Tokens, estrategias semánticas,
conectores remotos, persistencia y enriquecimientos posteriores quedan pendientes,
sin agregar interfaces anticipadas. Los casos de aceptación anteriores guían la
implementación futura; todavía no hay lectores, normalizadores ni chunkers ejecutables.
