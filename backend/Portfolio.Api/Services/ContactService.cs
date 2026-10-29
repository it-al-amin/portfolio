using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Services;

public enum ContactOutcome { Accepted, Duplicate, Unavailable }

/// <summary>
/// Submission strategy:
/// - Inbox enabled (default): the message is saved first; success means "saved". Email notification is
///   best-effort and its result is recorded on the message so the admin can see failed deliveries.
/// - Inbox disabled: email delivery is the only record, so success requires the email to be sent.
/// Identical submissions (same email, subject and message) within 24 hours are not stored or sent twice.
/// </summary>
public class ContactService(
    PortfolioDbContext db,
    EmailConfigProvider configProvider,
    IEmailSender sender,
    ILogger<ContactService> logger)
{
    public async Task<ContactOutcome> SubmitAsync(ContactRequest request, CancellationToken ct = default)
    {
        var message = new ContactMessage
        {
            Name = Clean.Text(request.Name),
            Email = Clean.Text(request.Email),
            Phone = Clean.Optional(request.Phone),
            Subject = Clean.Text(request.Subject),
            Message = Clean.Multiline(request.Message),
        };
        message.ContentHash = Hash($"{message.Email.ToLowerInvariant()}\n{message.Subject}\n{message.Message}");

        var since = DateTime.UtcNow.AddHours(-24);
        if (await db.ContactMessages.AnyAsync(m => m.ContentHash == message.ContentHash && m.CreatedAt >= since, ct))
            return ContactOutcome.Duplicate;

        var config = await configProvider.GetAsync(ct);
        var settings = config.Settings;
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var ownerName = profile?.FullName ?? "Portfolio";

        if (!settings.SaveToInbox && (!config.IsConfigured || !settings.NotifyOnNewMessage))
        {
            logger.LogWarning("Contact submission rejected: inbox disabled and email notifications are not available.");
            return ContactOutcome.Unavailable;
        }

        if (settings.SaveToInbox)
        {
            message.NotificationStatus = !settings.NotifyOnNewMessage ? NotificationStatus.Disabled
                : config.IsConfigured ? NotificationStatus.Pending : NotificationStatus.NotConfigured;
            db.ContactMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var notified = false;
        if (settings.NotifyOnNewMessage && config.IsConfigured)
        {
            notified = await TrySendAsync(config, EmailTemplates.Notification(message, config.RecipientEmail!, settings.SubjectPrefix, ownerName), "notification", ct);
            if (settings.SaveToInbox)
            {
                message.NotificationStatus = notified ? NotificationStatus.Sent : NotificationStatus.Failed;
                await db.SaveChangesAsync(ct);
            }
            else if (!notified)
            {
                return ContactOutcome.Unavailable;
            }
        }

        if (settings.SendVisitorConfirmation && config.IsConfigured)
        {
            var replyTo = profile is { ShowEmail: true, PublicEmail: not null } ? profile.PublicEmail : null;
            await TrySendAsync(config, EmailTemplates.VisitorConfirmation(message, settings, ownerName, replyTo), "visitor confirmation", ct);
        }

        return ContactOutcome.Accepted;
    }

    private async Task<bool> TrySendAsync(EffectiveEmailConfig config, OutgoingEmail email, string kind, CancellationToken ct)
    {
        try
        {
            await sender.SendAsync(config, email, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log the failure type only; never the message body, addresses or credentials.
            logger.LogError("Failed to send {Kind} email via {Host}:{Port}: {Error}", kind, config.Host, config.Port, ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
