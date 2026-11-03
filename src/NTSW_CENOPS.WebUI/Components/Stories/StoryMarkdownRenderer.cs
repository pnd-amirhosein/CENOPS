using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NTSW_CENOPS.Components.Stories;

/// <summary>
/// Small, deliberately restricted Markdown presentation layer for bundled release docs.
/// All source text is HTML-encoded first. Inline HTML, scripts and unsafe link protocols
/// are never interpreted. This is for release documentation, not a generic HTML renderer.
/// </summary>
public static partial class StoryMarkdownRenderer
{
    public static string Render(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new StringBuilder();
        bool code = false, list = false, table = false, quote = false;
        foreach (var original in lines)
        {
            var trimmed = original.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                CloseParagraphs();
                if (!code) { result.Append("<pre><code>"); code = true; }
                else { result.Append("</code></pre>"); code = false; }
                continue;
            }
            if (code) { result.Append(WebUtility.HtmlEncode(original)).Append('\n'); continue; }
            if (trimmed.Length == 0) { CloseParagraphs(); continue; }
            if (trimmed.StartsWith('|') && trimmed.EndsWith('|'))
            {
                if (trimmed.Replace("|", "").Replace(":", "").Replace("-", "").Trim().Length == 0) continue;
                if (!table) { CloseParagraphs(); result.Append("<table><tbody>"); table = true; }
                result.Append("<tr>");
                foreach (var cell in trimmed.Trim('|').Split('|')) result.Append("<td>").Append(Inline(cell.Trim())).Append("</td>");
                result.Append("</tr>");
                continue;
            }
            if (table) { result.Append("</tbody></table>"); table = false; }
            var heading = HeadingPattern().Match(trimmed);
            if (heading.Success)
            {
                CloseParagraphs();
                var level = Math.Min(6, heading.Groups[1].Length);
                result.Append("<h").Append(level).Append('>').Append(Inline(heading.Groups[2].Value)).Append("</h").Append(level).Append('>');
                continue;
            }
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || NumberListPattern().IsMatch(trimmed))
            {
                if (quote) { result.Append("</blockquote>"); quote = false; }
                if (!list) { result.Append("<ul>"); list = true; }
                var text = NumberListPattern().Replace(trimmed, "");
                if (text == trimmed) text = trimmed[2..];
                result.Append("<li>").Append(Inline(text)).Append("</li>");
                continue;
            }
            if (list) { result.Append("</ul>"); list = false; }
            if (trimmed.StartsWith('>'))
            {
                if (!quote) { result.Append("<blockquote>"); quote = true; }
                result.Append("<p>").Append(Inline(trimmed[1..].Trim())).Append("</p>");
                continue;
            }
            if (quote) { result.Append("</blockquote>"); quote = false; }
            if (trimmed is "---" or "***") { result.Append("<hr/>"); continue; }
            result.Append("<p>").Append(Inline(trimmed)).Append("</p>");
        }
        CloseParagraphs();
        if (code) result.Append("</code></pre>");
        return result.ToString();

        void CloseParagraphs()
        {
            if (list) { result.Append("</ul>"); list = false; }
            if (table) { result.Append("</tbody></table>"); table = false; }
            if (quote) { result.Append("</blockquote>"); quote = false; }
        }
    }

    private static string Inline(string value)
    {
        // Encode before formatting; only generated tags are ever emitted.
        var encoded = WebUtility.HtmlEncode(value);
        encoded = CodePattern().Replace(encoded, m => $"<code>{m.Groups[1].Value}</code>");
        encoded = BoldPattern().Replace(encoded, m => $"<strong>{(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)}</strong>");
        encoded = ItalicPattern().Replace(encoded, m => $"<em>{m.Groups[1].Value}</em>");
        encoded = LinkPattern().Replace(encoded, m =>
        {
            var href = WebUtility.HtmlDecode(m.Groups[2].Value);
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return m.Groups[1].Value;
            return $"<a href=\"{WebUtility.HtmlEncode(uri.AbsoluteUri)}\" target=\"_blank\" rel=\"noopener noreferrer\">{m.Groups[1].Value}</a>";
        });
        return encoded;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")] private static partial Regex HeadingPattern();
    [GeneratedRegex(@"^\d+[.)]\s+")] private static partial Regex NumberListPattern();
    [GeneratedRegex(@"`([^`]+)`")] private static partial Regex CodePattern();
    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__")] private static partial Regex BoldPattern();
    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")] private static partial Regex ItalicPattern();
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")] private static partial Regex LinkPattern();
}
