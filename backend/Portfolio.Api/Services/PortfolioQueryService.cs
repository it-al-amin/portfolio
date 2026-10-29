using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Services;

/// <summary>Builds the public portfolio view. Only published content and public documents are included.</summary>
public class PortfolioQueryService(PortfolioDbContext db)
{
    public const string CvUrl = "/api/public/cv";

    public async Task<PortfolioResponse> GetAsync(CancellationToken ct = default)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(ct) ?? new Profile { FullName = "Portfolio", Headline = "" };

        var categories = await db.SkillCategories.AsNoTracking()
            .Where(c => c.IsPublished)
            .Include(c => c.Skills.Where(s => s.IsPublished).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .ToListAsync(ct);

        var experience = await db.Experiences.AsNoTracking().Where(e => e.IsPublished)
            .OrderBy(e => e.DisplayOrder).ThenByDescending(e => e.StartDate).ToListAsync(ct);

        var projects = await PublishedProjects().ToListAsync(ct);

        var education = await db.Education.AsNoTracking().Where(e => e.IsPublished).OrderBy(e => e.DisplayOrder).ThenBy(e => e.Id).ToListAsync(ct);

        var publicDocs = await db.Documents.AsNoTracking().Where(d => d.IsPublic)
            .OrderBy(d => d.Kind).ThenByDescending(d => d.UpdatedAt).ToListAsync(ct);
        var publicDocIds = publicDocs.Select(d => d.Id).ToHashSet();

        var achievements = await db.Achievements.AsNoTracking().Where(a => a.IsPublished).OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id).ToListAsync(ct);
        var services = await db.Services.AsNoTracking().Where(s => s.IsPublished).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToListAsync(ct);

        return new PortfolioResponse(
            PublicProfile.From(profile),
            categories.Where(c => c.Skills.Count > 0)
                .Select(c => new PublicSkillCategory(c.Name, c.Icon, c.Skills.Select(s => new PublicSkill(s.Name, s.Proficiency)).ToList())).ToList(),
            experience.Select(PublicExperience.From).ToList(),
            projects.Select(PublicProject.From).ToList(),
            education.Select(PublicEducation.From).ToList(),
            achievements.Select(a => new PublicAchievement(a.Title, a.Kind, a.Issuer, a.Description, a.Date, a.DateLabel, a.VerificationUrl,
                a.DocumentId is { } id && publicDocIds.Contains(id) ? DocumentUrl(id) : null)).ToList(),
            services.Select(s => new PublicService(s.Title, s.Description, s.Icon)).ToList(),
            publicDocs.Select(d => new PublicDocument(d.Id, d.Title, d.Description, d.Kind, d.Version, d.OriginalFileName, d.SizeBytes, DocumentUrl(d.Id))).ToList(),
            publicDocs.Any(d => d.Kind == DocumentKind.Cv) ? CvUrl : null);
    }

    public IQueryable<Project> PublishedProjects() =>
        db.Projects.AsNoTracking().Where(p => p.IsPublished)
            .Include(p => p.Images)
            .OrderByDescending(p => p.IsFeatured).ThenBy(p => p.DisplayOrder).ThenBy(p => p.Id);

    /// <summary>The primary public CV, falling back to the most recently updated public CV.</summary>
    public Task<Document?> GetPublicCvAsync(CancellationToken ct = default) =>
        db.Documents.AsNoTracking().Where(d => d.Kind == DocumentKind.Cv && d.IsPublic)
            .OrderByDescending(d => d.IsPrimary).ThenByDescending(d => d.UpdatedAt).FirstOrDefaultAsync(ct);

    public static string DocumentUrl(Guid id) => $"/api/public/documents/{id}/download";
}
