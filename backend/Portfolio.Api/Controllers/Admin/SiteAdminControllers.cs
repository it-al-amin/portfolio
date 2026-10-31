using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;
using Portfolio.Api.Services;
using Portfolio.Api.Services.Backup;

namespace Portfolio.Api.Controllers.Admin;

[ApiController]
[Authorize]
[Route("api/admin/profile")]
public class ProfileController(PortfolioDbContext db, FileStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<Profile> Get() => await GetOrCreateAsync();

    [HttpPut]
    public async Task<Profile> Update(ProfileInput input)
    {
        var profile = await GetOrCreateAsync();
        input.ApplyTo(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    [HttpPost("image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<Profile>> UploadImage(IFormFile? file)
    {
        if (file is null) return ValidationProblem(new ValidationProblemDetails { Title = "Choose an image to upload." });
        StoredFile stored;
        try { stored = await storage.SaveAsync(file, FileCategory.Media, HttpContext.RequestAborted); }
        catch (FileValidationException ex) { return ValidationProblem(new ValidationProblemDetails { Title = ex.Message }); }

        var media = new MediaFile { StoredFileName = stored.StoredFileName, OriginalFileName = stored.OriginalFileName, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes };
        db.MediaFiles.Add(media);
        var profile = await GetOrCreateAsync();
        var previous = profile.ProfileImageId;
        profile.ProfileImageId = media.Id;
        await db.SaveChangesAsync();
        if (previous is { } old) await DeleteMediaAsync(old);
        return profile;
    }

    [HttpDelete("image")]
    public async Task<IActionResult> DeleteImage()
    {
        var profile = await GetOrCreateAsync();
        if (profile.ProfileImageId is { } id)
        {
            profile.ProfileImageId = null;
            await db.SaveChangesAsync();
            await DeleteMediaAsync(id);
        }
        return NoContent();
    }

    private async Task DeleteMediaAsync(Guid id)
    {
        var media = await db.MediaFiles.FindAsync(id);
        if (media is null) return;
        db.MediaFiles.Remove(media);
        await db.SaveChangesAsync();
        storage.Delete(FileCategory.Media, media.StoredFileName);
    }

    private async Task<Profile> GetOrCreateAsync()
    {
        var profile = await db.Profiles.FirstOrDefaultAsync();
        if (profile is not null) return profile;
        profile = new Profile { FullName = "Your Name", Headline = "Software Engineer" };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }
}

[ApiController]
[Authorize]
[Route("api/admin/settings/email")]
public class EmailSettingsController(PortfolioDbContext db, EmailConfigProvider configProvider, IEmailSender sender, ILogger<EmailSettingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<EmailSettingsView> Get() => ToView(await configProvider.GetAsync());

    [HttpPut]
    public async Task<EmailSettingsView> Update(EmailSettingsInput input)
    {
        var settings = await EmailConfigProvider.GetSettingsAsync(db);
        input.ApplyTo(settings);
        await db.SaveChangesAsync();
        return ToView(await configProvider.GetAsync());
    }

    [HttpPost("test")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> SendTest(TestEmailRequest request)
    {
        var config = await configProvider.GetAsync();
        if (!config.IsConfigured)
            return Problem(statusCode: 400, title: "Email is not configured. Set the SMTP host, from address and recipient first.");

        var to = string.IsNullOrWhiteSpace(request.To) ? config.RecipientEmail! : request.To.Trim();
        var siteName = (await db.Profiles.AsNoTracking().FirstOrDefaultAsync())?.FullName ?? "Portfolio";
        try
        {
            await sender.SendAsync(config, EmailTemplates.Test(to, siteName), HttpContext.RequestAborted);
            return Ok(new { message = $"Test email sent to {to}." });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Test email failed: {Error}", ex.GetType().Name + ": " + ex.Message);
            // The admin sees a short reason (e.g. authentication failed) but never credentials or stack traces.
            return Problem(statusCode: 502, title: "The test email could not be sent.", detail: ex.Message);
        }
    }

    private static EmailSettingsView ToView(EffectiveEmailConfig c) => new(
        c.Settings, c.Host, c.Port, c.Username, c.FromEmail, c.RecipientEmail, c.HasPassword, c.IsConfigured);
}

[ApiController]
[Authorize]
[Route("api/admin/messages")]
public class MessagesController(PortfolioDbContext db) : ControllerBase
{
    public record MessagePage(List<ContactMessage> Items, int Total, int Page, int PageSize);

    [HttpGet]
    public async Task<MessagePage> List([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ContactMessages.AsNoTracking();

        query = status switch
        {
            "unread" => query.Where(m => !m.IsRead),
            "read" => query.Where(m => m.IsRead),
            "replied" => query.Where(m => m.IsReplied),
            "unreplied" => query.Where(m => !m.IsReplied),
            "failed" => query.Where(m => m.NotificationStatus == NotificationStatus.Failed),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m => EF.Functions.Like(m.Name, $"%{term}%") || EF.Functions.Like(m.Email, $"%{term}%")
                || EF.Functions.Like(m.Subject, $"%{term}%") || EF.Functions.Like(m.Message, $"%{term}%"));
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(m => m.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new MessagePage(items, total, page, pageSize);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContactMessage>> Get(int id) =>
        await db.ContactMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id) is { } m ? m : NotFound();

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ContactMessage>> UpdateStatus(int id, MessageStatusRequest request)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is null) return NotFound();
        if (request.IsRead is { } read) message.IsRead = read;
        if (request.IsReplied is { } replied)
        {
            message.IsReplied = replied;
            message.RepliedAt = replied ? DateTime.UtcNow : null;
            if (replied) message.IsRead = true;
        }
        await db.SaveChangesAsync();
        return message;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is null) return NotFound();
        db.ContactMessages.Remove(message);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/admin/dashboard")]
public class DashboardController(PortfolioDbContext db, EmailConfigProvider configProvider, DatabaseBackupService backups) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get()
    {
        var email = await configProvider.GetAsync();
        return new
        {
            projects = new { total = await db.Projects.CountAsync(), published = await db.Projects.CountAsync(p => p.IsPublished), featured = await db.Projects.CountAsync(p => p.IsFeatured && p.IsPublished) },
            skills = await db.Skills.CountAsync(),
            experiences = await db.Experiences.CountAsync(),
            education = await db.Education.CountAsync(),
            achievements = await db.Achievements.CountAsync(),
            services = await db.Services.CountAsync(),
            documents = new { total = await db.Documents.CountAsync(), @public = await db.Documents.CountAsync(d => d.IsPublic) },
            messages = new
            {
                total = await db.ContactMessages.CountAsync(),
                unread = await db.ContactMessages.CountAsync(m => !m.IsRead),
                failedNotifications = await db.ContactMessages.CountAsync(m => m.NotificationStatus == NotificationStatus.Failed),
            },
            recentMessages = await db.ContactMessages.AsNoTracking().OrderByDescending(m => m.CreatedAt).Take(5)
                .Select(m => new { m.Id, m.Name, m.Subject, m.CreatedAt, m.IsRead }).ToListAsync(),
            hasPublicCv = await db.Documents.AnyAsync(d => d.Kind == DocumentKind.Cv && d.IsPublic),
            emailConfigured = email.IsConfigured,
            emailPasswordConfigured = email.HasPassword,
            backup = backups.Status,
        };
    }
}
