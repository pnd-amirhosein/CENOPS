using NTSW_CENOPS.Application.Files;

namespace NTSW_CENOPS.Endpoints;

/// <summary>
/// File delivery uses parent + membership authorization; bare file IDs never authorize reads.
/// Never map the underlying storage directory as static files.
/// </summary>
public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/files").RequireAuthorization();
        group.MapGet("/{id:guid}/content", (Guid id, IAppFileGateway gateway, IFileResourceAccess policy, HttpContext context, CancellationToken ct) =>
            OpenAsync(id, gateway, policy, context, false, ct));
        group.MapGet("/{id:guid}/download", (Guid id, IAppFileGateway gateway, IFileResourceAccess policy, HttpContext context, CancellationToken ct) =>
            OpenAsync(id, gateway, policy, context, true, ct));
        group.MapGet("/{id:guid}/preview", (Guid id, IAppFileGateway gateway, IFileResourceAccess policy, HttpContext context, CancellationToken ct) =>
            OpenPdfAsync(id, gateway, policy, context, ct));
        return routes;
    }

    private static async Task<bool> AuthorizedAsync(Guid id, IFileResourceAccess policy, HttpContext context, CancellationToken ct)
    {
        var query = context.Request.Query;
        var hasMessage = query.ContainsKey("messageId");
        var hasNote = query.ContainsKey("noteId");
        if (hasMessage == hasNote) return false;
        long? messageId = null;
        int? noteId = null;
        if (hasMessage)
        {
            if (!long.TryParse(query["messageId"], out var value) || value <= 0) return false;
            messageId = value;
        }
        if (hasNote)
        {
            if (!int.TryParse(query["noteId"], out var value) || value <= 0) return false;
            noteId = value;
        }
        return await policy.CanReadAsync(id, messageId, noteId, context.User, ct);
    }

    private static async Task<IResult> OpenPdfAsync(Guid id, IAppFileGateway gateway, IFileResourceAccess policy, HttpContext context, CancellationToken ct)
    {
        if (!await AuthorizedAsync(id, policy, context, ct)) return Results.NotFound();
        AppFileRead result;
        try { result = await gateway.OpenReadAsync(id, ct); }
        catch (FileNotFoundException) { return Results.NotFound(); }

        if (!string.Equals(Path.GetExtension(result.File.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await result.DisposeAsync();
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        // Uploaded MIME and extension are untrusted; validate at least the PDF signature.
        var signature = new byte[5];
        try { await result.Content.ReadExactlyAsync(signature.AsMemory(), ct); }
        catch (EndOfStreamException)
        {
            await result.DisposeAsync();
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        if (!signature.AsSpan().SequenceEqual("%PDF-"u8))
        {
            await result.DisposeAsync();
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        result.Content.Position = 0;
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.File(result.Content, "application/pdf", enableRangeProcessing: true);
    }

    private static async Task<IResult> OpenAsync(Guid id, IAppFileGateway gateway, IFileResourceAccess policy, HttpContext context, bool download, CancellationToken ct)
    {
        if (!await AuthorizedAsync(id, policy, context, ct)) return Results.NotFound();
        AppFileRead result;
        try { result = await gateway.OpenReadAsync(id, ct); }
        catch (FileNotFoundException) { return Results.NotFound(); }

        // MIME declared by uploaders is untrusted. Only common raster images can render inline.
        var extension = Path.GetExtension(result.File.FileName).ToLowerInvariant();
        var safeType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => "application/octet-stream"
        };
        if (!download && safeType == "application/octet-stream")
        {
            await result.DisposeAsync();
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        // Result.File owns/disposes the stream after sending (including range requests).
        return Results.File(result.Content, safeType,
            fileDownloadName: download ? result.File.FileName : null,
            enableRangeProcessing: true);
    }
}
