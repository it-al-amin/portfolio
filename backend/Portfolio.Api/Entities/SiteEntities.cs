namespace Portfolio.Api.Entities;

/// <summary>Single-row table holding the public profile and portfolio-wide settings.</summary>
public class Profile
{
    public int Id { get; set; } = 1;
    public string FullName { get; set; } = "";
    public string Headline { get; set; } = "";
    public string? Tagline { get; set; }
    public string? Summary { get; set; }
    public string? Bio { get; set; }
    public List<string> CareerInterests { get; set; } = [];
    /// <summary>Hero highlight stats, each formatted as "value | label".</summary>
    public List<string> Highlights { get; set; } = [];
    public string? Location { get; set; }

    public string? PublicEmail { get; set; }
    public bool ShowEmail { get; set; } = true;
    public string? Phone { get; set; }
    public bool ShowPhone { get; set; }
    /// <summary>International format digits, e.g. 8801XXXXXXXXX. Empty hides the WhatsApp button.</summary>
    public string? WhatsAppNumber { get; set; }

    public string? GitHubUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    public string? AvailabilityStatus { get; set; }
    public bool ShowAvailability { get; set; } = true;
    public Guid? ProfileImageId { get; set; }

    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    /// <summary>Public base URL (e.g. https://example.com) used for canonical links and the sitemap.</summary>
    public string? SiteUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SmtpSecurity { Auto, None, StartTls, SslOnConnect }

/// <summary>Single-row table with non-secret email settings. The SMTP password lives only in server configuration.</summary>
public class EmailSettings
{
    public int Id { get; set; } = 1;
    public string? RecipientEmail { get; set; }
    public string? SenderName { get; set; }
    public string? FromEmail { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public SmtpSecurity SmtpSecurity { get; set; } = SmtpSecurity.Auto;
    public string SubjectPrefix { get; set; } = "[Portfolio]";
    public bool NotifyOnNewMessage { get; set; } = true;
    public bool SaveToInbox { get; set; } = true;
    public bool SendVisitorConfirmation { get; set; }
    public string? AutoReplySubject { get; set; }
    public string? AutoReplyBody { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum DocumentKind { Cv, CoverLetter, Certificate, Other }

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DocumentKind Kind { get; set; }
    public string? Version { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool IsPublic { get; set; }
    /// <summary>The primary public CV is what /api/public/cv serves.</summary>
    public bool IsPrimary { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class MediaFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoredFileName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum NotificationStatus { Pending, Sent, Failed, NotConfigured, Disabled }

public class ContactMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public bool IsReplied { get; set; }
    public DateTime? RepliedAt { get; set; }
    public NotificationStatus NotificationStatus { get; set; }
    /// <summary>SHA-256 of email+subject+message, used to suppress duplicate submissions.</summary>
    public string ContentHash { get; set; } = "";
}

public class AdminUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>Rotated on password change so existing sessions are invalidated.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
