using System.ComponentModel.DataAnnotations;
using Portfolio.Api.Entities;
using Portfolio.Api.Services;

namespace Portfolio.Api.Dtos;

public interface IEntityInput<in T>
{
    bool IsPublished { get; }
    void ApplyTo(T entity);
}

public class ExperienceInput : IEntityInput<Experience>, IValidatableObject
{
    [Required, MaxLength(120)] public string Company { get; set; } = "";
    [Required, MaxLength(120)] public string Designation { get; set; } = "";
    [MaxLength(60)] public string? EmploymentType { get; set; }
    [MaxLength(120)] public string? Location { get; set; }
    [Required] public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    [MaxLength(1000)] public string? Summary { get; set; }
    [ListItems(30, 500)] public List<string> Responsibilities { get; set; } = [];
    [ListItems(40, 60)] public List<string> Technologies { get; set; } = [];
    public bool IsPublished { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!IsCurrent && EndDate is null)
            yield return new ValidationResult("End date is required unless this is your current position.", [nameof(EndDate)]);
        if (!IsCurrent && EndDate < StartDate)
            yield return new ValidationResult("End date must be after the start date.", [nameof(EndDate)]);
    }

    public void ApplyTo(Experience e)
    {
        e.Company = Clean.Text(Company);
        e.Designation = Clean.Text(Designation);
        e.EmploymentType = Clean.Optional(EmploymentType);
        e.Location = Clean.Optional(Location);
        e.StartDate = StartDate!.Value;
        e.IsCurrent = IsCurrent;
        e.EndDate = IsCurrent ? null : EndDate;
        e.Summary = Clean.Optional(Summary);
        e.Responsibilities = Clean.List(Responsibilities);
        e.Technologies = Clean.List(Technologies);
        e.IsPublished = IsPublished;
    }
}

public class SkillCategoryInput : IEntityInput<SkillCategory>
{
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(40)] public string? Icon { get; set; }
    public bool IsPublished { get; set; } = true;

    public void ApplyTo(SkillCategory e)
    {
        e.Name = Clean.Text(Name);
        e.Icon = Clean.Optional(Icon);
        e.IsPublished = IsPublished;
    }
}

public class SkillInput : IEntityInput<Skill>
{
    [Required, MaxLength(60)] public string Name { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")] public int CategoryId { get; set; }
    [AllowedValues(null, "", "Beginner", "Intermediate", "Advanced", "Expert")] public string? Proficiency { get; set; }
    public bool IsPublished { get; set; } = true;

    public void ApplyTo(Skill e)
    {
        e.Name = Clean.Text(Name);
        e.CategoryId = CategoryId;
        e.Proficiency = Clean.Optional(Proficiency);
        e.IsPublished = IsPublished;
    }
}

public class ProjectInput : IEntityInput<Project>
{
    [Required, MaxLength(150)] public string Title { get; set; } = "";
    [MaxLength(120), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug may contain lowercase letters, numbers and single hyphens.")]
    public string? Slug { get; set; }
    [MaxLength(80)] public string? Category { get; set; }
    [Required, MaxLength(400)] public string Summary { get; set; } = "";
    [MaxLength(8000)] public string? Description { get; set; }
    [MaxLength(200)] public string? Role { get; set; }
    [ListItems(40, 60)] public List<string> Technologies { get; set; } = [];
    [ListItems(30, 500)] public List<string> Features { get; set; } = [];
    [ListItems(30, 500)] public List<string> Responsibilities { get; set; } = [];
    [HttpUrl] public string? LiveUrl { get; set; }
    [HttpUrl] public string? SourceUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPublished { get; set; }

    public void ApplyTo(Project e)
    {
        e.Title = Clean.Text(Title);
        e.Category = Clean.Optional(Category);
        e.Summary = Clean.Text(Summary);
        e.Description = Clean.Optional(Description);
        e.Role = Clean.Optional(Role);
        e.Technologies = Clean.List(Technologies);
        e.Features = Clean.List(Features);
        e.Responsibilities = Clean.List(Responsibilities);
        e.LiveUrl = Clean.Optional(LiveUrl);
        e.SourceUrl = Clean.Optional(SourceUrl);
        e.IsFeatured = IsFeatured;
        e.IsPublished = IsPublished;
    }
}

public class EducationInput : IEntityInput<Education>, IValidatableObject
{
    [Required, MaxLength(150)] public string Degree { get; set; } = "";
    [MaxLength(150)] public string? FieldOfStudy { get; set; }
    [Required, MaxLength(200)] public string Institution { get; set; } = "";
    [Range(1950, 2100)] public int? StartYear { get; set; }
    [Range(1950, 2100)] public int? EndYear { get; set; }
    [MaxLength(60)] public string? Grade { get; set; }
    [MaxLength(2000)] public string? Description { get; set; }
    public bool IsPublished { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartYear is not null && EndYear is not null && EndYear < StartYear)
            yield return new ValidationResult("End year must not be before the start year.", [nameof(EndYear)]);
    }

