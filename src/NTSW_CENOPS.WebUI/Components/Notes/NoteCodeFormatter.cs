using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace NTSW_CENOPS.Components.Notes;

/// <summary>Formats code for the note editor and read-only preview. Source is ALWAYS HTML-encoded.</summary>
public static class NoteCodeFormatter
{
    public static readonly (string Id, string Label)[] Languages =
    [
        ("plain", "Plain text"), ("csharp", "C#"), ("javascript", "JavaScript"),
        ("typescript", "TypeScript"), ("python", "Python"), ("sql", "SQL"),
        ("json", "JSON"), ("curl", "cURL"), ("html", "HTML / XML"), ("css", "CSS"),
        ("bash", "Bash / cURL"), ("powershell", "PowerShell"),
        ("java", "Java"), ("go", "Go")
    ];

    private static readonly Regex Tokens = new(
        "(?<comment>/\\*[\\s\\S]*?\\*/|//[^\\r\\n]*|--[^\\r\\n]*|#[^\\r\\n]*)" +
        "|(?<string>\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|`(?:\\\\.|[^`\\\\])*`)" +
        "|(?<number>\\b\\d+(?:\\.\\d+)?\\b)" +
        "|(?<keyword>\\b(?:using|namespace|public|private|protected|class|interface|record|struct|enum|new|return|async|await|var|void|int|string|bool|decimal|long|if|else|for|foreach|while|switch|case|break|throw|try|catch|finally|const|let|function|export|import|from|default|true|false|null|undefined|def|lambda|self|print|None|True|False|pass|yield|SELECT|FROM|WHERE|JOIN|LEFT|RIGHT|INNER|OUTER|ORDER|BY|GROUP|HAVING|INSERT|INTO|UPDATE|DELETE|CREATE|TABLE|AS|AND|OR|NOT|NULL|VALUES|SET|LIMIT|SELECT|func|package|type|range)\\b)" +
        "|(?<tag></?[a-zA-Z][a-zA-Z0-9:_-]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(150));

    public static string Normalize(string? language)
    {
        var value = (language ?? "").Trim().ToLowerInvariant();
        return value switch
        {
            "c#" or "cs" => "csharp", "js" => "javascript", "ts" => "typescript",
            "py" => "python", "sh" or "shell" => "bash",
            "xml" => "html", "ps1" or "ps" => "powershell", "text" or "plaintext" or "" => "plain",
            _ => Languages.Any(x => x.Id == value) ? value : "plain"
        };
    }

    /// <summary>Only returns a specific language for recognizable syntax; otherwise returns plain.</summary>
    public static string Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "plain";
        var t = text.TrimStart();
        if (TryCleanJson(t) is not null) return "json";
        if (Regex.IsMatch(t, @"(?im)^\s*curl(?:\.exe)?\s+(?:--?\w|['""\-h]|https?://)")) return "curl";
        if (Regex.IsMatch(t, @"(?im)^\s*(?:SELECT\b|WITH\s+\w+\s+AS\b|INSERT\s+INTO\b|UPDATE\s+\w+\s+SET\b|CREATE\s+(?:TABLE|VIEW)\b|DELETE\s+FROM\b|ALTER\s+TABLE\b|EXEC(?:UTE)?\s+|DECLARE\s+@)")) return "sql";
        if (Regex.IsMatch(t, @"(?m)^\s*(using\s+[A-Z][\w.]*\s*;|namespace\s+[\w.]+\s*[;{]|(?:public|internal|private|protected)\s+(?:(?:static|sealed|abstract|partial|readonly|async)\s+)*(?:class|interface|record|struct|enum|Task|void|string|int)\b|\[(?:HttpGet|HttpPost|ApiController)\b)") ||
            t.Contains("Console.WriteLine(", StringComparison.Ordinal) ||
            (t.Contains("=>", StringComparison.Ordinal) && t.Contains("public ", StringComparison.Ordinal))) return "csharp";
        if (Regex.IsMatch(t, @"(?m)^\s*(interface\s+\w+\s*\{|type\s+\w+\s*=|import\s+type\s+)") || t.Contains(": string", StringComparison.Ordinal)) return "typescript";
        if (Regex.IsMatch(t, @"(?m)^\s*(const\s+\w+\s*=|let\s+\w+\s*=|function\s+\w+\s*\(|export\s+default\s+|import\s+.*from\s+)") || t.Contains("console.log(", StringComparison.Ordinal)) return "javascript";
        if (Regex.IsMatch(t, @"(?m)^\s*(def\s+\w+\s*\(|from\s+\w+\s+import\s+|import\s+\w+|class\s+\w+\s*\(|print\s*\()") || t.Contains("if __name__ ==", StringComparison.Ordinal)) return "python";
        if (t.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(t, @"^(?:<!--[^>]*-->\s*)*(?:<!DOCTYPE\s+html|</?[a-zA-Z][\w:-]*(?:\s|>|/?>))", RegexOptions.IgnoreCase)) return "html";
        if (t.StartsWith("{", StringComparison.Ordinal) || t.StartsWith("[", StringComparison.Ordinal))
        {
            try { using var _ = System.Text.Json.JsonDocument.Parse(t); return "json"; } catch (System.Text.Json.JsonException) { /* still editing */ }
        }
        if (Regex.IsMatch(t, @"(?m)^\s*(#!/bin/(?:bash|sh)|curl\s+|sudo\s+|chmod\s+|export\s+\w+=)") ) return "bash";
        if (Regex.IsMatch(t, @"(?m)^\s*(Get-[A-Z]|Set-[A-Z]|\$\w+\s*=)") ) return "powershell";
        if (Regex.IsMatch(t, @"(?m)^\s*(package\s+main|func\s+main\s*\(|import\s+""fmt"")")) return "go";
        if (Regex.IsMatch(t, @"(?m)^\s*(package\s+[\w.]+;|public\s+static\s+void\s+main\s*\()")) return "java";
        if (Regex.IsMatch(t, @"(?m)^[.#]?[a-zA-Z][\w.#\s,:>+~\[\]='""()-]*\s*\{\s*(?:color|display|background|margin|padding|font)[-\w]*\s*:")) return "css";
        return "plain";
    }


