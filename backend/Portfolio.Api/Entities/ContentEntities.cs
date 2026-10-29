namespace Portfolio.Api.Entities;

/// <summary>Base for portfolio content that can be ordered and published/unpublished.</summary>
public abstract class OrderedEntity
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Experience : OrderedEntity
{
    public string Company { get; set; } = "";
    public string Designation { get; set; } = "";
    public string? EmploymentType { get; set; }
    public string? Location { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Summary { get; set; }
    public List<string> Responsibilities { get; set; } = [];
    public List<string> Technologies { get; set; } = [];
}

public class SkillCategory : OrderedEntity
{
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public List<Skill> Skills { get; set; } = [];
}

public class Skill : OrderedEntity
{
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    /// <summary>Optional label (e.g. "Advanced"). Deliberately not a percentage bar.</summary>
    public string? Proficiency { get; set; }
}

public class Project : OrderedEntity
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Category { get; set; }
    public string Summary { get; set; } = "";
    public string? Description { get; set; }
    public string? Role { get; set; }
    public List<string> Technologies { get; set; } = [];
    public List<string> Features { get; set; } = [];
    public List<string> Responsibilities { get; set; } = [];
    public string? LiveUrl { get; set; }
    public string? SourceUrl { get; set; }
    public bool IsFeatured { get; set; }
    /// <summary>Ordered screenshots; the first one is used as the thumbnail.</summary>
    public List<ProjectImage> Images { get; set; } = [];
}

public class ProjectImage
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Guid MediaId { get; set; }
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
}

public class Education : OrderedEntity
{
    public string Degree { get; set; } = "";
    public string? FieldOfStudy { get; set; }
    public string Institution { get; set; } = "";
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Grade { get; set; }
    public string? Description { get; set; }
}

public enum AchievementKind { Achievement, Certification }

public class Achievement : OrderedEntity
{
    public string Title { get; set; } = "";
    public AchievementKind Kind { get; set; }
    public string? Issuer { get; set; }
    public string? Description { get; set; }
    public DateOnly? Date { get; set; }
    public string? DateLabel { get; set; }
    public string? VerificationUrl { get; set; }
    /// <summary>Linked certificate document; only exposed publicly when that document is public.</summary>
    public Guid? DocumentId { get; set; }
}

public class ServiceOffering : OrderedEntity
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Icon { get; set; }
}
