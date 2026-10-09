using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RagExperiment.Documents.Models;

namespace RagExperiment.Documents.Readers.Epub;

/// <summary>Acumula texto y calcula sus posiciones; convierte borradores mutables en nodos inmutables al finalizar.</summary>
/// <param name="cancellationToken">Cancelación observada durante el recorrido y la extracción de texto XHTML.</param>
internal sealed class XhtmlDocumentBuilder(CancellationToken cancellationToken)
{
    private readonly StringBuilder text = new();
    private readonly List<NodeDraft> nodes = [];
    private readonly NodeDraft root = new("n0", DocumentNodeKind.Document, null, null, 0);

    /// <summary>Agrega el cuerpo de un recurso al final del texto, con una sección propia y jerarquía local.</summary>
    /// <param name="resource">Ruta interna del XHTML; se conserva en SourceLocator de los nodos emitidos.</param>
    /// <param name="xhtml">Contenido del recurso; debe contener body. El orden de llamadas debe seguir el spine.</param>
    public void AddResource(string resource, string xhtml)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var html = new HtmlDocument();
        html.LoadHtml(xhtml);
        var body = html.DocumentNode.Descendants().FirstOrDefault(n => Name(n) == "body")
            ?? throw new FormatException($"XHTML resource '{resource}' has no body element.");
        var section = Create(DocumentNodeKind.Section, root, new SourceLocator(resource: resource));
        Walk(body, new HeadingScope(section), resource);
    }

    /// <summary>Finaliza spans, ordena nodos por inicio y materializa el resultado validado.</summary>
    /// <param name="context">Contexto del que se toma la identidad lógica; su metadata ya fue combinada por el lector.</param>
    /// <param name="metadata">Atributos finales después de aplicar overrides del corpus a los datos del EPUB.</param>
    /// <returns>Documento con el texto completo acumulado y sus nodos inmutables.</returns>
    public ParsedDocument Build(DocumentReadContext context,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> metadata)
    {
        root.End = text.Length;
        var result = new[] { root }.Concat(nodes)
            .Select(n => new DocumentNode(n.Id, n.Kind,
                new TextSpan(n.Start ?? n.EmptyPosition, n.Start.HasValue ? n.End - n.Start.Value : 0),
                n.Parent?.Id, n.Source))
            .OrderBy(n => n.Span.Start).ToArray();
        return new ParsedDocument(context.DocumentId, text.ToString(), result, metadata);
    }

    /// <summary>Recorre bloques, agrupa texto suelto y mantiene el padre activo según headings y secciones explícitas.</summary>
    /// <param name="container">Elemento cuyos hijos se recorren en orden de aparición.</param>
    /// <param name="scope">Ámbito de headings; wrappers transparentes lo comparten y secciones explícitas crean otro.</param>
    /// <param name="resource">Ruta de origen usada para localizar todos los nodos de este recorrido.</param>
    private void Walk(HtmlNode container, HeadingScope scope, string resource)
    {
        var inline = new List<HtmlNode>();
        void Flush()
        {
            if (inline.Count == 0) return;
            Emit(DocumentNodeKind.Paragraph, scope.Current, RenderInline(inline),
                Locator(inline.FirstOrDefault(n => n.NodeType == HtmlNodeType.Element) ?? container, resource));
            inline.Clear();
        }

        foreach (var child in container.ChildNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Excluded(child)) continue;
            var name = Name(child);
            if (name is "section" or "article")
            {
                Flush();
                var section = Create(DocumentNodeKind.Section, scope.Current, Locator(child, resource));
                Walk(child, new HeadingScope(section), resource);
            }
            else if (name.Length == 2 && name[0] == 'h' && name[1] is >= '1' and <= '6')
            {
                Flush();
                var level = name[1] - '0';
                while (scope.Headings.Count > 0 && scope.Headings.Peek().Level >= level)
                    scope.Headings.Pop();
                var section = Create(DocumentNodeKind.Section, scope.Current, Locator(child, resource));
                scope.Headings.Push((level, section));
                Emit(DocumentNodeKind.Heading, section, RenderInline(child.ChildNodes), Locator(child, resource));
            }
            else if (name is "p" or "pre" or "ul" or "ol" or "table")
            {
                Flush();
                var kind = name switch
                {
                    "pre" => DocumentNodeKind.Code,
                    "ul" or "ol" => DocumentNodeKind.List,
                    "table" => DocumentNodeKind.Table,
                    _ => DocumentNodeKind.Paragraph
                };
                Emit(kind, scope.Current, RenderBlock(child), Locator(child, resource));
            }
            else if (name is "div" or "main" or "blockquote" or "header" or "footer" or "aside" or "figure" or "address")
            {
                Flush();
                // Transparent wrappers must not reset the heading hierarchy.
                Walk(child, scope, resource);
            }
            else inline.Add(child);
        }
        Flush();
    }

    /// <summary>Emite texto una sola vez y extiende los spans de todos sus ancestros; omite bloques vacíos.</summary>
    /// <param name="kind">Tipo estructural del bloque emitido.</param>
    /// <param name="parent">Contenedor al que pertenecerá el nodo y cuyo rango debe abarcarlo.</param>
    /// <param name="value">Texto ya extraído del bloque; los separadores externos se agregan antes de calcular su inicio.</param>
    /// <param name="source">Recurso interno y ancla original, cuando está disponible.</param>
    private void Emit(DocumentNodeKind kind, NodeDraft parent, string value, SourceLocator source)
    {
        if (value.Length == 0) return;
        if (text.Length > 0) text.Append("\n\n");
        var node = Create(kind, parent, source);
        var start = text.Length;
        text.Append(value);
        for (NodeDraft? current = node; current is not null; current = current.Parent)
        {
            current.Start ??= start;
            current.End = text.Length;
        }
    }

    /// <summary>Registra un borrador con ID determinista según el orden de creación, todavía sin texto asignado.</summary>
    /// <param name="kind">Tipo del nodo que se construye.</param>
    /// <param name="parent">Padre del nodo dentro del mismo documento.</param>
    /// <param name="source">Procedencia original que acompañará al nodo final.</param>
    private NodeDraft Create(DocumentNodeKind kind, NodeDraft parent, SourceLocator source)
    {
        var node = new NodeDraft($"n{nodes.Count + 1}", kind, parent, source, text.Length);
        nodes.Add(node);
        return node;
    }

    /// <summary>Extrae un bloque: preserva pre, separa elementos de lista y representa filas y celdas de tabla.</summary>
    /// <param name="node">Bloque XHTML que se convertirá en texto; no agrega nodos ni modifica el acumulador documental.</param>
    private string RenderBlock(HtmlNode node)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Name(node) switch
        {
            "pre" => RawText(node).Replace("\r\n", "\n").Replace('\r', '\n'),
            "ul" or "ol" => string.Join("\n", node.ChildNodes.Where(n => Name(n) == "li")
                .Select(RenderListItem).Where(s => s.Length > 0)),
            "table" => string.Join("\n", node.Descendants().Where(n => Name(n) == "tr" &&
                    n.Ancestors().FirstOrDefault(a => Name(a) == "table") == node)
                .Select(row => string.Join("\t", row.ChildNodes.Where(n => Name(n) is "td" or "th")
                    .Select(cell => RenderInline(cell.ChildNodes))))),
            _ => RenderInline(node.ChildNodes)
        };
    }

    /// <summary>Conserva texto inline junto y separa párrafos y listas anidadas mediante saltos de línea.</summary>
    /// <param name="node">Elemento li cuyos hijos se extraen en su orden original.</param>
    private string RenderListItem(HtmlNode node)
    {
        var parts = new List<string>();
        var inline = new List<HtmlNode>();
        void Flush()
        {
            var value = RenderInline(inline);
            if (value.Length > 0) parts.Add(value);
            inline.Clear();
        }
        foreach (var child in node.ChildNodes)
        {
            if (Name(child) is "ul" or "ol" or "p" or "pre")
            {
                Flush();
                parts.Add(RenderBlock(child));
            }
            else inline.Add(child);
        }
        Flush();
        return string.Join("\n", parts.Where(s => s.Length > 0));
    }

    /// <summary>Extrae texto inline con entidades decodificadas y whitespace colapsado, preservando br como LF.</summary>
    /// <param name="source">Nodos inline contiguos; se procesan juntos para conservar correctamente los espacios entre ellos.</param>
    private string RenderInline(IEnumerable<HtmlNode> source)
    {
        // Collapse HTML whitespace before inserting explicit line breaks, so <br> survives.
        var result = new StringBuilder();
        foreach (var node in source) AppendInline(node, result);
        return result.ToString().Trim(' ', '\n');
    }

    /// <summary>Agrega un nodo inline y sus descendientes, excluyendo elementos que no aportan contenido principal.</summary>
    /// <param name="node">Nodo de texto, br o contenedor inline a interpretar.</param>
    /// <param name="result">Acumulador del bloque actual; su último carácter permite evitar espacios consecutivos.</param>
    private void AppendInline(HtmlNode node, StringBuilder result)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Excluded(node)) return;
        if (node.NodeType == HtmlNodeType.Text)
        {
            var value = Regex.Replace(HtmlEntity.DeEntitize(node.InnerText), @"\s+", " ");
            if (result.Length > 0 && (result[^1] == ' ' || result[^1] == '\n')) value = value.TrimStart(' ');
            result.Append(value);
        }
        else if (Name(node) == "br")
        {
            if (result.Length > 0 && result[^1] == ' ') result.Length--;
            result.Append('\n');
        }
        else
        {
            foreach (var child in node.ChildNodes) AppendInline(child, result);
        }
    }

    /// <summary>Extrae texto preformateado sin colapsar espacios; decodifica entidades y convierte br a LF.</summary>
    /// <param name="node">Nodo dentro de un bloque pre; los elementos excluidos tampoco se incorporan aquí.</param>
    private string RawText(HtmlNode node)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Excluded(node)) return "";
        if (node.NodeType == HtmlNodeType.Text) return HtmlEntity.DeEntitize(node.InnerText);
        if (Name(node) == "br") return "\n";
        return string.Concat(node.ChildNodes.Select(RawText));
    }

    /// <summary>Indica si el nodo y su contenido deben omitirse durante la extracción.</summary>
    /// <param name="node">Nodo XHTML que puede ser comentario, navegación, imagen, estilo o script.</param>
    private static bool Excluded(HtmlNode node) => node.NodeType == HtmlNodeType.Comment ||
        Name(node) is "script" or "style" or "nav" or "img" or "svg" or "head";
    /// <summary>Obtiene el nombre de etiqueta en minúsculas para comparaciones independientes de mayúsculas.</summary>
    /// <param name="node">Nodo del DOM interpretado por HtmlAgilityPack.</param>
    private static string Name(HtmlNode node) => node.Name.ToLowerInvariant();
    /// <summary>Conserva la ruta del recurso y, si existe, el id original del elemento.</summary>
    /// <param name="node">Elemento del que se toma el ancla; no se inventa un id si está ausente.</param>
    /// <param name="resource">Ruta interna del archivo EPUB, independiente de la ruta física del libro.</param>
    private static SourceLocator Locator(HtmlNode node, string resource) =>
        new(resource: resource, anchor: string.IsNullOrWhiteSpace(node.Id) ? null : node.Id);

    /// <summary>Ámbito local que determina a qué sección pertenece el siguiente bloque de texto.</summary>
    /// <param name="container">Sección base que recibe contenido cuando no hay headings activos en este ámbito.</param>
    private sealed class HeadingScope(NodeDraft container)
    {
        /// <summary>Pila de secciones abiertas por headings: Level es el nivel h1–h6 y Node es la sección creada.</summary>
        public Stack<(int Level, NodeDraft Node)> Headings { get; } = new();
        /// <summary>Padre activo: la sección del heading más reciente o el contenedor base si no hay ninguno.</summary>
        public NodeDraft Current => Headings.TryPeek(out var heading) ? heading.Node : container;
    }

    /// <summary>Estado mutable de un nodo mientras se conoce progresivamente el texto que abarca.</summary>
    /// <param name="id">ID local determinista asignado por el constructor documental.</param>
    /// <param name="kind">Clasificación estructural que tendrá el DocumentNode final.</param>
    /// <param name="parent">Borrador del padre, o null para la raíz; permite extender rangos de ancestros.</param>
    /// <param name="source">Procedencia original opcional; la raíz no requiere una localización concreta.</param>
    /// <param name="emptyPosition">Posición actual del acumulador, usada si el nodo no llega a recibir texto.</param>
    private sealed class NodeDraft(string id, DocumentNodeKind kind, NodeDraft? parent,
        SourceLocator? source, int emptyPosition)
    {
        /// <summary>ID que se conservará al materializar el nodo inmutable.</summary>
        public string Id { get; } = id;
        /// <summary>Función estructural del nodo en el documento.</summary>
        public DocumentNodeKind Kind { get; } = kind;
        /// <summary>Referencia interna al padre, convertida después en ParentNodeId.</summary>
        public NodeDraft? Parent { get; } = parent;
        /// <summary>Ubicación original, independiente de Start y End del texto extraído.</summary>
        public SourceLocator? Source { get; } = source;
        /// <summary>Inicio alternativo de un span vacío, cuando Start permanece null.</summary>
        public int EmptyPosition { get; } = emptyPosition;
        /// <summary>Inicio UTF-16 del primer texto recibido; null mientras el nodo y sus descendientes no emiten texto.</summary>
        public int? Start { get; set; }
        /// <summary>Fin UTF-16 exclusivo, extendido con cada bloque emitido por este nodo o sus descendientes.</summary>
        public int End { get; set; }
    }
}
