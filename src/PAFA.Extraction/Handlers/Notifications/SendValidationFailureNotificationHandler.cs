using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Models;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Notifications;

namespace PAFA.Extraction.Handlers.Notifications;

/// <summary>
/// Handles <see cref="SendValidationFailureNotificationCommand"/>:
///   1. Reads the recipient list from configuration.
///   2. Builds the <see cref="ValidationFailureEmailContext"/> and sends the email.
///   3. Persists a <see cref="ValidationNotification"/> audit record.
/// </summary>
public sealed class SendValidationFailureNotificationHandler
    : IRequestHandler<SendValidationFailureNotificationCommand, SendValidationFailureNotificationResult>
{
    private readonly IEmailService _email;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SendValidationFailureNotificationHandler> _log;
    private readonly IReadOnlyList<string> _recipients;

    public SendValidationFailureNotificationHandler(
        IEmailService email,
        IUnitOfWork uow,
        IConfiguration configuration,
        ILogger<SendValidationFailureNotificationHandler> log)
    {
        _email = email;
        _uow   = uow;
        _log   = log;

        // Read the recipient list from configuration (falls back to empty list)
        _recipients = configuration
            .GetSection("Notifications:ValidationFailureRecipients")
            .Get<List<string>>() ?? [];
    }

    public async Task<SendValidationFailureNotificationResult> Handle(
        SendValidationFailureNotificationCommand cmd,
        CancellationToken ct)
    {
        var ctx = new ValidationFailureEmailContext(
            cmd.IngestionFileId,
            cmd.FileName,
            cmd.ReportingPeriod,
            cmd.SourceSystem,
            _recipients,
            cmd.AllErrors);

        var notification = new ValidationNotification
        {
            IngestionFileId = cmd.IngestionFileId,
            FileName        = cmd.FileName,
            ReportingPeriod = cmd.ReportingPeriod,
            SourceSystem    = cmd.SourceSystem,
            Recipients      = string.Join(";", _recipients),
            TotalErrors     = cmd.AllErrors.Count,
            SentAt          = DateTime.UtcNow,
            CreatedBy       = "PAFA_SYSTEM"
        };

        try
        {
            await _email.SendValidationFailureAsync(ctx, ct);
            notification.Status = "SENT";

            _log.LogInformation(
                "[NOTIFICATION] Validation failure email sent | File: {File} | Period: {Period} | Errors: {Errors}",
                cmd.FileName, cmd.ReportingPeriod, cmd.AllErrors.Count);
        }
        catch (Exception ex)
        {
            notification.Status      = "FAILED";
            notification.ErrorDetail = ex.Message;

            _log.LogError(ex,
                "[NOTIFICATION] Failed to send validation failure email | File: {File}",
                cmd.FileName);
        }
        finally
        {
            // Always persist the audit record, regardless of send outcome
            await _uow.ValidationNotifications.AddAsync(notification, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return notification.Status == "SENT"
            ? new SendValidationFailureNotificationResult(true)
            : new SendValidationFailureNotificationResult(false, notification.ErrorDetail);
    }
}
