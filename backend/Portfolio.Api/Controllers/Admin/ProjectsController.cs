using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers.Admin;

[Route("api/admin/projects")]
public partial class ProjectsController(PortfolioDbContext db, FileStorageService storage)
    : CrudControllerBase<Project, ProjectInput>(db)
{
    protected override IQueryable<Project> Query => Db.Projects.Include(p => p.Images.OrderBy(i => i.DisplayOrder));

    protected override async Task<string?> ValidateAsync(ProjectInput input, Project? existing)
    {
        var slug = string.IsNullOrWhiteSpace(input.Slug) ? Slugify(input.Title) : input.Slug.Trim();
        if (slug.Length == 0) return "Title must contain letters or numbers.";
        var taken = await Db.Projects.AnyAsync(p => p.Slug == slug && (existing == null || p.Id != existing.Id));
        return taken ? $"Another project already uses the URL slug \"{slug}\"." : null;
    }

    protected override Task BeforeSaveAsync(Project entity, ProjectInput input)
    {
        entity.Slug = string.IsNullOrWhiteSpace(input.Slug) ? Slugify(input.Title) : input.Slug.Trim();
        return Task.CompletedTask;
    }

    protected override async Task BeforeDeleteAsync(Project entity)
    {
        var mediaIds = entity.Images.Select(i => i.MediaId).ToList();
        var media = await Db.MediaFiles.Where(m => mediaIds.Contains(m.Id)).ToListAsync();
        Db.ProjectImages.RemoveRange(entity.Images);
        Db.MediaFiles.RemoveRange(media);
        foreach (var m in media) storage.Delete(FileCategory.Media, m.StoredFileName);
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ProjectImage>> UploadImage(int id, IFormFile? file, [FromForm] string? caption)
    {
        var project = await Db.Projects.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        if (file is null) return ValidationError("Choose an image to upload.");
        if (project.Images.Count >= 12) return ValidationError("A project can have at most 12 images.");

        StoredFile stored;
        try { stored = await storage.SaveAsync(file, FileCategory.Media, HttpContext.RequestAborted); }
        catch (FileValidationException ex) { return ValidationError(ex.Message); }

        var media = new MediaFile { StoredFileName = stored.StoredFileName, OriginalFileName = stored.OriginalFileName, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes };
        var image = new ProjectImage
        {
            ProjectId = id,
            MediaId = media.Id,
            Caption = Clean.Optional(caption is { Length: > 200 } ? caption[..200] : caption),
            DisplayOrder = project.Images.Count == 0 ? 0 : project.Images.Max(i => i.DisplayOrder) + 1,
        };
        Db.MediaFiles.Add(media);
        Db.ProjectImages.Add(image);
        await Db.SaveChangesAsync();
        return image;
    }

    [HttpPatch("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> UpdateImageCaption(int id, int imageId, ImageCaptionRequest request)
    {
        var image = await Db.ProjectImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProjectId == id);
        if (image is null) return NotFound();
        image.Caption = Clean.Optional(request.Caption);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/images/reorder")]
    public async Task<IActionResult> ReorderImages(int id, ReorderRequest request)
    {
        var images = await Db.ProjectImages.Where(i => i.ProjectId == id).ToListAsync();
        for (var i = 0; i < request.Ids.Count; i++)
        {
            var image = images.FirstOrDefault(x => x.Id == request.Ids[i]);
            if (image is null) return ValidationError("One or more images no longer exist. Refresh and try again.");
            image.DisplayOrder = i;
        }
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        var image = await Db.ProjectImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProjectId == id);
        if (image is null) return NotFound();
        var media = await Db.MediaFiles.FindAsync(image.MediaId);
        Db.ProjectImages.Remove(image);
        if (media is not null)
        {
            Db.MediaFiles.Remove(media);
            storage.Delete(FileCategory.Media, media.StoredFileName);
        }
        await Db.SaveChangesAsync();
        return NoContent();
    }

    public static string Slugify(string value)
    {
        var slug = NonSlugChars().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length > 120 ? slug[..120].TrimEnd('-') : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