    public void ApplyTo(Education e)
    {
        e.Degree = Clean.Text(Degree);
        e.FieldOfStudy = Clean.Optional(FieldOfStudy);
        e.Institution = Clean.Text(Institution);
        e.StartYear = StartYear;
        e.EndYear = EndYear;
        e.Grade = Clean.Optional(Grade);
        e.Description = Clean.Optional(Description);
        e.IsPublished = IsPublished;
    }
}

public class AchievementInput : IEntityInput<Achievement>
{
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    public AchievementKind Kind { get; set; }
    [MaxLength(150)] public string? Issuer { get; set; }
    [MaxLength(2000)] public string? Description { get; set; }
    public DateOnly? Date { get; set; }
    [MaxLength(40)] public string? DateLabel { get; set; }
    [HttpUrl] public string? VerificationUrl { get; set; }
    public Guid? DocumentId { get; set; }
    public bool IsPublished { get; set; } = true;

    public void ApplyTo(Achievement e)
    {
        e.Title = Clean.Text(Title);
        e.Kind = Kind;
        e.Issuer = Clean.Optional(Issuer);
        e.Description = Clean.Optional(Description);
        e.Date = Date;
        e.DateLabel = Clean.Optional(DateLabel);
        e.VerificationUrl = Clean.Optional(VerificationUrl);
        e.DocumentId = DocumentId;
        e.IsPublished = IsPublished;
    }
}

public class ServiceInput : IEntityInput<ServiceOffering>
{
    [Required, MaxLength(120)] public string Title { get; set; } = "";
    [Required, MaxLength(600)] public string Description { get; set; } = "";
    [MaxLength(40)] public string? Icon { get; set; }
    public bool IsPublished { get; set; } = true;

    public void ApplyTo(ServiceOffering e)
    {
        e.Title = Clean.Text(Title);
        e.Description = Clean.Text(Description);
        e.Icon = Clean.Optional(Icon);
        e.IsPublished = IsPublished;
    }
}

public class ProfileInput
{
    [Required, MaxLength(120)] public string FullName { get; set; } = "";
    [Required, MaxLength(160)] public string Headline { get; set; } = "";
    [MaxLength(200)] public string? Tagline { get; set; }
    [MaxLength(1200)] public string? Summary { get; set; }
    [MaxLength(6000)] public string? Bio { get; set; }
    [ListItems(20, 120)] public List<string> CareerInterests { get; set; } = [];
    [ListItems(6, 120)] public List<string> Highlights { get; set; } = [];
    [MaxLength(120)] public string? Location { get; set; }
    [EmailAddress, MaxLength(254)] public string? PublicEmail { get; set; }
    public bool ShowEmail { get; set; } = true;
    [MaxLength(30), RegularExpression(@"^\+?[0-9 ()-]{6,30}$", ErrorMessage = "Enter a valid phone number.")] public string? Phone { get; set; }
    public bool ShowPhone { get; set; }
    [MaxLength(20), RegularExpression(@"^[0-9]{8,15}$", ErrorMessage = "Use digits only in international format, without + or spaces (e.g. 8801XXXXXXXXX).")]
    public string? WhatsAppNumber { get; set; }
    [HttpUrl] public string? GitHubUrl { get; set; }
    [HttpUrl] public string? LinkedInUrl { get; set; }
    [HttpUrl] public string? WebsiteUrl { get; set; }
    [MaxLength(120)] public string? AvailabilityStatus { get; set; }
    public bool ShowAvailability { get; set; } = true;
    [MaxLength(70)] public string? SeoTitle { get; set; }
    [MaxLength(170)] public string? SeoDescription { get; set; }
    [HttpUrl] public string? SiteUrl { get; set; }

