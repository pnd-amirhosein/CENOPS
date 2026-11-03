using System.Net;
using System.Text;

namespace NTSW_CENOPS.Components.Common;

/// <summary>
/// Escaped, read-only C# highlighting for indexed source previews and virtualized full files.
/// Never renders source as raw markup: all token text is HTML-encoded first.
/// </summary>
public static class SourceSyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract","as","async","await","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double","else","enum","event",
        "explicit","extern","false","finally","fixed","float","for","foreach","from","get","global",
        "goto","group","if","implicit","in","init","int","interface","internal","into","is","join",
        "let","lock","long","nameof","namespace","new","not","null","object","on","operator","or",
        "orderby","out","override","params","partial","private","protected","public","readonly","record",
        "ref","required","return","sbyte","sealed","select","set","short","sizeof","stackalloc",
        "static","string","struct","switch","this","throw","true","try","typeof","uint","ulong",
        "unchecked","unsafe","ushort","using","value","var","virtual","void","volatile","when",
        "where","while","with","yield"
    };

    public static IReadOnlyList<string> HighlightLines(IEnumerable<string> lines)
    {
        var inBlockComment = false;
        var highlighted = new List<string>();
        foreach (var line in lines)
            highlighted.Add(HighlightLine(line.TrimEnd('\r'), ref inBlockComment));
        return highlighted;
    }

    public static string Highlight(string? code) =>
        string.Join("\n", HighlightLines((code ?? string.Empty).Split('\n')));

    private static string HighlightLine(string line, ref bool inBlockComment)
    {
        var html = new StringBuilder(line.Length + 32);
        var i = 0;
        while (i < line.Length)
        {
            var start = i;
            if (inBlockComment || Starts(line, i, "/*"))
            {
                var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                i = end < 0 ? line.Length : end + 2;
                inBlockComment = end < 0;
                Append(html, "comment", line[start..i]);
            }
            else if (Starts(line, i, "//"))
            {
                Append(html, "comment", line[i..]);
                break;
            }
            else if (line[i] is '"' or '\'' || Starts(line, i, "@\"") || Starts(line, i, "$\"") ||
                     Starts(line, i, "$@\"") || Starts(line, i, "@$\""))
            {
                var verbatim = Starts(line, i, "@\"") || Starts(line, i, "$@\"") || Starts(line, i, "@$\"");
                var opening = Starts(line, i, "$@\"") || Starts(line, i, "@$\"") ? 3 :
                    Starts(line, i, "$\"") || Starts(line, i, "@\"") ? 2 : 1;
                var quote = line[i + opening - 1];
                i += opening;
                while (i < line.Length)
                {
                    if (line[i] == quote)
                    {
                        if (verbatim && i + 1 < line.Length && line[i + 1] == quote) { i += 2; continue; }
                        i++;
                        break;
                    }
                    if (!verbatim && line[i] == '\\' && i + 1 < line.Length) i += 2;
                    else i++;
                }
                Append(html, "string", line[start..i]);
            }
            else if (char.IsDigit(line[i]))
            {
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '.' or '_')) i++;
                Append(html, "number", line[start..i]);
            }
            else if (char.IsLetter(line[i]) || line[i] == '_' ||
                     line[i] == '@' && i + 1 < line.Length && (char.IsLetter(line[i + 1]) || line[i + 1] == '_'))
            {
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                var token = line[start..i];
                var kind = Keywords.Contains(token) ? "keyword" :
                    token.StartsWith('@') ? "identifier" : char.IsUpper(token[0]) ? "type" : "identifier";
                Append(html, kind, token);
            }
            else
            {
                html.Append(WebUtility.HtmlEncode(line[i].ToString()));
                i++;
            }
        }
        return html.ToString();
    }

    private static bool Starts(string line, int index, string value) =>
        index + value.Length <= line.Length &&
        string.CompareOrdinal(line, index, value, 0, value.Length) == 0;

    private static void Append(StringBuilder html, string kind, string text)
    {
        html.Append("<span class=\"src-token-").Append(kind).Append("\">")
            .Append(WebUtility.HtmlEncode(text)).Append("</span>");
    }
}
