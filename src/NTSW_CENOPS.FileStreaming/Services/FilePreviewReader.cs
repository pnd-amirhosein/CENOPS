using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NTSW_CENOPS.FileStreaming.Contracts;
using NTSW_CENOPS.FileStreaming.Models;

namespace NTSW_CENOPS.FileStreaming.Services;

/// <summary>
/// Read-only, bounded previews with no external converters or executables.
/// Word/Excel use OOXML; legacy binary Office files deliberately require download.
/// Never render markup from uploads as trusted HTML in the web application.
/// </summary>
public sealed class FilePreviewReader(IFileStreamingService files) : IFilePreviewReader
{
    private const long MaxTextBytes = 2L * 1024 * 1024;
    private const long MaxOfficeBytes = 20L * 1024 * 1024;
    private const long MaxXmlEntryBytes = 3L * 1024 * 1024;
    private const int MaxRows = 150;
    private const int MaxColumns = 32;
    private const int MaxSheets = 5;
    private const int MaxChars = 160_000;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".tsv", ".json", ".xml", ".svg", ".html", ".htm", ".cshtml", ".razor",
        ".cs", ".css", ".scss", ".js", ".jsx", ".ts", ".tsx", ".sql", ".yaml", ".yml",
        ".log", ".config", ".ini", ".py", ".sh", ".ps1", ".diff", ".patch"
    };

    public async Task<FilePreviewResult> ReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var file = await files.FindAsync(id, cancellationToken) ?? throw new FileNotFoundException("File was not found.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is ".png" or ".jpeg" or ".jpg" or ".gif" or ".webp")
            return new(FilePreviewKind.Image);
        if (ext == ".pdf") return new(FilePreviewKind.Pdf);
        if (ext is ".mp4" or ".webm") return new(FilePreviewKind.Video);
        if (!TextExtensions.Contains(ext) && ext is not (".docx" or ".xlsx" or ".pptx"))
            return new(FilePreviewKind.Unavailable, Message: "برای این نوع فایل، پیش‌نمایش داخلی در دسترس نیست؛ فایل را دریافت کنید.");

        var limit = TextExtensions.Contains(ext) ? MaxTextBytes : MaxOfficeBytes;
        if (file.Length > limit)
            return new(FilePreviewKind.Unavailable, Message: "این فایل برای پیش‌نمایش سریع بزرگ است؛ می‌توانید نسخه اصلی را دریافت کنید.");

        try
        {
            await using var read = await files.OpenReadAsync(id, cancellationToken);
            if (ext == ".docx" || ext == ".pptx")
                return new(FilePreviewKind.Text, Text: ReadOfficeText(read.Content, ext));
            if (ext == ".xlsx")
                return new(FilePreviewKind.Spreadsheet, Sheets: ReadWorkbook(read.Content));

            var bytes = await ReadBoundedAsync(read.Content, MaxTextBytes, cancellationToken);
            var text = DecodeText(bytes);
            if (ext is ".csv" or ".tsv")
                return new(FilePreviewKind.Spreadsheet, Sheets: [new FilePreviewSheet("داده‌ها", ParseDelimited(text, ext == ".tsv" ? '\t' : ','))]);
            return new(FilePreviewKind.Text, Text: Truncate(text));
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or ArgumentException)
        {
            return new(FilePreviewKind.Unavailable, Message: "ساختار فایل برای پیش‌نمایش قابل خواندن نیست؛ می‌توانید نسخه اصلی را دریافت کنید.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, long max, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        var bytes = new byte[16 * 1024];
        int read;
        while ((read = await input.ReadAsync(bytes, ct)) != 0)
        {
            if (memory.Length + read > max) throw new InvalidDataException("Preview limit exceeded.");
            memory.Write(bytes, 0, read);
        }
        return memory.ToArray();
    }

    private static string DecodeText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, false), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string Truncate(string value) => value.Length <= MaxChars ? value : value[..MaxChars] + "\n… (ادامه فایل نمایش داده نمی‌شود)";

    private static XDocument? ReadXml(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null) return null;
        if (entry.Length > MaxXmlEntryBytes) throw new InvalidDataException("Office XML entry exceeds preview limit.");
        using var input = entry.Open();
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxXmlEntryBytes,
            IgnoreComments = true
        });
        return XDocument.Load(reader);
    }

    private static string ReadOfficeText(Stream stream, string extension)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var lines = new List<string>();
        if (extension == ".docx")
        {
            var doc = ReadXml(zip, "word/document.xml") ?? throw new InvalidDataException("Missing Word document.");
            foreach (var paragraph in doc.Descendants(w + "p").Take(2000))
            {
                var line = string.Concat(paragraph.Descendants(w + "t").Select(x => x.Value));
                if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
            }
        }
        else
        {
            foreach (var slide in zip.Entries.Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.FullName, @"^ppt/slides/slide\d+\.xml$"))
                         .OrderBy(x => x.FullName, StringComparer.Ordinal).Take(30))
            {
                var doc = ReadXml(zip, slide.FullName);
                if (doc is null) continue;
                var text = string.Join(" ", doc.Descendants(a + "t").Select(x => x.Value));
                if (!string.IsNullOrWhiteSpace(text)) lines.Add(text);
            }
        }
        return Truncate(lines.Count == 0 ? "متنی برای نمایش یافت نشد." : string.Join("\n", lines));
    }

    private static IReadOnlyList<FilePreviewSheet> ReadWorkbook(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        XNamespace m = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace p = "http://schemas.openxmlformats.org/package/2006/relationships";
        var workbook = ReadXml(zip, "xl/workbook.xml") ?? throw new InvalidDataException("Missing Excel workbook.");
        var rels = ReadXml(zip, "xl/_rels/workbook.xml.rels");
        var targets = (rels?.Descendants(p + "Relationship") ?? [])
            .Where(x => x.Attribute("Id") is not null && x.Attribute("Target") is not null)
            .ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!, StringComparer.Ordinal);
        var strings = new List<string>();
        var shared = ReadXml(zip, "xl/sharedStrings.xml");
        if (shared is not null)
            foreach (var si in shared.Descendants(m + "si").Take(10000))
                strings.Add(string.Concat(si.Descendants(m + "t").Select(x => x.Value)));

        var sheets = new List<FilePreviewSheet>();
        foreach (var sheet in workbook.Descendants(m + "sheet").Take(MaxSheets))
        {
            var id = (string?)sheet.Attribute(r + "id");
            if (id is null || !targets.TryGetValue(id, out var target)) continue;
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            // Relationship paths are internal ZIP paths; never let them escape xl/.
            var parts = new List<string>();
            foreach (var segment in path.Replace('\\', '/').Split('/'))
            {
                if (segment == "..") { if (parts.Count != 0) parts.RemoveAt(parts.Count - 1); }
                else if (segment != "." && segment.Length > 0) parts.Add(segment);
            }
            path = string.Join('/', parts);
            if (!path.StartsWith("xl/", StringComparison.Ordinal)) continue;
            var document = ReadXml(zip, path);
            if (document is null) continue;
            var rows = new List<IReadOnlyList<string>>();
            foreach (var row in document.Descendants(m + "sheetData").Descendants(m + "row").Take(MaxRows))
            {
                var cells = new string[MaxColumns];
                foreach (var cell in row.Elements(m + "c"))
                {
                    var col = ColumnIndex((string?)cell.Attribute("r"));
                    if (col < 0 || col >= MaxColumns) continue;
                    var raw = (string?)cell.Element(m + "v") ?? string.Concat(cell.Descendants(m + "is").Descendants(m + "t").Select(x => x.Value));
                    if ((string?)cell.Attribute("t") == "s" && int.TryParse(raw, out var idx))
                        raw = idx >= 0 && idx < strings.Count ? strings[idx] : raw;
                    cells[col] = raw.Length > 300 ? raw[..300] + "…" : raw;
                }
                var last = Array.FindLastIndex(cells, s => !string.IsNullOrEmpty(s));
                if (last >= 0) rows.Add(cells.Take(last + 1).ToArray());
            }
            sheets.Add(new FilePreviewSheet((string?)sheet.Attribute("name") ?? "Sheet", rows));
        }
        if (sheets.Count == 0) throw new InvalidDataException("No readable worksheets.");
        return sheets;
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return -1;
        var index = 0;
        foreach (var character in reference.ToUpperInvariant())
        {
            if (character is < 'A' or > 'Z') break;
            index = index * 26 + character - 'A' + 1;
            if (index > MaxColumns) break;
        }
        return index - 1;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseDelimited(string text, char delimiter)
    {
        var rows = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length && rows.Count < MaxRows; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == delimiter && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else if ((ch == '\r' || ch == '\n') && !quoted)
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(value.ToString()); value.Clear();
                rows.Add(fields.Take(MaxColumns).ToArray()); fields.Clear();
            }
            else if (value.Length < 3000) value.Append(ch);
        }
        if (rows.Count < MaxRows && (value.Length > 0 || fields.Count > 0))
        {
            fields.Add(value.ToString());
            rows.Add(fields.Take(MaxColumns).ToArray());
        }
        return rows;
    }
}
