using Microsoft.Extensions.Logging;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Models;

namespace PAFA.Infrastructure.Services;

/// <summary>
/// POC implementation — logs the email content instead of sending it.
/// Replace with SmtpEmailService in production.
/// </summary>
public class LoggingEmailService(ILogger<LoggingEmailService> log) : IEmailService
{
    public Task SendWelcomeEmailAsync(
        string recipientEmail, string firstName, string temporaryPassword,
        CancellationToken ct = default)
    {
        log.LogInformation(
            "[EMAIL-LOG] Welcome email ? {Email} | Name: {Name} | TempPassword: {Pass}",
            recipientEmail, firstName, temporaryPassword);

        return Task.CompletedTask;
    }

    public Task SendValidationFailureAsync(
        ValidationFailureEmailContext context,
        CancellationToken ct = default)
    {
        log.LogWarning(
            "[EMAIL-LOG] Validation failure notification ? {Recipients} | File: {File} | Period: {Period} | Source: {Source} | TotalErrors: {Errors}",
            string.Join(";", context.Recipients),
            context.FileName,
            context.ReportingPeriod,
            context.SourceSystem,
            context.AllErrors.Count);

        foreach (var err in context.AllErrors.Take(10))
        {
            log.LogWarning(
                "[EMAIL-LOG]   Row {Row} | {Field} | [{Code}] {Msg} | Value: {Val}",
                err.RowNumber?.ToString() ?? "—", err.ColumnName, err.ErrorCode, err.ErrorMessage, err.OriginalValue);
        }

        return Task.CompletedTask;
    }

    public Task SendIngestionFailureAsync(
        IngestionFailureEmailContext context,
        CancellationToken ct = default)
    {
        log.LogError(
            "[EMAIL-LOG] INGESTION FAILURE NOTIFICATION ? {Recipients} | Period: {Year}-{Month:D2} | Trigger: {Trigger} | Retries: {Retries} | Error: {Error} | FailedAt: {FailedAt:u}",
            string.Join(";", context.Recipients),
            context.Year,
            context.Month,
            context.TriggerSource,
            context.RetryAttempts,
            context.ErrorMessage,
            context.FailedAtUtc);

        return Task.CompletedTask;
    }
}



