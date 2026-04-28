using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Interfaces;
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

        // Build attachment with explicit ContentDisposition so all SMTP clients
        // (including MailHog) recognise it as a downloadable file.
        var csvStream = new MemoryStream(csvBytes);
        csvStream.Position = 0;
        var csvFileName = $"{context.FileName}_errors.csv";

        var attachment = new Attachment(csvStream, new ContentType("text/csv; charset=utf-8"))
        {
            Name = csvFileName
        };
        attachment.ContentDisposition.DispositionType = DispositionTypeNames.Attachment;
        attachment.ContentDisposition.FileName        = csvFileName;
        attachment.ContentDisposition.CreationDate    = DateTime.UtcNow;
        attachment.ContentDisposition.ModificationDate = DateTime.UtcNow;

        msg.Attachments.Add(attachment);

        await SendAsync(msg, ct);

        _log.LogInformation(
            "[SMTP] Validation failure email sent | File: {File} | Recipients: {Recipients} | Attachment: {Csv} ({Bytes} bytes)",
            context.FileName, string.Join(";", context.Recipients), csvFileName, csvBytes.Length);
    }

    public async Task SendIngestionFailureAsync(
        IngestionFailureEmailContext context,
        CancellationToken ct = default)
    {
        var subject = $"PAFA Ingestion Failure — {context.Year}-{context.Month:D2} — All retries exhausted";
        var body = $"<h2>Ingestion Failure</h2>" +
                   $"<p><strong>Period:</strong> {context.Year}-{context.Month:D2}</p>" +
                   $"<p><strong>Trigger:</strong> {WebUtility.HtmlEncode(context.TriggerSource)}</p>" +
                   $"<p><strong>Retry attempts:</strong> {context.RetryAttempts}</p>" +
                   $"<p><strong>Failed at (UTC):</strong> {context.FailedAtUtc:u}</p>" +
                   $"<p><strong>Error:</strong> {WebUtility.HtmlEncode(context.ErrorMessage)}</p>" +
                   "<p>Please investigate and take manual action.</p>";

        using var msg = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        foreach (var recipient in context.Recipients)
            msg.To.Add(recipient);

        await SendAsync(msg, ct);

        _log.LogInformation(
            "[SMTP] Ingestion failure email sent | Period: {Year}-{Month:D2} | Recipients: {Recipients}",
            context.Year, context.Month, string.Join(";", context.Recipients));
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

