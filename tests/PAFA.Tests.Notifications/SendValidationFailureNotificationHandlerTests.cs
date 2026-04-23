using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Models;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Notifications;
using PAFA.Extraction.Handlers.Notifications;

namespace PAFA.Tests.Notifications;

/// <summary>
/// Unit tests for <see cref="SendValidationFailureNotificationHandler"/>.
///
/// What is covered:
///   ? Returns Success=true when email sends correctly
///   ? Audit record is persisted with Status=SENT
///   ? Returns Success=false when email service throws
///   ? Audit record is still persisted with Status=FAILED even when email throws
///   ? ErrorDetail is populated on failure
///   ? No recipients configured — still persists audit record
///   ? All 25 errors are included in the email context (not capped)
///   ? SaveChangesAsync is always called (in finally block)
/// </summary>
public class SendValidationFailureNotificationHandlerTests
{
    // ?? Shared factory helpers ????????????????????????????????????????????

    private static (
        SendValidationFailureNotificationHandler handler,
        Mock<IEmailService> emailMock,
        Mock<IValidationNotificationRepository> repoMock,
        Mock<IUnitOfWork> uowMock)
        BuildHandler(
            string[]? recipients = null,
            bool emailThrows = false)
    {
        var emailMock = new Mock<IEmailService>();

        if (emailThrows)
            emailMock
                .Setup(e => e.SendValidationFailureAsync(
                    It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SmtpException("SMTP connection refused"));
        else
            emailMock
                .Setup(e => e.SendValidationFailureAsync(
                    It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var repoMock = new Mock<IValidationNotificationRepository>();
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<ValidationNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.SetupGet(u => u.ValidationNotifications).Returns(repoMock.Object);
        uowMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(BuildConfigValues(recipients ?? ["ops@test.com"]))
            .Build();

        var handler = new SendValidationFailureNotificationHandler(
            emailMock.Object,
            uowMock.Object,
            config,
            NullLogger<SendValidationFailureNotificationHandler>.Instance);

        return (handler, emailMock, repoMock, uowMock);
    }

    private static Dictionary<string, string?> BuildConfigValues(string[] recipients)
    {
        var dict = new Dictionary<string, string?>();
        for (int i = 0; i < recipients.Length; i++)
            dict[$"Notifications:ValidationFailureRecipients:{i}"] = recipients[i];
        return dict;
    }

    private static SendValidationFailureNotificationCommand BuildCommand(int errorCount = 5)
    {
        var errors = Enumerable.Range(1, errorCount)
            .Select(i => new ValidationErrorItem(
                RowNumber:     i,
                ColumnName:    i % 2 == 0 ? "ShipperShortCode" : "ReportingPeriod",
                ErrorCode:     i % 2 == 0 ? "VAL-005" : "VAL-003",
                Severity:      "ERROR",
                ErrorMessage:  $"Validation error on row {i}",
                OriginalValue: null))
            .ToList();

        return new SendValidationFailureNotificationCommand(
            IngestionFileId: Guid.NewGuid(),
            FileName:        "MOD520A_2025_02.xlsx",
            ReportingPeriod: "2025-02",
            SourceSystem:    "SharePoint",
            AllErrors:       errors);
    }

    // ?? Tests ?????????????????????????????????????????????????????????????

    [Fact]
    public async Task Handle_WhenEmailSucceeds_ReturnsSuccess()
    {
        var (handler, _, _, _) = BuildHandler();
        var cmd = BuildCommand();

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenEmailSucceeds_PersistsAuditRecordWithStatusSent()
    {
        var (handler, _, repoMock, _) = BuildHandler();
        var cmd = BuildCommand();

        await handler.Handle(cmd, CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.Status == "SENT"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailSucceeds_AuditRecordContainsCorrectFileName()
    {
        var (handler, _, repoMock, _) = BuildHandler();
        var cmd = BuildCommand();

        await handler.Handle(cmd, CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.FileName == "MOD520A_2025_02.xlsx"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailSucceeds_AuditRecordContainsTotalErrorCount()
    {
        var (handler, _, repoMock, _) = BuildHandler();
        var cmd = BuildCommand(errorCount: 12);

        await handler.Handle(cmd, CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.TotalErrors == 12),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailFails_ReturnsFailure()
    {
        var (handler, _, _, _) = BuildHandler(emailThrows: true);
        var cmd = BuildCommand();

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenEmailFails_StillPersistsAuditRecordWithStatusFailed()
    {
        var (handler, _, repoMock, _) = BuildHandler(emailThrows: true);
        var cmd = BuildCommand();

        await handler.Handle(cmd, CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.Status == "FAILED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailFails_ErrorDetailIsPopulated()
    {
        var (handler, _, repoMock, _) = BuildHandler(emailThrows: true);
        var cmd = BuildCommand();

        await handler.Handle(cmd, CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.ErrorDetail != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SaveChangesIsAlwaysCalled_WhenEmailSucceeds()
    {
        var (handler, _, _, uowMock) = BuildHandler();

        await handler.Handle(BuildCommand(), CancellationToken.None);

        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SaveChangesIsAlwaysCalled_WhenEmailFails()
    {
        var (handler, _, _, uowMock) = BuildHandler(emailThrows: true);

        await handler.Handle(BuildCommand(), CancellationToken.None);

        // Even on email failure, SaveChanges must be called (finally block)
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesAllErrorsToEmailContext_NotCapped()
    {
        var (handler, emailMock, _, _) = BuildHandler();
        var cmd = BuildCommand(errorCount: 25);

        await handler.Handle(cmd, CancellationToken.None);

        // Email service must receive ALL 25 errors (capping is presentation-layer only)
        emailMock.Verify(e => e.SendValidationFailureAsync(
            It.Is<ValidationFailureEmailContext>(ctx => ctx.AllErrors.Count == 25),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoRecipients_StillPersistsAuditRecord()
    {
        var (handler, _, repoMock, _) = BuildHandler(recipients: []);
        var cmd = BuildCommand();

        await handler.Handle(cmd, CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(
            It.IsAny<ValidationNotification>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RecipientsArePersisted_Semicolonseparated()
    {
        var (handler, _, repoMock, _) = BuildHandler(
            recipients: ["alice@test.com", "bob@test.com"]);

        await handler.Handle(BuildCommand(), CancellationToken.None);

        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n =>
                n.Recipients == "alice@test.com;bob@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>Fake exception to avoid a real SMTP dependency in unit tests.</summary>
internal sealed class SmtpException(string message) : Exception(message);
