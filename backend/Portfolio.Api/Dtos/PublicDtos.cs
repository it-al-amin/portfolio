using System.ComponentModel.DataAnnotations;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Dtos;

public record PublicProfile(
    string FullName, string Headline, string? Tagline, string? Summary, string? Bio,
    List<string> CareerInterests, List<string> Highlights, string? Location,
    string? Email, string? Phone, string? WhatsAppNumber,
    string? GitHubUrl, string? LinkedInUrl, string? WebsiteUrl,
    string? AvailabilityStatus, string? ImageUrl, string? SeoTitle, string? SeoDescription)
{
    /// <summary>Only fields the owner marked public are exposed.</summary>
    public static PublicProfile From(Profile p) => new(
        p.FullName, p.Headline, p.Tagline, p.Summary, p.Bio, p.CareerInterests, p.Highlights, p.Location,
        p.ShowEmail ? p.PublicEmail : null,
        p.ShowPhone ? p.Phone : null,
        p.WhatsAppNumber,
        p.GitHubUrl, p.LinkedInUrl, p.WebsiteUrl,
        p.ShowAvailability ? p.AvailabilityStatus : null,
        p.ProfileImageId is { } id ? MediaUrl(id) : null,
        p.SeoTitle, p.SeoDescription);

    public static string MediaUrl(Guid id) => $"/api/media/{id}";
}

public record PublicSkill(string Name, string? Proficiency);
public record PublicSkillCategory(string Name, string? Icon, List<PublicSkill> Skills);

public record PublicExperience(int Id, string Company, string Designation, string? EmploymentType, string? Location,
    DateOnly StartDate, DateOnly? EndDate, bool IsCurrent, string? Summary, List<string> Responsibilities, List<string> Technologies)
{
    public static PublicExperience From(Experience e) => new(e.Id, e.Company, e.Designation, e.EmploymentType, e.Location,
        e.StartDate, e.EndDate, e.IsCurrent, e.Summary, e.Responsibilities, e.Technologies);
}

public record PublicProjectImage(string Url, string? Caption);

public record PublicProject(int Id, string Slug, string Title, string? Category, string Summary, string? Description, string? Role,
    List<string> Technologies, List<string> Features, List<string> Responsibilities,
    string? LiveUrl, string? SourceUrl, bool IsFeatured, List<PublicProjectImage> Images)
{
    public static PublicProject From(Project p) => new(p.Id, p.Slug, p.Title, p.Category, p.Summary, p.Description, p.Role,
        p.Technologies, p.Features, p.Responsibilities, p.LiveUrl, p.SourceUrl, p.IsFeatured,
        p.Images.OrderBy(i => i.DisplayOrder).Select(i => new PublicProjectImage(PublicProfile.MediaUrl(i.MediaId), i.Caption)).ToList());
}

public record PublicEducation(string Degree, string? FieldOfStudy, string Institution, int? StartYear, int? EndYear, string? Grade, string? Description)
{
    public static PublicEducation From(Education e) => new(e.Degree, e.FieldOfStudy, e.Institution, e.StartYear, e.EndYear, e.Grade, e.Description);
}

public record PublicAchievement(string Title, AchievementKind Kind, string? Issuer, string? Description,
    DateOnly? Date, string? DateLabel, string? VerificationUrl, string? DocumentUrl);

public record PublicService(string Title, string Description, string? Icon);

public record PublicDocument(Guid Id, string Title, string? Description, DocumentKind Kind, string? Version, string FileName, long SizeBytes, string DownloadUrl);

public record PortfolioResponse(
    PublicProfile Profile,
    List<PublicSkillCategory> Skills,
    List<PublicExperience> Experience,
    List<PublicProject> Projects,
    List<PublicEducation> Education,
    List<PublicAchievement> Achievements,
    List<PublicService> Services,
    List<PublicDocument> Documents,
    string? CvUrl);

public class ContactRequest
{
    [Required, MinLength(2), MaxLength(100), SingleLine] public string Name { get; set; } = "";
    [Required, EmailAddress, MaxLength(254), SingleLine] public string Email { get; set; } = "";
    [MaxLength(30), RegularExpression(@"^\+?[0-9 ()-]{6,30}$", ErrorMessage = "Enter a valid phone number.")] public string? Phone { get; set; }
    [Required, MinLength(3), MaxLength(150), SingleLine] public string Subject { get; set; } = "";
    [Required, MinLength(10), MaxLength(5000)] public string Message { get; set; } = "";
    /// <summary>Honeypot. Hidden from people; bots tend to fill it in.</summary>
    [MaxLength(200)] public string? Website { get; set; }
}

public record ContactResponse(string Message);
