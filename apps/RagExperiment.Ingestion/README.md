# Propósito
Aplicación de ingesta y raíz de composición.

# Responsabilidades
Alojamiento, configuración, inyección de dependencias, registro de eventos, ciclo de vida y futura coordinación de la ingesta.

# Qué corresponde a este proyecto
Infraestructura de inicio y futura composición de los flujos de procesamiento.

También permite leer un EPUB local hasta ParsedDocument, sin persistencia ni
normalización. Registra IDocumentReader y DocumentReaderResolver desde la aplicación.

```powershell
dotnet run --project apps/RagExperiment.Ingestion -- --Document:Path="C:\libros\ejemplo.epub" --Document:Id="libro-123" --Document:Format="epub" --Document:Title="Título opcional"
```

Document:Path y Document:Id son obligatorios cuando se suministra configuración
Document. Format es opcional y se infiere de la extensión sin distinguir mayúsculas;
Title permite reemplazar el título extraído. Los mismos campos pueden configurarse
mediante appsettings o variables de entorno del host, por ejemplo Document__Path.
Las rutas relativas se resuelven desde el directorio de trabajo. El nombre físico
no determina la identidad lógica. La aplicación abre y libera el archivo, registra
identidad, título, longitud y cantidad de nodos, y devuelve código 1 ante errores.
Sin configuración Document conserva el inicio y finalización habituales.

# Qué NO corresponde a este proyecto
Implementaciones de analizadores de documentos, división en fragmentos y algoritmos de indexación.

# Dependencias del proyecto
RagExperiment.Core, RagExperiment.Documents, RagExperiment.Indexing, RagExperiment.Concepts
