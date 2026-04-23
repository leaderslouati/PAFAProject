using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Models;

namespace PAFA.Infrastructure.Services.Notifications;

/// <summary>
/// Production email service that sends validation-failure notifications via SMTP.
/// The email body is a clean HTML table (first 10 errors) and a CSV attachment
/// containing the full error list.
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly NotificationSettings _settings;
    private readonly ILogger<SmtpEmailService> _log;

    public SmtpEmailService(
        IOptions<NotificationSettings> settings,
        ILogger<SmtpEmailService> log)
    {
        _settings = settings.Value;
        _log = log;
    }

    public async Task SendWelcomeEmailAsync(
        string recipientEmail, string firstName, string temporaryPassword,
        CancellationToken ct = default)
    {
        var subject = $"Welcome to the PAFA Platform, {firstName}!";
        var body = $"<p>Hello {WebUtility.HtmlEncode(firstName)},</p>" +
                   $"<p>Your account has been created. Your temporary password is: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>" +
                   "<p>Please change it on first login.</p>";

        using var msg = new MailMessage(_settings.SenderEmail, recipientEmail, subject, body)
        {
            IsBodyHtml = true
        };

        await SendAsync(msg, ct);
    }

    public async Task SendValidationFailureAsync(
        ValidationFailureEmailContext context,
        CancellationToken ct = default)
    {
        var subject = $"PAFA Validation Failure — {context.FileName} ({context.ReportingPeriod})";
        var htmlBody = EmailContentBuilder.BuildHtmlBody(context);
        var csvBytes = EmailContentBuilder.BuildCsvBytes(context.AllErrors);

        using var msg = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        foreach (var recipient in context.Recipients)
            msg.To.Add(recipient);

        var csvStream = new MemoryStream(csvBytes);
        msg.Attachments.Add(new Attachment(csvStream, $"{context.FileName}_errors.csv", "text/csv"));

        await SendAsync(msg, ct);

        _log.LogInformation(
            "[SMTP] Validation failure email sent | File: {File} | Recipients: {Recipients}",
            context.FileName, string.Join(";", context.Recipients));
    }

    private async Task SendAsync(MailMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.SmtpUseSsl,
            Credentials = string.IsNullOrEmpty(_settings.SmtpUsername)
                ? null
                : new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword)
        };

        await client.SendMailAsync(message, ct);
    }
}
