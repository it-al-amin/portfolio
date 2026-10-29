using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers.Admin;

[ApiController]
[Authorize]
[Route("api/admin/documents")]
public class DocumentsController(PortfolioDbContext db, FileStorageService storage) : ControllerBase
{
    private const long MaxRequestBytes = 11 * 1024 * 1024;

    [HttpGet]
    public async Task<List<DocumentView>> List() =>
        (await db.Documents.AsNoTracking().OrderBy(d => d.Kind).ThenByDescending(d => d.UpdatedAt).ToListAsync())
            .Select(DocumentView.From).ToList();

    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult<DocumentView>> Upload(IFormFile? file, [FromForm] DocumentUpdateInput input)
    {
        if (file is null) return Error("Choose a file to upload.");

        StoredFile stored;
        try { stored = await storage.SaveAsync(file, FileCategory.Documents, HttpContext.RequestAborted); }
        catch (FileValidationException ex) { return Error(ex.Message); }

        var doc = new Document
        {
            StoredFileName = stored.StoredFileName,
            OriginalFileName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
        };
        Apply(doc, input);
        db.Documents.Add(doc);
        await EnforceSinglePrimaryAsync(doc);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Download), new { id = doc.Id }, DocumentView.From(doc));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentView>> Update(Guid id, DocumentUpdateInput input)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null) return NotFound();
        Apply(doc, input);
        await EnforceSinglePrimaryAsync(doc);
        await db.SaveChangesAsync();
        return DocumentView.From(doc);
    }

    /// <summary>Replaces the file while keeping the document's id, metadata and public links.</summary>
    [HttpPost("{id:guid}/file")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult<DocumentView>> ReplaceFile(Guid id, IFormFile? file, [FromForm] string? version)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null) return NotFound();
        if (file is null) return Error("Choose a file to upload.");

        StoredFile stored;
        try { stored = await storage.SaveAsync(file, FileCategory.Documents, HttpContext.RequestAborted); }
        catch (FileValidationException ex) { return Error(ex.Message); }

        var oldFile = doc.StoredFileName;
        doc.StoredFileName = stored.StoredFileName;
        doc.OriginalFileName = stored.OriginalFileName;
        doc.ContentType = stored.ContentType;
        doc.SizeBytes = stored.SizeBytes;
        doc.UploadedAt = doc.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(version)) doc.Version = version.Trim()[..Math.Min(30, version.Trim().Length)];
        await db.SaveChangesAsync();
        storage.Delete(FileCategory.Documents, oldFile);
        return DocumentView.From(doc);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        var path = doc is null ? null : storage.GetPath(FileCategory.Documents, doc.StoredFileName);
        return path is null ? NotFound() : PhysicalFile(path, doc!.ContentType, doc.OriginalFileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null) return NotFound();
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        storage.Delete(FileCategory.Documents, doc.StoredFileName);
        return NoContent();
    }

    private static void Apply(Document doc, DocumentUpdateInput input)
    {
        doc.Title = Clean.Text(input.Title);
        doc.Description = Clean.Optional(input.Description);
        doc.Kind = input.Kind;
        doc.Version = Clean.Optional(input.Version);
        doc.IsPublic = input.IsPublic;
        // Only a public CV can be the primary one served from the public "Download CV" link.
        doc.IsPrimary = input.IsPrimary && input.IsPublic && input.Kind == DocumentKind.Cv;
        doc.UpdatedAt = DateTime.UtcNow;
    }

    private async Task EnforceSinglePrimaryAsync(Document doc)
    {
        if (!doc.IsPrimary) return;
        var others = await db.Documents.Where(d => d.Kind == doc.Kind && d.IsPrimary && d.Id != doc.Id).ToListAsync();
        foreach (var other in others) other.IsPrimary = false;
    }

    private ActionResult Error(string message) => ValidationProblem(new ValidationProblemDetails { Title = message, Detail = message });
}
