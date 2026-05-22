using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PAFA.Domain.Entities;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Models;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Notifications;
using PAFA.Extraction.Handlers.Notifications;

namespace PAFA.Tests.Notifications;

/// <summary>
/// Integration-style tests for the validation failure notification flow.
/// Transport is mocked — these tests verify the handler ? IEmailService contract
/// (which routes to Azure Service Bus in production) and audit record persistence.
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

    private static (Mock<IValidationNotificationRepository> repoMock, Mock<IUnitOfWork> uowMock) BuildUowMocks()
    {
        var repoMock = new Mock<IValidationNotificationRepository>();
        repoMock.Setup(r => r.AddAsync(It.IsAny<ValidationNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.SetupGet(u => u.ValidationNotifications).Returns(repoMock.Object);
        uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (repoMock, uowMock);
    }

    /// <summary>Builds config with ServiceBus:ValidationFailureRecipients.</summary>
    private static IConfiguration BuildConfig(params string[] recipients)
    {
        var dict = new Dictionary<string, string?>();
        for (int i = 0; i < recipients.Length; i++)
            dict[$"ServiceBus:ValidationFailureRecipients:{i}"] = recipients[i];
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static SendValidationFailureNotificationHandler BuildHandler(
        IEmailService emailService,
        IUnitOfWork uow,
        IConfiguration config)
        => new(emailService, uow, config, NullLogger<SendValidationFailureNotificationHandler>.Instance);

    // ?????????????????????????????????????????????????????????????????????
    //  Test 1: Full pipeline — publishes to Service Bus and persists audit
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithMockedServiceBus_SendsAndPersistsAudit()
    {
        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendValidationFailureAsync(
            It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (repoMock, uowMock) = BuildUowMocks();
        var handler = BuildHandler(emailMock.Object, uowMock.Object, BuildConfig("ops@test.com", "lead@test.com"));

        var result = await handler.Handle(BuildCommand(errorCount: 12), CancellationToken.None);

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
    //  Test 2: All errors passed to the Service Bus message (not capped)
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithBlockingErrors_AllErrorsPassedToServiceBus()
    {
        var emailMock = new Mock<IEmailService>();
        ValidationFailureEmailContext? capturedCtx = null;
        emailMock.Setup(e => e.SendValidationFailureAsync(
            It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => capturedCtx = ctx)
            .Returns(Task.CompletedTask);

        var (_, uowMock) = BuildUowMocks();
        var handler = BuildHandler(emailMock.Object, uowMock.Object, BuildConfig("ops@test.com"));

        await handler.Handle(BuildCommand(errorCount: 25), CancellationToken.None);

        Assert.NotNull(capturedCtx);
        Assert.Equal(25, capturedCtx!.AllErrors.Count);
        Assert.Equal("MOD520A_2025_07.xlsx", capturedCtx.FileName);
        Assert.Equal("2025-07", capturedCtx.ReportingPeriod);
        Assert.Contains("ops@test.com", capturedCtx.Recipients);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 3: Exactly 10 errors — all 10 included in the message
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithExactly10Errors_SendsNotificationWithAll10Errors()
    {
        var emailMock = new Mock<IEmailService>();
        ValidationFailureEmailContext? capturedCtx = null;
        emailMock.Setup(e => e.SendValidationFailureAsync(
            It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => capturedCtx = ctx)
            .Returns(Task.CompletedTask);

        var (repoMock, uowMock) = BuildUowMocks();
        var handler = BuildHandler(emailMock.Object, uowMock.Object,
            BuildConfig("ops-team@company.com", "data-lead@company.com"));

        var result = await handler.Handle(BuildCommand(errorCount: 10), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(10, capturedCtx!.AllErrors.Count);
        Assert.Equal(2, capturedCtx.Recipients.Count);
        Assert.Contains("ops-team@company.com", capturedCtx.Recipients);
        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n =>
                n.TotalErrors == 10 && n.Status == "SENT" &&
                n.Recipients == "ops-team@company.com;data-lead@company.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 4: Single error — notification IS still triggered
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WithSingleError_StillSendsNotification()
    {
        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendValidationFailureAsync(
            It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (repoMock, uowMock) = BuildUowMocks();
        var handler = BuildHandler(emailMock.Object, uowMock.Object, BuildConfig("ops@test.com"));

        var result = await handler.Handle(BuildCommand(errorCount: 1), CancellationToken.None);

        Assert.True(result.Success);
        emailMock.Verify(e => e.SendValidationFailureAsync(
            It.Is<ValidationFailureEmailContext>(ctx => ctx.AllErrors.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.TotalErrors == 1 && n.Status == "SENT"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 5: Multiple recipients — all carried inside the message
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_MultipleRecipients_AllIncludedInMessage()
    {
        var emailMock = new Mock<IEmailService>();
        ValidationFailureEmailContext? capturedCtx = null;
        emailMock.Setup(e => e.SendValidationFailureAsync(
            It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationFailureEmailContext, CancellationToken>((ctx, _) => capturedCtx = ctx)
            .Returns(Task.CompletedTask);

        var (repoMock, uowMock) = BuildUowMocks();
        var handler = BuildHandler(emailMock.Object, uowMock.Object,
            BuildConfig("alice@company.com", "bob@company.com", "charlie@company.com"));

        await handler.Handle(BuildCommand(errorCount: 10), CancellationToken.None);

        Assert.Equal(3, capturedCtx!.Recipients.Count);
        Assert.Equal(10, capturedCtx.AllErrors.Count);
        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n =>
                n.Recipients == "alice@company.com;bob@company.com;charlie@company.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????
    //  Test 6: Service Bus dispatch fails ? audit record persisted as FAILED
    // ?????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task FullPipeline_WhenServiceBusFails_AuditRecordPersistedAsFailed()
    {
        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendValidationFailureAsync(
            It.IsAny<ValidationFailureEmailContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var (repoMock, uowMock) = BuildUowMocks();
        var handler = BuildHandler(emailMock.Object, uowMock.Object, BuildConfig("ops@test.com"));

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        repoMock.Verify(r => r.AddAsync(
            It.Is<ValidationNotification>(n => n.Status == "FAILED" && n.ErrorDetail != null),
            It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
