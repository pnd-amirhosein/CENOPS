using System.Text;
using System.IO.Compression;
using NTSW_CENOPS.FileStreaming;
using NTSW_CENOPS.FileStreaming.Contracts;
using NTSW_CENOPS.FileStreaming.Exceptions;
using NTSW_CENOPS.FileStreaming.Models;
using NTSW_CENOPS.FileStreaming.Services;
using NTSW_CENOPS.FileStreaming.Storage;

var root = Path.Combine(Path.GetTempPath(), "cenops-files-regression-" + Guid.NewGuid().ToString("N"));
try
{
    var options = new FileStreamingOptions { RootDirectory = root, MaximumUploadBytes = 16 };
    var objectStore = new LocalFileStorage(options);
    var metadataStore = new JsonFileMetadataStore(options);
    var service = new FileStreamingService(objectStore, metadataStore, options);

    var content = Encoding.UTF8.GetBytes("CENOPS");
    using var source = new MemoryStream(content);
    var saved = await service.UploadAsync(new FileUploadRequest(source, "../demo.txt", "text/plain"));
    Check(saved.FileName == "demo.txt", "Filename is treated as a display name");
    Check(saved.Length == content.Length, "Streaming size is correct");
    Check(source.CanRead, "Caller retains ownership of upload stream");

    var restarted = new FileStreamingService(new LocalFileStorage(options),
        new JsonFileMetadataStore(options), options);
    Check((await restarted.FindAsync(saved.Id)) == saved, "Metadata persists across instances");
    await using (var downloaded = await restarted.OpenReadAsync(saved.Id))
    {
        using var memory = new MemoryStream();
        await downloaded.Content.CopyToAsync(memory);
        Check(memory.ToArray().SequenceEqual(content), "Download preserves exact file bytes");
    }

    var oversize = new MemoryStream(new byte[17]);
    try
    {
        await service.UploadAsync(new FileUploadRequest(oversize, "large.bin"));
        throw new Exception("Expected file-size limit exception");
    }
    catch (FileSizeLimitExceededException) { }

    Check(Directory.GetFiles(root, "*.upload", SearchOption.AllDirectories).Length == 0,
        "Failed upload leaves no temporary object");
    Check(Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Count(file => file.Contains(Path.DirectorySeparatorChar + "objects" + Path.DirectorySeparatorChar)) == 1,
        "Failed upload does not publish an object");

    Check(await restarted.DeleteAsync(saved.Id), "Delete succeeds");
    Check(!await restarted.DeleteAsync(saved.Id), "Delete is idempotent");
    Check(await restarted.FindAsync(saved.Id) is null, "Delete removes metadata");

    try
    {
        await restarted.OpenReadAsync(saved.Id);
        throw new Exception("Expected missing-file exception");
    }
    catch (FileNotFoundException) { }

    await VerifyFilePreviewsAsync(Path.Combine(root, "preview-tests"));
    Console.WriteLine("FileStreaming regression tests passed.");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}


static async Task VerifyFilePreviewsAsync(string directory)
{
    var opts = new FileStreamingOptions { RootDirectory = directory, MaximumUploadBytes = 2 * 1024 * 1024 };
    var storage = new LocalFileStorage(opts);
    var metadata = new JsonFileMetadataStore(opts);
    var streaming = new FileStreamingService(storage, metadata, opts);
    var preview = new FilePreviewReader(streaming);

    async Task<FilePreviewResult> UploadAndReadAsync(string name, byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        var result = await streaming.UploadAsync(new FileUploadRequest(input, name));
        return await preview.ReadAsync(result.Id);
    }

    var source = await UploadAndReadAsync("demo.html", Encoding.UTF8.GetBytes("<script>alert(1)</script>"));
    Check(source.Kind == FilePreviewKind.Text && source.Text == "<script>alert(1)</script>",
        "HTML preview is text, not executable markup");

    var csv = await UploadAndReadAsync("data.csv", Encoding.UTF8.GetBytes("Name,Value\n\"Ali, A\",42"));
    Check(csv.Kind == FilePreviewKind.Spreadsheet && csv.Sheets![0].Rows[1][0] == "Ali, A",
        "CSV parser keeps quoted commas inside a cell");

    using var docx = new MemoryStream();
    using (var archive = new ZipArchive(docx, ZipArchiveMode.Create, leaveOpen: true))
    {
        var entry = archive.CreateEntry("word/document.xml");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>CENOPS Word</w:t></w:r></w:p></w:body></w:document>");
    }
    var word = await UploadAndReadAsync("document.docx", docx.ToArray());
    Check(word.Kind == FilePreviewKind.Text && word.Text!.Contains("CENOPS Word"),
        "DOCX OOXML text extraction works without external packages");

    using var xlsx = new MemoryStream();
    using (var archive = new ZipArchive(xlsx, ZipArchiveMode.Create, leaveOpen: true))
    {
        static void Add(ZipArchive archive, string name, string xml)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(xml);
        }
        Add(archive, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\" /></sheets></workbook>");
        Add(archive, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" /></Relationships>");
        Add(archive, "xl/worksheets/sheet1.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Hello</t></is></c><c r=\"B1\"><v>42</v></c></row></sheetData></worksheet>");
    }
    var excel = await UploadAndReadAsync("book.xlsx", xlsx.ToArray());
    Check(excel.Kind == FilePreviewKind.Spreadsheet && excel.Sheets![0].Rows[0][0] == "Hello" && excel.Sheets[0].Rows[0][1] == "42",
        "XLSX worksheet cells are read from relationships and inline strings");

    var old = await UploadAndReadAsync("legacy.doc", new byte[] { 1, 2, 3 });
    Check(old.Kind == FilePreviewKind.Unavailable, "Legacy binary Office format falls back to download");
}
