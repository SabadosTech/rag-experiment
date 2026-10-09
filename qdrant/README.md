# Qdrant

Esta carpeta contiene los archivos de la base de datos vectorial Qdrant.

## Descargas QDRANT

### Motor base de datos vectorial

Para obtener el ejecutable, visitar la página de versiones de Qdrant en GitHub:

https://github.com/qdrant/qdrant/releases

Descargar el siguiente archivo para Windows 11:

- qdrant-x86_64-pc-windows-msvc.zip

### Sitio web de administración

Para obtener las paginas estaticas de la web de administracion, visitar la página de versiones Qdrant web-ui en GitHub:

https://github.com/qdrant/qdrant-web-ui/releases

Descargar el siguiente archivo:
- dist-qdrant.zip

## Instalación

El archivo **qdrant-x86_64-pc-windows-msvc.zip** contiene el ejecutable portable de la base de datos vectorial, **qdrant.exe**. Extraer el ejecutable en esta carpeta.

El archivo **dist-qdrant.zip** tiene la carpeta **dist** y dentro de esta los archivos necesarios de la web de administracion *Qdrant web-ui*. Extraer el contenido en esta carpeta. Renombrar la carpeta **dist** extraida por **static**.

## Operaciones basicas en Qdrant

### Iniciar la base de datos 
Ejecutar **qdrant.exe** desde la línea de comandos.

### Acceder a la WebU de Qdrant
http://localhost:6333/dashboard

#### Detener la base de datos
En la ventana donde se ejecuta Qdrant, presionar **Ctrl+C**.

### Configuracion y logs
Se puede agregar configuracion y logs.

Ver: https://github.com/qdrant/qdrant/tree/master/config