    /// <summary>
    /// Conservative formatter: pretty-print JSON; place major SQL clauses on separate lines
    /// (never inside SQL string literals); normalize trailing whitespace for other languages.
    /// No external formatter or remote code execution is involved.
    /// </summary>
    public static string Format(string? content, string? language)
    {
        var text = (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(text)) return text;
        var selected = Normalize(language);
        if (selected == "json")
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                return System.Text.Json.JsonSerializer.Serialize(doc.RootElement,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch (System.Text.Json.JsonException) { return text; }
        }
        if (selected == "sql") return FormatSql(text);
        if (selected == "curl") return FormatCurl(text);
        if (selected == "css") return FormatCss(text);
        if (selected == "html") return FormatMarkup(text);
        if (selected is "csharp" or "javascript" or "typescript" or "java" or "go") return FormatBracedCode(text);
        return string.Join("\n", text.Split('\n').Select(line => line.TrimEnd()));
    }

    /// <summary>
    /// Normalizes JSON, including JSON wrapped in a JSON string or an escaped copy of an
    /// object. Repairs are applied only after a successful JSON parse. For other languages,
    /// legitimate escape sequences remain untouched.
    /// </summary>
    public static string Clean(string? source, string? language)
    {
        var text = (source ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(text)) return text;
        // Trimming leading whitespace outside JSON would break indentation-sensitive languages.
        var json = TryCleanJson(text.Trim());
        if (json is not null) return json;
        var selected = Normalize(language);
        // A stale language selection must not prevent recognizing pasted code.
        if (selected == "plain") selected = Detect(text);
        if (selected == "curl" || (selected == "bash" && text.TrimStart().StartsWith("curl ", StringComparison.OrdinalIgnoreCase)))
            return FormatCurl(text);
        return Format(text, selected);
    }

