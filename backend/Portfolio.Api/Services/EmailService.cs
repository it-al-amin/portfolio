using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Portfolio.Api.Data;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Services;

public record OutgoingEmail(string To, string Subject, string Html, string Text, string? ReplyTo = null, string? ReplyToName = null);

/// <summary>Effective SMTP configuration: admin UI values override configuration defaults; the password only ever comes from configuration.</summary>
public record EffectiveEmailConfig(
    string? Host, int Port, string? Username, string? Password, SmtpSecurity Security,
    string? FromEmail, string? FromName, string? RecipientEmail, EmailSettings Settings)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromEmail) && !string.IsNullOrWhiteSpace(RecipientEmail);
    public bool HasPassword => !string.IsNullOrEmpty(Password);
}

public interface IEmailSender
{
    Task SendAsync(EffectiveEmailConfig config, OutgoingEmail email, CancellationToken ct = default);
}

public class SmtpEmailSender : IEmailSender
{
    public async Task SendAsync(EffectiveEmailConfig config, OutgoingEmail email, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.FromName ?? config.FromEmail, config.FromEmail!));
        message.To.Add(MailboxAddress.Parse(email.To));
        if (email.ReplyTo is not null) message.ReplyTo.Add(new MailboxAddress(email.ReplyToName ?? email.ReplyTo, email.ReplyTo));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { HtmlBody = email.Html, TextBody = email.Text }.ToMessageBody();

        var security = config.Security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.Auto,
        };

        using var client = new SmtpClient { Timeout = 15000 };
        await client.ConnectAsync(config.Host!, config.Port, security, ct);
        if (!string.IsNullOrEmpty(config.Username)) await client.AuthenticateAsync(config.Username, config.Password ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

public class EmailConfigProvider(PortfolioDbContext db, SmtpOptions defaults)
{
    public async Task<EffectiveEmailConfig> GetAsync(CancellationToken ct = default)
    {
        var s = await GetSettingsAsync(db, ct);
        return new EffectiveEmailConfig(
            Host: Coalesce(s.SmtpHost, defaults.Host),
            Port: s.SmtpPort ?? defaults.Port ?? 587,
            Username: Coalesce(s.SmtpUsername, defaults.Username),
            Password: defaults.Password,
            Security: s.SmtpSecurity,
            FromEmail: Coalesce(s.FromEmail, defaults.FromEmail),
            FromName: Coalesce(s.SenderName, defaults.FromName),
            RecipientEmail: Coalesce(s.RecipientEmail, defaults.RecipientEmail),
            Settings: s);
    }

    public static async Task<EmailSettings> GetSettingsAsync(PortfolioDbContext db, CancellationToken ct = default)
    {
        var s = await db.EmailSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = new EmailSettings();
            db.EmailSettings.Add(s);
            await db.SaveChangesAsync(ct);
        }
        return s;
    }

    private static string? Coalesce(string? a, string? b) => string.IsNullOrWhiteSpace(a) ? b : a;
}

