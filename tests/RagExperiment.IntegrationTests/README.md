# Propósito
Pruebas de integración, incluyendo lectura de EPUB reales del corpus local.

# Responsabilidades
Validar componentes externos reales cuando se implementen las integraciones.

# Qué corresponde a este proyecto
Futuras pruebas de Qdrant, Lucene.NET, API de modelos, analizadores de documentos y sistema de archivos.

# Qué NO corresponde a este proyecto
Pruebas de integración artificiales o servicios externos obligatorios en esta estructura inicial.

# Dependencias del proyecto
RagExperiment.Documents.

## EPUB local: 1984

`LocalEpubTests.Reads1984FromLocalCorpusWithMetadataStructureAndValidSpans` procesa
`data/documents/1984 - George Orwell.epub` con el lector real. Comprueba título,
autor, texto significativo, estructura, spans UTF-16, procedencia y stream abierto.
La salida de la prueba informa cantidades, sin imprimir el texto del libro.

El archivo se lee en su ubicación original, sin copiarlo ni incluir su contenido
en el código. Si no está disponible, la prueba se omite con un motivo explícito.
La ruta se resuelve desde la raíz del repositorio, independientemente del directorio
de trabajo del runner. Ejecutar desde la raíz:

```powershell
dotnet test tests/RagExperiment.IntegrationTests --filter Category=LocalCorpus --logger "console;verbosity=normal"
```
