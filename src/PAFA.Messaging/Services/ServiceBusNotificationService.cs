using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Models;
using PAFA.Messaging.Configuration;
using PAFA.Messaging.Messages;

namespace PAFA.Messaging.Services;

/// <summary>
/// Production notification service — publishes PAFA notification events to
/// Azure Service Bus topics instead of sending SMTP emails directly.
///
/// A downstream consumer (Azure Function / Logic App) subscribes to each topic
/// and is responsible for the actual email delivery, Teams alert, etc.
///
/// Topics:
///   pafa-validation-failure  → ValidateFileCommandHandler (blocking errors)
///   pafa-ingestion-failure   → DownloadParrFilesCommandHandler (AC10, all retries exhausted)
///   pafa-user-welcome        → CreateUserCommandHandler (new user provisioning)
/// </summary>
public sealed class ServiceBusNotificationService : IEmailService, IAsyncDisposable
{
    private readonly ServiceBusClient   _client;
    private readonly ServiceBusSettings _settings;
    private readonly ILogger<ServiceBusNotificationService> _log;

    // One sender per topic — created lazily and reused.
    private ServiceBusSender? _validationSender;
    private ServiceBusSender? _ingestionSender;
    private ServiceBusSender? _welcomeSender;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false
    };

    public ServiceBusNotificationService(
        IOptions<ServiceBusSettings> settings,
        ILogger<ServiceBusNotificationService> log)
    {
        _settings = settings.Value;
        _log      = log;
        _client   = new ServiceBusClient(_settings.ConnectionString);
    }

    // ── IEmailService implementation ──────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendValidationFailureAsync(
        ValidationFailureEmailContext context,
        CancellationToken ct = default)
    {
        var message = new ValidationFailureMessage(
            IngestionFileId: context.IngestionFileId,
            FileName:        context.FileName,
            ReportingPeriod: context.ReportingPeriod,
            SourceSystem:    context.SourceSystem,
            TotalErrors:     context.AllErrors.Count,
            Errors: context.AllErrors
                .Select(e => new ValidationErrorPayload(
                    e.RowNumber, e.ColumnName, e.ErrorCode,
                    e.Severity, e.ErrorMessage, e.OriginalValue))
                .ToList(),
            Recipients:     context.Recipients,
            PublishedAtUtc: DateTime.UtcNow);

        _validationSender ??= _client.CreateSender(_settings.ValidationFailureTopic);

        var sbMessage = BuildMessage(message, "ValidationFailure");
        sbMessage.ApplicationProperties["FileName"]        = context.FileName;
        sbMessage.ApplicationProperties["ReportingPeriod"] = context.ReportingPeriod;
        sbMessage.ApplicationProperties["TotalErrors"]     = context.AllErrors.Count;

        await _validationSender.SendMessageAsync(sbMessage, ct);

        _log.LogInformation(
            "[SERVICE-BUS] ValidationFailure published → topic '{Topic}' | File: {File} | Errors: {Count}",
            _settings.ValidationFailureTopic, context.FileName, context.AllErrors.Count);
    }

    /// <inheritdoc/>
    public async Task SendIngestionFailureAsync(
        IngestionFailureEmailContext context,
        CancellationToken ct = default)
    {
        var message = new IngestionFailureMessage(
            Year:           context.Year,
            Month:          context.Month,
            TriggerSource:  context.TriggerSource,
            ErrorMessage:   context.ErrorMessage,
            RetryAttempts:  context.RetryAttempts,
            FailedAtUtc:    context.FailedAtUtc,
            Recipients:     context.Recipients);

        _ingestionSender ??= _client.CreateSender(_settings.IngestionFailureTopic);

        var sbMessage = BuildMessage(message, "IngestionFailure");
        sbMessage.ApplicationProperties["Period"]          = $"{context.Year}-{context.Month:D2}";
        sbMessage.ApplicationProperties["RetryAttempts"]  = context.RetryAttempts;
        sbMessage.ApplicationProperties["TriggerSource"]  = context.TriggerSource;

        await _ingestionSender.SendMessageAsync(sbMessage, ct);

        _log.LogInformation(
            "[SERVICE-BUS] IngestionFailure published → topic '{Topic}' | Period: {Year}-{Month:D2} | Retries: {Retries}",
            _settings.IngestionFailureTopic, context.Year, context.Month, context.RetryAttempts);
    }

    /// <inheritdoc/>
    public async Task SendWelcomeEmailAsync(
        string recipientEmail,
        string firstName,
        string temporaryPassword,
        CancellationToken ct = default)
    {
        var message = new WelcomeMessage(
            RecipientEmail:    recipientEmail,
            FirstName:         firstName,
            TemporaryPassword: temporaryPassword,
            PublishedAtUtc:    DateTime.UtcNow);

        _welcomeSender ??= _client.CreateSender(_settings.WelcomeTopic);

        var sbMessage = BuildMessage(message, "UserWelcome");
        sbMessage.ApplicationProperties["RecipientEmail"] = recipientEmail;
        sbMessage.ApplicationProperties["FirstName"]      = firstName;

        await _welcomeSender.SendMessageAsync(sbMessage, ct);

        _log.LogInformation(
            "[SERVICE-BUS] UserWelcome published → topic '{Topic}' | Recipient: {Email}",
            _settings.WelcomeTopic, recipientEmail);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ServiceBusMessage BuildMessage<T>(T payload, string notificationType)
    {
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType   = "application/json",
            Subject       = notificationType,
            MessageId     = Guid.NewGuid().ToString(),
            CorrelationId = notificationType
        };
        sbMessage.ApplicationProperties["NotificationType"] = notificationType;
        sbMessage.ApplicationProperties["Source"]           = "PAFA_PLATFORM";
        return sbMessage;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_validationSender is not null) await _validationSender.DisposeAsync();
        if (_ingestionSender  is not null) await _ingestionSender.DisposeAsync();
        if (_welcomeSender    is not null) await _welcomeSender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
