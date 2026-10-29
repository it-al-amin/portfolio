using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController(PortfolioQueryService portfolio, PortfolioDbContext db, FileStorageService storage) : ControllerBase
{
    [HttpGet("portfolio")]
    public async Task<PortfolioResponse> Get()
    {
        Response.Headers.CacheControl = "no-cache";
        return await portfolio.GetAsync(HttpContext.RequestAborted);
    }

    [HttpGet("projects/{slug}")]
    public async Task<ActionResult<PublicProject>> GetProject(string slug) =>
        await portfolio.PublishedProjects().FirstOrDefaultAsync(p => p.Slug == slug) is { } p ? PublicProject.From(p) : NotFound();

    /// <summary>Stable "Download CV" link that always serves the current primary public CV.</summary>
    [HttpGet("cv")]
    public async Task<IActionResult> DownloadCv()
    {
        var doc = await portfolio.GetPublicCvAsync(HttpContext.RequestAborted);
        var path = doc is null ? null : storage.GetPath(FileCategory.Documents, doc.StoredFileName);
        if (path is null) return NotFound();
        Response.Headers.CacheControl = "no-cache";
        return PhysicalFile(path, doc!.ContentType, doc.OriginalFileName);
    }

    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        // Private documents return 404 (not 403) so their existence is not revealed.
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.IsPublic);
        var path = doc is null ? null : storage.GetPath(FileCategory.Documents, doc.StoredFileName);
        return path is null ? NotFound() : PhysicalFile(path, doc!.ContentType, doc.OriginalFileName);
    }
}

[ApiController]
[Route("api/public/contact")]
public class ContactController(ContactService contact) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Contact)]
    public async Task<ActionResult<ContactResponse>> Submit(ContactRequest request)
    {
        const string received = "Thank you! Your message has been received. I'll get back to you soon.";

        // Honeypot filled: respond like a success so bots learn nothing, but store and send nothing.
        if (!string.IsNullOrWhiteSpace(request.Website)) return new ContactResponse(received);

        return await contact.SubmitAsync(request, HttpContext.RequestAborted) switch
        {
            ContactOutcome.Accepted => new ContactResponse(received),
            ContactOutcome.Duplicate => new ContactResponse("This message was already received. No need to send it again."),
            _ => Problem(statusCode: 503, title: "Your message could not be delivered right now. Please try again later or contact me by email."),
        };
    }
}

[ApiController]
[Route("api/media")]
public class MediaController(PortfolioDbContext db, FileStorageService storage) : ControllerBase
{
    /// <summary>
    /// Serves images. Images used by the profile or a published project are public;
    /// anything else (e.g. screenshots of a draft project) requires an admin session.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var media = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (media is null) return NotFound();

        var isPublic = await db.Profiles.AnyAsync(p => p.ProfileImageId == id)
            || await db.ProjectImages.AnyAsync(i => i.MediaId == id && db.Projects.Any(p => p.Id == i.ProjectId && p.IsPublished));
        if (!isPublic && User.Identity?.IsAuthenticated != true) return NotFound();

        var path = storage.GetPath(FileCategory.Media, media.StoredFileName);
        if (path is null) return NotFound();
        Response.Headers.CacheControl = isPublic ? "public, max-age=86400" : "private, no-store";
        return PhysicalFile(path, media.ContentType);
    }
}
