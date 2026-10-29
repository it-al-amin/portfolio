namespace Portfolio.Api.Services;

public class StorageOptions
{
    /// <summary>Root folder for uploads. Relative paths resolve against the content root. Must not be inside wwwroot.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";
    public long MaxDocumentBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
}

/// <summary>
/// SMTP defaults from configuration ("Smtp" section, or flat SMTP_* environment variables).
/// Non-secret values can be overridden from the admin UI; the password can only come from here.
/// </summary>
public class SmtpOptions
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string? RecipientEmail { get; set; }

    public static SmtpOptions FromConfiguration(IConfiguration config)
    {
        var o = config.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
        o.Host = config["SMTP_HOST"] ?? o.Host;
        o.Port = int.TryParse(config["SMTP_PORT"], out var port) ? port : o.Port;
        o.Username = config["SMTP_USERNAME"] ?? o.Username;
        o.Password = config["SMTP_PASSWORD"] ?? o.Password;
        o.FromEmail = config["SMTP_FROM_EMAIL"] ?? o.FromEmail;
        o.FromName = config["SMTP_FROM_NAME"] ?? o.FromName;
        o.RecipientEmail = config["CONTACT_RECIPIENT_EMAIL"] ?? o.RecipientEmail;
        return o;
    }
}

public class RateLimitOptions
{
    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
    public int ContactPermitLimit { get; set; } = 5;
    public int ContactWindowSeconds { get; set; } = 900;
}
