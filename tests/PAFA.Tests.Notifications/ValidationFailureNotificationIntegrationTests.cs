using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Models;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Notifications;
using PAFA.Extraction.Handlers.Notifications;
using PAFA.Infrastructure.Services;
using PAFA.Infrastructure.Services.Notifications;

namespace PAFA.Tests.Notifications;

/// <summary>
/// Integration-style tests for the validation failure notification flow.
///
/// 1. LoggingEmailService tests: run always, verify the full handler?email pipeline with the logging stub.
/// 2. SmtpEmailService tests: require MailHog on localhost:1025 — skip automatically otherwise.
///    Start MailHog: docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
///    View emails:  http://localhost:8025
/// </summary>
public class ValidationFailureNotificationIntegrationTests
{
    // ?? Helpers ???????????????????????????????????????????????????????????

    private static SendValidationFailureNotificationCommand BuildCommand(int errorCount = 8)
    {
        var errors = Enumerable.Range(1, errorCount)
            .Select(i => new ValidationErrorItem(
                RowNumber: i,
                ColumnName: i % 3 == 0 ? "EnergyValue" : "ShipperShortCode",
                ErrorCode: i % 3 == 0 ? "VAL-008" : "VAL-005",
                Severity: i % 5 == 0 ? "WARNING" : "ERROR",
                ErrorMessage: $"Validation error on row {i}",
                OriginalValue: i % 3 == 0 ? "-999" : null))
            .ToList();

        return new SendValidationFailureNotificationCommand(
            IngestionFileId: Guid.NewGuid(),
            FileName: "MOD520A_2025_07.xlsx",
            ReportingPeriod: "2025-07",
            SourceSystem: "SharePoint",
            AllErrors: errors);
    }

    private static (Mock<IValidationNotificationRepository> repoMock, Mock<IUnitOfWork> uowMock)
        BuildUowMocks()
    {
        var repoMock = new Mock<IValidationNotificationRepository>();
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<ValidationNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.SetupGet(u => u.ValidationNotifications).Returns(repoMock.Object);
        uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (repoMock, uowMock);
    }

