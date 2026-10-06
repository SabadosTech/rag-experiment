# Propósito
Preparación de la ingesta de documentos.

La [definición de interfaces de lectura y chunking](../../docs/document-processing-design.md)
describe los contratos, modelos, estrategias iniciales y criterios de aceptación
para archivos locales TXT, EPUB y Markdown. Es un diseño pendiente de implementación;
todavía no contiene lectores ni algoritmos ejecutables.

# Responsabilidades
Carga, análisis, normalización y división en fragmentos.

# Qué corresponde a este proyecto
Adaptadores de formatos documentales y estrategias intercambiables de división en fragmentos cuando se implementen.

# Qué NO corresponde a este proyecto
Almacenamiento de índices, recuperación y generación de respuestas.

# Dependencias del proyecto
RagExperiment.Core
