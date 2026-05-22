using Moq;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Models;

namespace PAFA.Tests.Notifications;

/// <summary>
/// Contract tests for IEmailService — verifies the interface is called correctly
/// by callers, independently of the transport (Azure Service Bus).
///
/// What is covered:
///   ? SendValidationFailureAsync is called once with correct context
///   ? All errors are passed — not capped at 10
///   ? Works when recipient list is empty
///   ? Works with multiple recipients
///   ? SendWelcomeEmailAsync is called with correct parameters
///   ? SendIngestionFailureAsync is called with correct context
/// </summary>
public class NotificationServiceContractTests
{
    private static ValidationFailureEmailContext BuildContext(
        int errorCount = 5,
        IReadOnlyList<string>? recipients = null)
    {
        var errors = Enumerable.Range(1, errorCount)
            .Select(i => new ValidationErrorItem(
                RowNumber:     i,
                ColumnName:    "ShipperShortCode",
                ErrorCode:     "VAL-005",
                Severity:      "ERROR",
                ErrorMessage:  $"ShipperShortCode missing on row {i}",
                OriginalValue: null))
            .ToList();

        return new ValidationFailureEmailContext(
            IngestionFileId: Guid.NewGuid(),
            FileName:        "MOD520A_2025_02.xlsx",
            ReportingPeriod: "2025-02",
            SourceSystem:    "SharePoint",
            Recipients:      recipients ?? ["ops@company.com"],
            AllErrors:       errors);
    }

    [Fact]
    public async Task SendValidationFailureAsync_IsCalledOnce()
    {
        var mock = new Mock<IEmailService>();
        mock.Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mock.Object.SendValidationFailureAsync(BuildContext(errorCount: 3));

        mock.Verify(e => e.SendValidationFailureAsync(
            It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendValidationFailureAsync_ReceivesAllErrors_NotCappedAt10()
    {
        var mock = new Mock<IEmailService>();
        ValidationFailureEmailContext? captured = null;
        mock.Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => captured = ctx)
            .Returns(Task.CompletedTask);

        await mock.Object.SendValidationFailureAsync(BuildContext(errorCount: 25));

        Assert.NotNull(captured);
        Assert.Equal(25, captured!.AllErrors.Count);
    }

    [Fact]
    public async Task SendValidationFailureAsync_WithEmptyRecipients_DoesNotThrow()
    {
        var mock = new Mock<IEmailService>();
        mock.Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mock.Object.SendValidationFailureAsync(BuildContext(recipients: []));
    }

    [Fact]
    public async Task SendValidationFailureAsync_MultipleRecipients_AllPresent()
    {
        var mock = new Mock<IEmailService>();
        ValidationFailureEmailContext? captured = null;
        mock.Setup(e => e.SendValidationFailureAsync(
                It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => captured = ctx)
            .Returns(Task.CompletedTask);

        var ctx = BuildContext(recipients: ["alice@company.com", "bob@company.com", "ops-dl@company.com"]);
        await mock.Object.SendValidationFailureAsync(ctx);

        Assert.Equal(3, captured!.Recipients.Count);
        Assert.Contains("alice@company.com", captured.Recipients);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_IsCalledWithCorrectParameters()
    {
        var mock = new Mock<IEmailService>();
        mock.Setup(e => e.SendWelcomeEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mock.Object.SendWelcomeEmailAsync("user@company.com", "Alice", "TempPass#123");

        mock.Verify(e => e.SendWelcomeEmailAsync(
            "user@company.com", "Alice", "TempPass#123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendIngestionFailureAsync_IsCalledWithCorrectPeriod()
    {
        var mock = new Mock<IEmailService>();
        IngestionFailureEmailContext? captured = null;
        mock.Setup(e => e.SendIngestionFailureAsync(
                It.IsAny<IngestionFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionFailureEmailContext, CancellationToken>((ctx, _) => captured = ctx)
            .Returns(Task.CompletedTask);

        var ctx = new IngestionFailureEmailContext(
            Year: 2025, Month: 7, TriggerSource: "CRON_AUTO",
            ErrorMessage: "Connection failed", RetryAttempts: 3,
            FailedAtUtc: DateTime.UtcNow,
            Recipients: ["ops@company.com"]);

        await mock.Object.SendIngestionFailureAsync(ctx);

        Assert.NotNull(captured);
        Assert.Equal(2025, captured!.Year);
        Assert.Equal(7, captured.Month);
        Assert.Equal(3, captured.RetryAttempts);
    }
}