    private static string? TryCleanJson(string text)
    {
        static string? Pretty(string input)
        {
            try
            {
                using var doc = JsonDocument.Parse(input, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (doc.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    return null;
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException) { return null; }
        }
        var formatted = Pretty(text);
        if (formatted is not null) return formatted;
        // Some loggers double-encode JSON: "{\\"key\\":1}".
        try
        {
            using var outer = JsonDocument.Parse(text);
            if (outer.RootElement.ValueKind == JsonValueKind.String)
            {
                var unwrapped = outer.RootElement.GetString();
                if (unwrapped is not null && (formatted = Pretty(unwrapped)) is not null) return formatted;
            }
        }
        catch (JsonException) { }
        // Bare escaped JSON object copied from logs: {\"key\":1}
        if ((text[0] is '{' or '[') && text.Contains("\\\"", StringComparison.Ordinal))
        {
            var repaired = text.Replace("\\\"", "\"", StringComparison.Ordinal);
            if ((formatted = Pretty(repaired)) is not null) return formatted;
        }
        return null;
    }

    /// <summary>Split SQL clauses without changing comments, quoted strings or identifiers.</summary>
    private static string FormatSql(string text)
    {
        var token = new Regex(
            @"(?<protect>'(?:''|[^'])*'|""(?:""""|[^""])*""|\[[^\]]+\]|--[^\n]*|/\*[\s\S]*?\*/)|(?<clause>\b(?:LEFT\s+OUTER\s+JOIN|RIGHT\s+OUTER\s+JOIN|INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|GROUP\s+BY|ORDER\s+BY|INSERT\s+INTO|DELETE\s+FROM|SELECT|FROM|WHERE|HAVING|JOIN|LIMIT|UNION\s+ALL|UNION|VALUES|SET)\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        try
        {
            var formatted = token.Replace(text, match =>
            {
                if (match.Groups["protect"].Success) return match.Value;
                return "\n" + Regex.Replace(match.Value, @"\s+", " ").ToUpperInvariant();
            });
            return string.Join("\n", formatted.Trim().Split('\n').Select(line => line.TrimEnd()))
                .TrimStart('\n');
        }
        catch (RegexMatchTimeoutException) { return text; }
    }

    /// <summary>Whitespace-only CSS formatting; preserve quoted values and comments.</summary>
    private static string FormatCss(string text)
    {
        var output = new StringBuilder(text.Length + 40);
        var level = 0;
        bool quoted = false, single = false, comment = false, escaped = false;
        void Newline()
        {
            while (output.Length > 0 && char.IsWhiteSpace(output[^1])) output.Length--;
            if (output.Length > 0) output.Append('\n');
            output.Append(' ', level * 2);
        }
        for (var idx = 0; idx < text.Length; idx++)
        {
            var ch = text[idx];
            if (comment)
            {
                output.Append(ch);
                if (ch == '/' && idx > 0 && text[idx - 1] == '*') comment = false;
                continue;
            }
            if (escaped) { output.Append(ch); escaped = false; continue; }
            if (ch == '\\' && (quoted || single)) { output.Append(ch); escaped = true; continue; }
            if (ch == '"' && !single) { quoted = !quoted; output.Append(ch); continue; }
            if (ch == '\'' && !quoted) { single = !single; output.Append(ch); continue; }
            if (single || quoted) { output.Append(ch); continue; }
            if (ch == '/' && idx + 1 < text.Length && text[idx + 1] == '*') { comment = true; output.Append("/*"); idx++; continue; }
            if (ch == '{') { output.Append(" {"); level++; Newline(); continue; }
            if (ch == '}') { level = Math.Max(0, level - 1); Newline(); output.Append('}'); Newline(); continue; }
            if (ch == ';') { output.Append(';'); Newline(); continue; }
            if (!char.IsWhiteSpace(ch) || output.Length > 0 && !char.IsWhiteSpace(output[^1])) output.Append(ch);
        }
        return output.ToString().Trim();
    }

    /// <summary>Visually separates command-line cURL flags without touching quoted payloads.</summary>
    private static string FormatCurl(string text)
    {
        // Preserve scripts that already use shell line continuations or multiple commands.
        if (text.Contains('\n')) return Format(text, "plain");
        var source = text.Trim();
        var output = new StringBuilder(source.Length + 40);
        bool single = false, quoted = false, escaped = false;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (escaped) { output.Append(c); escaped = false; continue; }
            if (c == '\\' && !single) { output.Append(c); escaped = true; continue; }
            if (c == '\'' && !quoted) single = !single;
            else if (c == '"' && !single) quoted = !quoted;
            if (!single && !quoted && char.IsWhiteSpace(c))
            {
                var next = i + 1;
                while (next < source.Length && char.IsWhiteSpace(source[next])) next++;
                if (next >= source.Length) break;
                var isFlag = source[next] == '-' && next + 1 < source.Length &&
                    (source[next + 1] == '-' || char.IsLetter(source[next + 1]));
                output.Append(isFlag ? "\n  " : " ");
                i = next - 1;
                continue;
            }
            output.Append(c);
        }
        return output.ToString();
    }

    // Format markup into readable tag lines. Leave attributes and text unchanged;
    // never run untrusted HTML or scripts to prettify it.
    private static string FormatMarkup(string text)
    {
        // Tag-aware tokenization formats compact HTML as well as already multiline
        // markup. Quotes inside attributes do not become false tag boundaries.
        const string tokenPattern = @"(?s)<!--.*?-->|<(?:""[^""]*""|'[^']*'|[^'""<>])*>|[^<]+";
        var result = new List<string>();
        var depth = 0;
        foreach (Match token in Regex.Matches(text, tokenPattern))
        {
            var part = token.Value.Trim();
            if (part.Length == 0) continue;
            var closing = Regex.IsMatch(part, @"^</[\w:-]+\s*>");
            if (closing) depth = Math.Max(0, depth - 1);
            result.Add(new string(' ', depth * 2) + part);
            var opening = Regex.Match(part, @"^<([\w:-]+)(?=[\s>/])", RegexOptions.IgnoreCase);
            if (opening.Success && !part.EndsWith("/>", StringComparison.Ordinal) &&
                !Regex.IsMatch(opening.Groups[1].Value, @"^(area|base|br|col|embed|hr|img|input|link|meta|param|source|track|wbr)$", RegexOptions.IgnoreCase))
                depth = Math.Min(depth + 1, 50);
        }
        return string.Join("\n", result);
    }

    // A lexical (not regex-only) formatter for common brace-based snippets.
    // Comments, strings, template literals and for-loop separators are preserved.
    private static string FormatBracedCode(string text)
    {
        var output = new StringBuilder(text.Length + 60);
        var indent = 0;
        var parens = 0;
        char quote = '\0';
        bool escaped = false, lineComment = false, blockComment = false;
        void TrimTrailing()
        {
            while (output.Length > 0 && output[^1] is ' ' or '\t' or '\n') output.Length--;
        }
        void NextLine()
        {
            TrimTrailing();
            if (output.Length > 0) output.Append('\n');
            output.Append(' ', Math.Min(indent, 50) * 4);
        }
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (lineComment)
            {
                if (ch == '\n') { lineComment = false; NextLine(); }
                else output.Append(ch);
                continue;
            }
            if (blockComment)
            {
                output.Append(ch);
                if (ch == '*' && next == '/') { output.Append('/'); i++; blockComment = false; }
                continue;
            }
            if (quote != '\0')
            {
                output.Append(ch);
                if (escaped) escaped = false;
                else if (ch == '\\') escaped = true;
                else if (ch == quote) quote = '\0';
                continue;
            }
            if (ch == '/' && next == '/') { output.Append("//"); i++; lineComment = true; continue; }
            if (ch == '/' && next == '*') { output.Append("/*"); i++; blockComment = true; continue; }
            if (ch is '\'' or '"' or '`') { quote = ch; output.Append(ch); continue; }
            if (ch == '(') parens++;
            if (ch == ')') parens = Math.Max(0, parens - 1);
            if (ch == '{' && parens == 0) { TrimTrailing(); output.Append(" {"); indent++; NextLine(); continue; }
            if (ch == '}' && parens == 0) { indent = Math.Max(0, indent - 1); NextLine(); output.Append('}'); NextLine(); continue; }
            if (ch == ';' && parens == 0) { output.Append(';'); NextLine(); continue; }
            if (ch == '\n') { NextLine(); continue; }
            if (ch == '\r') continue;
            if (ch is ' ' or '\t')
            {
                if (output.Length > 0 && output[^1] != ' ' && output[^1] != '\n') output.Append(' ');
                continue;
            }
            output.Append(ch);
        }
        return string.Join("\n", output.ToString().Trim().Split('\n').Select(x => x.TrimEnd()));
    }

    public static string Highlight(string? code, string? language)
    {
        var input = code ?? string.Empty;
        if (Normalize(language) == "plain" || input.Length > 50000) return HtmlEncoder.Default.Encode(input);
        var sb = new StringBuilder(input.Length + 150);
        try
        {
            var cursor = 0;
            foreach (Match match in Tokens.Matches(input))
            {
                sb.Append(HtmlEncoder.Default.Encode(input[cursor..match.Index]));
                var kind = match.Groups["comment"].Success ? "comment" :
                           match.Groups["string"].Success ? "string" :
                           match.Groups["number"].Success ? "number" :
                           match.Groups["keyword"].Success ? "keyword" : "tag";
                sb.Append("<span class=\"note-syntax-").Append(kind).Append("\">")
                  .Append(HtmlEncoder.Default.Encode(match.Value)).Append("</span>");
                cursor = match.Index + match.Length;
            }
            sb.Append(HtmlEncoder.Default.Encode(input[cursor..]));
            return sb.ToString();
        }
        catch (RegexMatchTimeoutException) { return HtmlEncoder.Default.Encode(input); }
    }
}
