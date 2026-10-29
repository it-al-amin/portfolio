using System.Net;
using System.Text;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Services;

/// <summary>
/// Table-based HTML emails with inline styles for broad client support.
/// Every user-supplied value is HTML-encoded before insertion.
/// </summary>
public static class EmailTemplates
{
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string Multiline(string? value) => E(value).Replace("\r\n", "\n").Replace("\n", "<br>");

    private static string Layout(string preheader, string heading, string bodyHtml, string footer) => $"""
        <!doctype html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(heading)}</title></head>
        <body style="margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#111827;">
        <span style="display:none;max-height:0;overflow:hidden;opacity:0;">{E(preheader)}</span>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;padding:24px 12px;">
          <tr><td align="center">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;">
              <tr><td style="background:#0f3d5e;padding:20px 28px;color:#ffffff;font-size:18px;font-weight:600;">{E(heading)}</td></tr>
              <tr><td style="padding:24px 28px;font-size:15px;line-height:1.6;">{bodyHtml}</td></tr>
              <tr><td style="padding:16px 28px;background:#f9fafb;color:#6b7280;font-size:12px;line-height:1.5;">{footer}</td></tr>
            </table>
          </td></tr>
        </table>
        </body></html>
        """;

    private static string Row(string label, string valueHtml) =>
        $"""<tr><td style="padding:6px 12px 6px 0;color:#6b7280;white-space:nowrap;vertical-align:top;">{E(label)}</td><td style="padding:6px 0;">{valueHtml}</td></tr>""";

    public static OutgoingEmail Notification(ContactMessage m, string recipient, string subjectPrefix, string siteName)
    {
        var submitted = m.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var rows = new StringBuilder()
            .Append(Row("Name", E(m.Name)))
            .Append(Row("Email", $"""<a href="mailto:{E(m.Email)}" style="color:#0f5f8c;">{E(m.Email)}</a>"""))
            .Append(string.IsNullOrEmpty(m.Phone) ? "" : Row("Phone", E(m.Phone)))
            .Append(Row("Subject", E(m.Subject)))
            .Append(Row("Submitted", E(submitted)));

        var body = $"""
            <p style="margin:0 0 16px;">You received a new message through your portfolio contact form.</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="font-size:14px;margin-bottom:16px;">{rows}</table>
            <div style="border-left:3px solid #0f5f8c;background:#f3f8fb;padding:12px 16px;border-radius:4px;">{Multiline(m.Message)}</div>
            <p style="margin:20px 0 0;"><a href="mailto:{E(m.Email)}?subject={Uri.EscapeDataString("Re: " + m.Subject)}" style="display:inline-block;background:#0f5f8c;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:6px;">Reply to {E(m.Name)}</a></p>
            """;

        var text = $"""
            New portfolio inquiry

            Name: {m.Name}
            Email: {m.Email}
            {(string.IsNullOrEmpty(m.Phone) ? "" : $"Phone: {m.Phone}\n")}Subject: {m.Subject}
            Submitted: {submitted}

            {m.Message}

            Reply directly to this email to respond to {m.Name}.
            """;

        return new OutgoingEmail(
            recipient,
            $"{subjectPrefix} {m.Subject}".Trim(),
            Layout($"New message from {m.Name}", "New portfolio inquiry", body, $"Sent by the contact form on {E(siteName)}. Replying to this email goes to the visitor."),
            text,
            ReplyTo: m.Email,
            ReplyToName: m.Name);
    }

    /// <summary>Visitor confirmation. A custom auto-reply body (with {name} and {subject} placeholders) replaces the default text when set.</summary>
    public static OutgoingEmail VisitorConfirmation(ContactMessage m, EmailSettings s, string ownerName, string? replyTo)
    {
        var subject = string.IsNullOrWhiteSpace(s.AutoReplySubject) ? $"Thanks for getting in touch, {m.Name}" : Fill(s.AutoReplySubject, m);
        var bodyText = string.IsNullOrWhiteSpace(s.AutoReplyBody)
            ? $"Hi {m.Name},\n\nThank you for your message about \"{m.Subject}\". I have received it and will get back to you as soon as I can.\n\nBest regards,\n{ownerName}"
            : Fill(s.AutoReplyBody, m);

        var body = $"""
            <p style="margin:0 0 16px;">{Multiline(bodyText)}</p>
            <p style="margin:16px 0 4px;color:#6b7280;font-size:13px;">A copy of your message:</p>
            <div style="border-left:3px solid #d1d5db;background:#f9fafb;padding:12px 16px;border-radius:4px;color:#374151;font-size:14px;"><strong>{E(m.Subject)}</strong><br>{Multiline(m.Message)}</div>
            """;

        return new OutgoingEmail(
            m.Email, subject,
            Layout("Your message was received", "Message received", body, $"You are receiving this because you submitted the contact form on {E(ownerName)}'s portfolio."),
            $"{bodyText}\n\n---\nYour message:\n{m.Subject}\n\n{m.Message}",
            ReplyTo: replyTo, ReplyToName: ownerName);
    }

    public static OutgoingEmail Test(string to, string siteName) => new(
        to, $"Test email from {siteName}",
        Layout("SMTP test", "Email delivery works", "<p style=\"margin:0;\">This is a test message from your portfolio admin dashboard. If you can read this, your email settings are working.</p>", E(siteName)),
        "This is a test message from your portfolio admin dashboard. If you can read this, your email settings are working.");

    private static string Fill(string template, ContactMessage m) =>
        template.Replace("{name}", m.Name).Replace("{subject}", m.Subject);
}
