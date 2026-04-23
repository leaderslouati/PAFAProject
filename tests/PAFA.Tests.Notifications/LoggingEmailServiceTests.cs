using Microsoft.Extensions.Logging;
using Moq;
using PAFA.Domain.Models;
using PAFA.Infrastructure.Services;

namespace PAFA.Tests.Notifications;

/// <summary>
/// Unit tests for <see cref="LoggingEmailService"/>.
/// No SMTP, no database — 100% fast, offline.
///
/// What is covered:
///   ? SendValidationFailureAsync completes without throwing
///   ? First 10 errors are logged
///   ? Works when error list has exactly 10 items
///   ? Works when error list has more than 10 items
///   ? Works when recipient list is empty (should not throw)
///   ? SendWelcomeEmailAsync logs and completes
/// </summary>
public class LoggingEmailServiceTests
{
    // ?? Shared factory helpers ????????????????????????????????????????????

    private static LoggingEmailService BuildService(out List<string> logLines)
    {
        var captured = new List<string>();
        var loggerMock = new Mock<ILogger<LoggingEmailService>>();

        loggerMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => true),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((lvl, _, state, _, fmt) =>
                captured.Add(state.ToString() ?? ""));

        logLines = captured;
        return new LoggingEmailService(loggerMock.Object);
    }

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
                ErrorMessage:  $"ShipperShortCode manquant — ligne {i}",
                OriginalValue: null))
            .ToList();

        return new ValidationFailureEmailContext(
            IngestionFileId: Guid.NewGuid(),
            FileName:        "MOD520A_2025_02.xlsx",
            ReportingPeriod: "2025-02",
            SourceSystem:    "SharePoint",
            Recipients:      recipients ?? ["ops@company.com"],
            AllErrors:        errors);
    }

    // ?? Tests ?????????????????????????????????????????????????????????????

    [Fact]
    public async Task SendValidationFailureAsync_CompletesWithoutException()
    {
        var svc = BuildService(out _);
        var ctx = BuildContext(errorCount: 3);

        // Should not throw
        await svc.SendValidationFailureAsync(ctx);
    }

    [Fact]
    public async Task SendValidationFailureAsync_LogsFileName()
    {
        var svc = BuildService(out var logs);
        var ctx = BuildContext(errorCount: 3);

        await svc.SendValidationFailureAsync(ctx);

        Assert.Contains(logs, l => l.Contains("MOD520A_2025_02.xlsx"));
    }

    [Fact]
    public async Task SendValidationFailureAsync_LogsReportingPeriod()
    {
        var svc = BuildService(out var logs);
        var ctx = BuildContext(errorCount: 3);

        await svc.SendValidationFailureAsync(ctx);

        Assert.Contains(logs, l => l.Contains("2025-02"));
    }

    [Fact]
    public async Task SendValidationFailureAsync_LogsTotalErrorCount()
    {
        var svc = BuildService(out var logs);
        var ctx = BuildContext(errorCount: 7);

        await svc.SendValidationFailureAsync(ctx);

        Assert.Contains(logs, l => l.Contains("7"));
    }

    [Fact]
    public async Task SendValidationFailureAsync_WithExactly10Errors_LogsAllTen()
    {
        var svc = BuildService(out var logs);
        var ctx = BuildContext(errorCount: 10);

        await svc.SendValidationFailureAsync(ctx);

        // One log line per error in the preview (max 10)
        var errorLines = logs.Where(l => l.Contains("VAL-005")).ToList();
        Assert.Equal(10, errorLines.Count);
    }

    [Fact]
    public async Task SendValidationFailureAsync_WithMoreThan10Errors_LogsOnlyFirst10()
    {
        var svc = BuildService(out var logs);
        var ctx = BuildContext(errorCount: 25);

        await svc.SendValidationFailureAsync(ctx);

        // Summary line contains the total count (25), not capped
        Assert.Contains(logs, l => l.Contains("25"));

        // Per-error preview lines are capped at 10
        var errorLines = logs.Where(l => l.Contains("VAL-005")).ToList();
        Assert.Equal(10, errorLines.Count);
    }

    [Fact]
    public async Task SendValidationFailureAsync_WithNoRecipients_DoesNotThrow()
    {
        var svc = BuildService(out _);
        var ctx = BuildContext(errorCount: 3, recipients: []);

        // Must not throw even with empty recipient list
        await svc.SendValidationFailureAsync(ctx);
    }

    [Fact]
    public async Task SendValidationFailureAsync_WithMultipleRecipients_LogsAll()
    {
        var svc = BuildService(out var logs);
        var recipients = new[] { "alice@company.com", "bob@company.com", "ops-dl@company.com" };
        var ctx = BuildContext(errorCount: 2, recipients: recipients);

        await svc.SendValidationFailureAsync(ctx);

        Assert.Contains(logs, l => l.Contains("alice@company.com"));
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_CompletesWithoutException()
    {
        var svc = BuildService(out _);

        await svc.SendWelcomeEmailAsync("user@company.com", "Alice", "TempPass#123");
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_LogsRecipientEmail()
    {
        var svc = BuildService(out var logs);

        await svc.SendWelcomeEmailAsync("user@company.com", "Alice", "TempPass#123");

        Assert.Contains(logs, l => l.Contains("user@company.com"));
    }
}
