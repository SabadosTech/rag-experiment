# Propósito
Pruebas rápidas y aisladas.

# Responsabilidades
Validar la infraestructura, los contratos documentales y de chunking, lectura EPUB
y uso desde Ingestion.

# Qué corresponde a este proyecto
Pruebas en memoria de identidad, spans UTF-16, orden, jerarquías, localizadores,
metadata JSON e inmutabilidad. No se prueban algoritmos todavía inexistentes.
Los fixtures EPUB 2/3 se generan en memoria y usan VersOne/HtmlAgilityPack reales.
Se comprueban texto, estructura, procedencia, metadata, errores y propiedad del
stream. Las pruebas de Ingestion usan archivos temporales y ejecutan el proceso
local para validar los códigos de salida, sin red ni servicios externos.

# Qué NO corresponde a este proyecto
Infraestructura real y llamadas de red.

# Dependencias del proyecto
RagExperiment.Generation
RagExperiment.Documents
RagExperiment.Chunking
RagExperiment.Ingestion
