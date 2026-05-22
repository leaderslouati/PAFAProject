using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PAFA.Api.BackgroundServices;
using PAFA.Api.Hubs;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Services;
using PAFA.Tests.Pipeline.Mocks;

namespace PAFA.Tests.Pipeline;

/// <summary>
/// Tests du flux complet du pipeline à 3 étapes :
///   Step 1 — SharePointToMinIO  (notification immédiate, fichier déjà dans MinIO)
///   Step 2 — ParseAndValidate   (fusionnés, résultat avec status "Processed" ou "Failed")
///   Step 3 — Persistence        (insertion en base avec le bon statut)
/// </summary>
public class IngestionPipelineWorkerTests
{
    // ?? Helpers de construction ???????????????????????????????????????????????

    private static (
        IngestionPipelineWorker worker,
        IngestionPipelineQueue queue,
        Mock<IMediator> mediatorMock,
        Mock<IHubClients> hubClientsMock,
        List<(string EventName, object Payload)> emittedEvents)
    BuildWorker()
    {
        var emittedEvents = new List<(string, object)>();

        // Mock SignalR Hub
        var clientProxyMock = new Mock<IClientProxy>();
        clientProxyMock
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, _) =>
            {
                emittedEvents.Add((method, args.FirstOrDefault()!));
            })
            .Returns(Task.CompletedTask);

        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);

        var hubContextMock = new Mock<IHubContext<IngestionHub>>();
        hubContextMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);

        // Mock MediatR
        var mediatorMock = new Mock<IMediator>();

        // Mock DI Scope
        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock
            .Setup(s => s.ServiceProvider.GetService(typeof(IMediator)))
            .Returns(mediatorMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(serviceScopeMock.Object);

        // Queue réelle avec un seul message
        var queue = new IngestionPipelineQueue();

        var worker = new IngestionPipelineWorker(
            queue,
            scopeFactoryMock.Object,
            hubContextMock.Object,
            NullLogger<IngestionPipelineWorker>.Instance);

        return (worker, queue, mediatorMock, hubClientsMock, emittedEvents);
    }

    // ?????????????????????????????????????????????????????????????????????????
    //  Test 1 : Fichier valide ? Step1 ? + Step2 ? + Step3 ?
    // ?????????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task Pipeline_ValidFile_EmitsThreeSuccessStepsAndFinished()
    {
        var (worker, queue, mediatorMock, _, emittedEvents) = BuildWorker();
        var fileId = MockData.FileId1;
        var msg    = new PipelineFileMessage(fileId, "MOD520A_2025_07.xlsx", MockData.JobId);

        mediatorMock
            .Setup(m => m.Send(It.Is<ParseFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ParseSuccess(fileId, rows: 250));

        mediatorMock
            .Setup(m => m.Send(It.Is<ValidateFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ValidateSuccess(fileId, validRows: 245, rejectedRows: 5));

        mediatorMock
            .Setup(m => m.Send(It.Is<PersistFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.PersistSuccess(fileId, metrics: 735));

        using var cts = new CancellationTokenSource();
        _ = worker.StartAsync(cts.Token);

        await queue.EnqueueAsync(msg);
        await Task.Delay(300);
        await cts.CancelAsync();

        // Step 1 — SharePointToMinIO
        Assert.Contains(emittedEvents, e => e.EventName == "StepCompleted"
            && e.Payload is StepCompletedPayload { Step: 1, StepName: "SharePointToMinIO", Status: "Success" });

        // Step 2 — ParseAndValidate
        Assert.Contains(emittedEvents, e => e.EventName == "StepCompleted"
            && e.Payload is StepCompletedPayload { Step: 2, StepName: "ParseAndValidate", Status: "Success" });

        // Step 3 — Persistence
        Assert.Contains(emittedEvents, e => e.EventName == "StepCompleted"
            && e.Payload is StepCompletedPayload { Step: 3, StepName: "Persistence", Status: "Success" });

        // PipelineFinished avec Succeeded=1
        Assert.Contains(emittedEvents, e => e.EventName == "PipelineFinished"
            && e.Payload is PipelineFinishedPayload { Succeeded: 1, Failed: 0 });
    }

    // ?????????????????????????????????????????????????????????????????????????
    //  Test 2 : Parse échoue ? Step2 Failed, Step3 jamais exécuté
    // ?????????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task Pipeline_ParseFails_EmitsStep2FailedAndStops()
    {
        var (worker, queue, mediatorMock, _, emittedEvents) = BuildWorker();
        var fileId = MockData.FileId3;
        var msg    = new PipelineFileMessage(fileId, "MOD700_2025_07.xlsx", MockData.JobId);

        mediatorMock
            .Setup(m => m.Send(It.Is<ParseFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ParseFailure(fileId));

        using var cts = new CancellationTokenSource();
        await queue.EnqueueAsync(msg);

        _ = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();

        // Step 2 doit être Failed avec fileStatus = "Failed"
        Assert.Contains(emittedEvents, e => e.EventName == "StepCompleted"
            && e.Payload is StepCompletedPayload { Step: 2, Status: "Failed" });

        // Step 3 ne doit PAS être émis
        Assert.DoesNotContain(emittedEvents, e => e.EventName == "StepCompleted"
            && e.Payload is StepCompletedPayload { Step: 3 });

        // PipelineFinished avec Failed=1
        Assert.Contains(emittedEvents, e => e.EventName == "PipelineFinished"
            && e.Payload is PipelineFinishedPayload { Succeeded: 0, Failed: 1 });

        // Validate jamais appelé
        mediatorMock.Verify(
            m => m.Send(It.IsAny<ValidateFileCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ?????????????????????????????????????????????????????????????????????????
    //  Test 3 : Validation bloquante ? fileStatus = "Failed", Persist quand même
    // ?????????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task Pipeline_BlockingValidation_Step2ReturnsFailedStatus_PersistStillCalled()
    {
        var (worker, queue, mediatorMock, _, emittedEvents) = BuildWorker();
        var fileId = MockData.FileId3;
        var msg    = new PipelineFileMessage(fileId, "MOD700_2025_07.xlsx", MockData.JobId);

        mediatorMock
            .Setup(m => m.Send(It.Is<ParseFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ParseSuccess(fileId, rows: 80));

        // Validation retourne Success=true mais HasBlockingErrors=true (comportement réel)
        mediatorMock
            .Setup(m => m.Send(It.Is<ValidateFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ValidateBlocking(fileId));

        mediatorMock
            .Setup(m => m.Send(It.Is<PersistFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.PersistFailure(fileId, "Validation échouée — erreurs bloquantes."));

        using var cts = new CancellationTokenSource();
        await queue.EnqueueAsync(msg);

        _ = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();

        // Step 2 émis avec fileStatus = "Failed" dans les details
        var step2 = emittedEvents
            .Where(e => e.EventName == "StepCompleted")
            .Select(e => e.Payload as StepCompletedPayload)
            .FirstOrDefault(p => p?.Step == 2);

        Assert.NotNull(step2);
        Assert.NotNull(step2!.Details);
        Assert.Equal("Failed", step2.Details["fileStatus"]?.ToString());
        Assert.True((bool)step2.Details["hasBlockingErrors"]!);

        // Persist est bien appelé (le PersistFileHandler gère lui-même le cas bloquant)
        mediatorMock.Verify(
            m => m.Send(It.IsAny<PersistFileCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ?????????????????????????????????????????????????????????????????????????
    //  Test 4 : Validation avec avertissements ? fileStatus = "Processed"
    // ?????????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task Pipeline_ValidationWithWarnings_Step2FileStatusIsProcessed()
    {
        var (worker, queue, mediatorMock, _, emittedEvents) = BuildWorker();
        var fileId = MockData.FileId2;
        var msg    = new PipelineFileMessage(fileId, "RPT_1364_2025_07.xlsx", MockData.JobId);

        mediatorMock
            .Setup(m => m.Send(It.Is<ParseFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ParseSuccess(fileId, rows: 195));

        mediatorMock
            .Setup(m => m.Send(It.Is<ValidateFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ValidateWithWarnings(fileId));

        mediatorMock
            .Setup(m => m.Send(It.Is<PersistFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.PersistSuccess(fileId, metrics: 570));

        using var cts = new CancellationTokenSource();
        await queue.EnqueueAsync(msg);

        _ = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();

        var step2 = emittedEvents
            .Where(e => e.EventName == "StepCompleted")
            .Select(e => e.Payload as StepCompletedPayload)
            .FirstOrDefault(p => p?.Step == 2);

        Assert.NotNull(step2);
        Assert.Equal("Success", step2!.Status);
        Assert.Equal("Processed", step2.Details!["fileStatus"]?.ToString());
        Assert.False((bool)step2.Details["hasBlockingErrors"]!);
        Assert.Equal(5, (int)step2.Details["rowsRejected"]!);
    }

    // ?????????????????????????????????????????????????????????????????????????
    //  Test 5 : Résultat Step2 contient bien le FileProcessingResultRow
    // ?????????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task Pipeline_Step2Details_ContainsFileProcessingResultRow()
    {
        var (worker, queue, mediatorMock, _, emittedEvents) = BuildWorker();
        var fileId = MockData.FileId1;
        var msg    = new PipelineFileMessage(fileId, "MOD520A_2025_07.xlsx", MockData.JobId);

        mediatorMock
            .Setup(m => m.Send(It.Is<ParseFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ParseSuccess(fileId, rows: 300));

        mediatorMock
            .Setup(m => m.Send(It.Is<ValidateFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ValidateSuccess(fileId, validRows: 295, rejectedRows: 5));

        mediatorMock
            .Setup(m => m.Send(It.Is<PersistFileCommand>(c => c.FileId == fileId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.PersistSuccess(fileId));

        using var cts = new CancellationTokenSource();
        await queue.EnqueueAsync(msg);

        _ = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();

        var step2 = emittedEvents
            .Where(e => e.EventName == "StepCompleted")
            .Select(e => e.Payload as StepCompletedPayload)
            .FirstOrDefault(p => p?.Step == 2);

        Assert.NotNull(step2);
        Assert.NotNull(step2!.Details);

        // Le champ "result" contient un FileProcessingResultRow
        var row = step2.Details["result"] as PAFA.Api.Hubs.FileProcessingResultRow;
        Assert.NotNull(row);
        Assert.Equal("MOD520A_2025_07.xlsx", row!.FileName);
        Assert.Equal("Processed", row.FileStatus);
        Assert.Equal(300, row.RowsRead);
        Assert.Equal(295, row.RowsValid);
        Assert.Equal(5, row.RowsRejected);
        Assert.False(row.HasBlockingErrors);
    }

    // ?????????????????????????????????????????????????????????????????????????
    //  Test 6 : Step 1 toujours Success avec blobReady=true
    // ?????????????????????????????????????????????????????????????????????????

    [Fact]
    public async Task Pipeline_Step1_AlwaysSuccessWithBlobReady()
    {
        var (worker, queue, mediatorMock, _, emittedEvents) = BuildWorker();
        var fileId = MockData.FileId1;
        var msg    = new PipelineFileMessage(fileId, "MOD520A_2025_07.xlsx", MockData.JobId);

        mediatorMock
            .Setup(m => m.Send(It.IsAny<ParseFileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ParseSuccess(fileId));
        mediatorMock
            .Setup(m => m.Send(It.IsAny<ValidateFileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.ValidateSuccess(fileId));
        mediatorMock
            .Setup(m => m.Send(It.IsAny<PersistFileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockData.PersistSuccess(fileId));

        using var cts = new CancellationTokenSource();
        await queue.EnqueueAsync(msg);

        _ = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();

        var step1 = emittedEvents
            .Where(e => e.EventName == "StepCompleted")
            .Select(e => e.Payload as StepCompletedPayload)
            .FirstOrDefault(p => p?.Step == 1);

        Assert.NotNull(step1);
        Assert.Equal("SharePointToMinIO", step1!.StepName);
        Assert.Equal("Success", step1.Status);
        Assert.True((bool)step1.Details!["blobReady"]!);
        Assert.Equal("MOD520A_2025_07.xlsx", step1.Details["fileName"]?.ToString());
    }

}