    public void ApplyTo(Profile p)
    {
        p.FullName = Clean.Text(FullName);
        p.Headline = Clean.Text(Headline);
        p.Tagline = Clean.Optional(Tagline);
        p.Summary = Clean.Optional(Summary);
        p.Bio = Clean.Optional(Bio);
        p.CareerInterests = Clean.List(CareerInterests);
        p.Highlights = Clean.List(Highlights);
        p.Location = Clean.Optional(Location);
        p.PublicEmail = Clean.Optional(PublicEmail);
        p.ShowEmail = ShowEmail;
        p.Phone = Clean.Optional(Phone);
        p.ShowPhone = ShowPhone;
        p.WhatsAppNumber = Clean.Optional(WhatsAppNumber);
        p.GitHubUrl = Clean.Optional(GitHubUrl);
        p.LinkedInUrl = Clean.Optional(LinkedInUrl);
        p.WebsiteUrl = Clean.Optional(WebsiteUrl);
        p.AvailabilityStatus = Clean.Optional(AvailabilityStatus);
        p.ShowAvailability = ShowAvailability;
        p.SeoTitle = Clean.Optional(SeoTitle);
        p.SeoDescription = Clean.Optional(SeoDescription);
        p.SiteUrl = Clean.Optional(SiteUrl)?.TrimEnd('/');
        p.UpdatedAt = DateTime.UtcNow;
    }
}

public class EmailSettingsInput
{
    [EmailAddress, MaxLength(254)] public string? RecipientEmail { get; set; }
    [MaxLength(100), SingleLine] public string? SenderName { get; set; }
    [EmailAddress, MaxLength(254)] public string? FromEmail { get; set; }
    [MaxLength(200), RegularExpression(@"^[A-Za-z0-9.-]+$", ErrorMessage = "Enter a host name such as smtp-relay.brevo.com.")] public string? SmtpHost { get; set; }
    [Range(1, 65535)] public int? SmtpPort { get; set; }
    [MaxLength(254)] public string? SmtpUsername { get; set; }
    public SmtpSecurity SmtpSecurity { get; set; }
    [MaxLength(40), SingleLine] public string SubjectPrefix { get; set; } = "";
    public bool NotifyOnNewMessage { get; set; }
    public bool SaveToInbox { get; set; }
    public bool SendVisitorConfirmation { get; set; }
    [MaxLength(150), SingleLine] public string? AutoReplySubject { get; set; }
    [MaxLength(3000)] public string? AutoReplyBody { get; set; }

    public void ApplyTo(EmailSettings s)
    {
        s.RecipientEmail = Clean.Optional(RecipientEmail);
        s.SenderName = Clean.Optional(SenderName);
        s.FromEmail = Clean.Optional(FromEmail);
        s.SmtpHost = Clean.Optional(SmtpHost);
        s.SmtpPort = SmtpPort;
        s.SmtpUsername = Clean.Optional(SmtpUsername);
        s.SmtpSecurity = SmtpSecurity;
        s.SubjectPrefix = Clean.Text(SubjectPrefix);
        s.NotifyOnNewMessage = NotifyOnNewMessage;
        s.SaveToInbox = SaveToInbox;
        s.SendVisitorConfirmation = SendVisitorConfirmation;
        s.AutoReplySubject = Clean.Optional(AutoReplySubject);
        s.AutoReplyBody = Clean.Optional(AutoReplyBody);
        s.UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>Returned to the admin UI. The SMTP password is never included, only whether one is configured.</summary>
public record EmailSettingsView(
    EmailSettings Settings,
    string? EffectiveHost, int EffectivePort, string? EffectiveUsername, string? EffectiveFromEmail,
    string? EffectiveRecipient, bool PasswordConfigured, bool IsConfigured);

public class DocumentUpdateInput
{
    [Required, MaxLength(150)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    public DocumentKind Kind { get; set; }
    [MaxLength(30)] public string? Version { get; set; }
    public bool IsPublic { get; set; }
    public bool IsPrimary { get; set; }
}

public record DocumentView(Guid Id, string Title, string? Description, DocumentKind Kind, string? Version,
    string OriginalFileName, string ContentType, long SizeBytes, bool IsPublic, bool IsPrimary, DateTime UploadedAt, DateTime UpdatedAt)
{
    public static DocumentView From(Document d) => new(d.Id, d.Title, d.Description, d.Kind, d.Version,
        d.OriginalFileName, d.ContentType, d.SizeBytes, d.IsPublic, d.IsPrimary, d.UploadedAt, d.UpdatedAt);
}

public record ReorderRequest([Required] List<int> Ids);
public record PublishRequest(bool IsPublished);
public record MessageStatusRequest(bool? IsRead, bool? IsReplied);
public record ImageCaptionRequest([MaxLength(200)] string? Caption);

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MaxLength(200)] public string Password { get; set; } = "";
}

public class ChangePasswordRequest
{
    [Required, MaxLength(200)] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(AdminAccountService.MinPasswordLength, ErrorMessage = "Use at least 8 characters."), MaxLength(200)] public string NewPassword { get; set; } = "";
}

public class TestEmailRequest
{
    [EmailAddress, MaxLength(254)] public string? To { get; set; }
}