    private static IConfiguration BuildConfig(params string[] recipients)
    {
        var dict = new Dictionary<string, string?>();
        for (int i = 0; i < recipients.Length; i++)
            dict[$"Notifications:ValidationFailureRecipients:{i}"] = recipients[i];
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 1: Full pipeline with LoggingEmailService (always runs)
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithLoggingEmailService_SendsAndPersistsAudit()
    {
        // Arrange — use the real LoggingEmailService (no SMTP needed)
        var emailService = new LoggingEmailService(
            NullLogger<LoggingEmailService>.Instance);

        var (repoMock, uowMock) = BuildUowMocks();
        var config = BuildConfig("ops@test.com", "lead@test.com");

        var handler = new SendValidationFailureNotificationHandler(
            emailService, uowMock.Object, config,
            NullLogger<SendValidationFailureNotificationHandler>.Instance);

        var cmd = BuildCommand(errorCount: 12);

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n =>
                n.Status == "SENT" &&
                n.FileName == "MOD520A_2025_07.xlsx" &&
                n.TotalErrors == 12 &&
                n.Recipients == "ops@test.com;lead@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 2: Full pipeline with blocking errors ? notification triggered
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithBlockingErrors_AllErrorsPassedToEmailContext()
    {
        // Arrange
        var emailMock = new Mock<IEmailService>();
        ValidationFailureEmailContext? capturedCtx = null;

        emailMock
            .Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => capturedCtx = ctx)
            .Returns(Task.CompletedTask);

        var (_, uowMock) = BuildUowMocks();
        var config = BuildConfig("ops@test.com");

        var handler = new SendValidationFailureNotificationHandler(
            emailMock.Object, uowMock.Object, config,
            NullLogger<SendValidationFailureNotificationHandler>.Instance);

        var cmd = BuildCommand(errorCount: 25);

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert — all 25 errors are in the email context (not capped)
        Assert.NotNull(capturedCtx);
        Assert.Equal(25, capturedCtx!.AllErrors.Count);
        Assert.Equal("MOD520A_2025_07.xlsx", capturedCtx.FileName);
        Assert.Equal("2025-07", capturedCtx.ReportingPeriod);
        Assert.Contains("ops@test.com", capturedCtx.Recipients);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 2b: Exactly 10 errors ? notification sent with all 10 errors
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithExactly10Errors_SendsNotificationWithAll10Errors()
    {
        // Arrange
        var emailMock = new Mock<IEmailService>();
        ValidationFailureEmailContext? capturedCtx = null;

        emailMock
            .Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => capturedCtx = ctx)
            .Returns(Task.CompletedTask);

        var (repoMock, uowMock) = BuildUowMocks();
        var config = BuildConfig("ops-team@company.com", "data-lead@company.com");

        var handler = new SendValidationFailureNotificationHandler(
            emailMock.Object, uowMock.Object, config,
            NullLogger<SendValidationFailureNotificationHandler>.Instance);

        var cmd = BuildCommand(errorCount: 10);

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedCtx);
        Assert.Equal(10, capturedCtx!.AllErrors.Count);

        // Both recipients received the notification
        Assert.Equal(2, capturedCtx.Recipients.Count);
        Assert.Contains("ops-team@company.com", capturedCtx.Recipients);
        Assert.Contains("data-lead@company.com", capturedCtx.Recipients);

        // Audit record persisted with correct error count and recipients
        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n =>
                n.TotalErrors == 10 &&
                n.Status == "SENT" &&
                n.Recipients == "ops-team@company.com;data-lead@company.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 2c: Single error ? notification IS still triggered
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithSingleError_StillSendsNotification()
    {
        // Arrange
        var emailMock = new Mock<IEmailService>();
        emailMock
            .Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (repoMock, uowMock) = BuildUowMocks();
        var config = BuildConfig("ops@test.com");

        var handler = new SendValidationFailureNotificationHandler(
            emailMock.Object, uowMock.Object, config,
            NullLogger<SendValidationFailureNotificationHandler>.Instance);

        var cmd = BuildCommand(errorCount: 1);

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert — even 1 blocking error triggers the notification
        Assert.True(result.Success);
        emailMock.Verify(e => e.SendValidationFailureAsync(
            It.Is<ValidationFailureEmailContext>(ctx => ctx.AllErrors.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.TotalErrors == 1 && n.Status == "SENT"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 2d: Multiple recipients each receive the full error list
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_MultipleRecipients_AllReceiveFullErrorList()
    {
        // Arrange
        var emailMock = new Mock<IEmailService>();
        ValidationFailureEmailContext? capturedCtx = null;

        emailMock
            .Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => capturedCtx = ctx)
            .Returns(Task.CompletedTask);

        var (repoMock, uowMock) = BuildUowMocks();
        var config = BuildConfig("alice@company.com", "bob@company.com", "charlie@company.com");

        var handler = new SendValidationFailureNotificationHandler(
            emailMock.Object, uowMock.Object, config,
            NullLogger<SendValidationFailureNotificationHandler>.Instance);

        var cmd = BuildCommand(errorCount: 10);

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert — all 3 recipients in context, all 10 errors included
        Assert.NotNull(capturedCtx);
        Assert.Equal(3, capturedCtx!.Recipients.Count);
        Assert.Equal(10, capturedCtx.AllErrors.Count);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n =>
                n.Recipients == "alice@company.com;bob@company.com;charlie@company.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 3: Real SMTP via MailHog (skip if MailHog not running)
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task SmtpEmailService_SendsToMailHog_WhenAvailable()
    {
        // Skip if MailHog is not running on localhost:1025
        if (!await IsMailHogAvailableAsync())
        {
            // Use Skip via Assert to avoid test failure when MailHog is not running
            return; // MailHog not available — skipping integration test
        }

        // Arrange
        var settings = Options.Create(new NotificationSettings
        {
            SmtpHost = "localhost",
            SmtpPort = 1025,
            SmtpUseSsl = false,
            SmtpUsername = "",
            SmtpPassword = "",
            SenderEmail = "pafa-test@localhost",
            SenderName = "PAFA Test"
        });

        var smtpService = new SmtpEmailService(settings,
            NullLogger<SmtpEmailService>.Instance);

        var ctx = new ValidationFailureEmailContext(
            IngestionFileId: Guid.NewGuid(),
            FileName: "MOD520A_2025_07.xlsx",
            ReportingPeriod: "2025-07",
            SourceSystem: "SharePoint",
            Recipients: ["test-recipient@localhost"],
            AllErrors: Enumerable.Range(1, 15)
                .Select(i => new ValidationErrorItem(i, "Col" + i, "VAL-005", "ERROR",
                    $"Error row {i}", null))
                .ToList());

        // Act & Assert — should not throw
        await smtpService.SendValidationFailureAsync(ctx);

        // Check http://localhost:8025 to see the email!
    }

    [Fact]
    public async Task SmtpEmailService_IngestionFailure_SendsToMailHog_WhenAvailable()
    {
        if (!await IsMailHogAvailableAsync())
            return;

        var settings = Options.Create(new NotificationSettings
        {
            SmtpHost = "localhost",
            SmtpPort = 1025,
            SmtpUseSsl = false,
            SmtpUsername = "",
            SmtpPassword = "",
            SenderEmail = "pafa-test@localhost",
            SenderName = "PAFA Test"
        });

        var smtpService = new SmtpEmailService(settings,
            NullLogger<SmtpEmailService>.Instance);

        var ctx = new IngestionFailureEmailContext(
            Year: 2025,
            Month: 7,
            TriggerSource: "CRON_AUTO",
            ErrorMessage: "SharePoint connection failed: [Network] Connection timed out after 3 retries",
            RetryAttempts: 3,
            FailedAtUtc: DateTime.UtcNow,
            Recipients: ["test-recipient@localhost"]);

        // Act & Assert
        await smtpService.SendIngestionFailureAsync(ctx);

        // Check http://localhost:8025 to see the email!
    }

    // ?? MailHog availability check ???????????????????????????????????????

    private static async Task<bool> IsMailHogAvailableAsync()
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync("localhost", 1025);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
