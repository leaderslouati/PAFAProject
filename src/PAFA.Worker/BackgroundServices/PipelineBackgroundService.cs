using System.Threading.Channels;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using PAFA.Domain.Enums;
using PAFA.Extraction.Commands.Pipeline;
using PAFA.Worker.Hubs;
using PAFA.Worker.Models;
using PAFA.Worker.State;

namespace PAFA.Worker.BackgroundServices;

/// <summary>
/// Consumes <see cref="PipelineJob"/> messages from an in-process
/// <see cref="Channel{T}"/> and orchestrates the three pipeline steps sequentially:
///
///   Step 1 — <see cref="ImportFilesCommand"/>           (Import from SharePoint)
///   Step 2 — <see cref="ParseAndValidateFilesCommand"/> (ClosedXML + 6 rules)
///   Step 3 — <see cref="PersistFilesCommand"/>          (DB + Service Bus + Power BI)
///
/// After each step the in-memory state store is updated and a SignalR "StepUpdated"
/// event is pushed to all clients subscribed to the job group.
///
/// If a step fails all remaining steps are marked Skipped.
/// Service Bus notifications (for quarantined files) are always sent — even on Error.
/// </summary>
public sealed class PipelineBackgroundService
    : BackgroundService, IPipelineBackgroundService
{
    private readonly Channel<PipelineJob> _channel;
    private readonly IPipelineStateStore _stateStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PipelineHub> _hub;
    private readonly ILogger<PipelineBackgroundService> _log;

    public PipelineBackgroundService(
        IPipelineStateStore stateStore,
        IServiceScopeFactory scopeFactory,
        IHubContext<PipelineHub> hub,
        ILogger<PipelineBackgroundService> log)
    {
        _stateStore   = stateStore;
        _scopeFactory = scopeFactory;
        _hub          = hub;
        _log          = log;

        _channel = Channel.CreateBounded<PipelineJob>(new BoundedChannelOptions(20)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc/>
    public ValueTask EnqueueAsync(PipelineJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    // ── BackgroundService ─────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("[PIPELINE_WORKER] Started — waiting for pipeline jobs.");

        while (!stoppingToken.IsCancellationRequested)
        {
            PipelineJob job;
            try
            {
                job = await _channel.Reader.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "[PIPELINE_WORKER] Unhandled exception for JobId {JobId}", job.JobId);
            }
        }

        _log.LogInformation("[PIPELINE_WORKER] Stopped.");
    }

    // ── Job processing ────────────────────────────────────────────────────────

    private async Task ProcessJobAsync(PipelineJob job, CancellationToken ct)
    {
        var correlationId = Guid.NewGuid();

        var state = new PipelineExecutionState
        {
            JobId         = job.JobId,
            CorrelationId = correlationId,
            Year          = job.Year,
            Month         = job.Month,
            StartedAt     = DateTime.UtcNow,
            OverallStatus = PipelineStatus.Running,
            Steps         =
            [
                new() { StepNumber = 1, Name = "Import des fichiers",  Status = StepStatus.Pending },
                new() { StepNumber = 2, Name = "Parsing + Validation", Status = StepStatus.Pending },
                new() { StepNumber = 3, Name = "Persistance",          Status = StepStatus.Pending }
            ]
        };

        _stateStore.Set(job.JobId, state);

        _log.LogInformation(
            "[PIPELINE_WORKER] JobId={JobId} CorrelationId={CorrelationId} Year={Year} Month={Month}",
            job.JobId, correlationId, job.Year, job.Month);

        using var scope = _scopeFactory.CreateScope();
        var mediator    = scope.ServiceProvider.GetRequiredService<IMediator>();

        // ── Step 1: Import ────────────────────────────────────────────────
        var step1 = state.Steps[0];
        await RunStepAsync(step1, state, ct, async () =>
        {
            var t      = DateTime.UtcNow;
            var result = await mediator.Send(
                new ImportFilesCommand(job.Year, job.Month, correlationId), ct);

            step1.DurationMs = Elapsed(t);
            step1.Result     = result.Files;

            if (!result.Success)
            {
                step1.Error = result.ErrorMessage;
                return false;
            }

            return true;
        });

        if (step1.Status != StepStatus.Success)
        {
            await SkipRemainingAsync(state, fromStep: 2, ct);
            return;
        }

        // Only pass files that were actually imported
        var importedFiles = ((IReadOnlyList<ImportedFile>)step1.Result!)
            .Where(f => f.Status == ImportStatus.Imported)
            .ToList();

        if (importedFiles.Count == 0)
        {
            _log.LogInformation(
                "[PIPELINE_WORKER] No files imported — skipping steps 2 and 3. CorrelationId={CorrelationId}",
                correlationId);
            await SkipRemainingAsync(state, fromStep: 2, ct);
            return;
        }

        // ── Step 2: Parse + Validate ──────────────────────────────────────
        var step2 = state.Steps[1];
        await RunStepAsync(step2, state, ct, async () =>
        {
            var t      = DateTime.UtcNow;
            var result = await mediator.Send(
                new ParseAndValidateFilesCommand(importedFiles, job.Year, job.Month, correlationId), ct);

            step2.DurationMs = Elapsed(t);
            step2.Result     = result.Files;

            if (!result.Success)
            {
                step2.Error = result.ErrorMessage;
                return false;
            }

            return true;
        });

        if (step2.Status != StepStatus.Success)
        {
            await SkipRemainingAsync(state, fromStep: 3, ct);
            return;
        }

        var parseResults = (IReadOnlyList<ParseAndValidateResult>)step2.Result!;

        // ── Step 3: Persist ───────────────────────────────────────────────
        var step3 = state.Steps[2];
        await RunStepAsync(step3, state, ct, async () =>
        {
            var t      = DateTime.UtcNow;
            var result = await mediator.Send(
                new PersistFilesCommand(parseResults, job.Year, job.Month, correlationId), ct);

            step3.DurationMs = Elapsed(t);
            step3.Result     = result.Report;

            if (!result.Success)
            {
                step3.Error = result.ErrorMessage;
                return false;
            }

            return true;
        });

        // ── Finalise ──────────────────────────────────────────────────────
        state.OverallStatus = state.Steps.All(s =>
            s.Status is StepStatus.Success or StepStatus.Skipped)
            ? PipelineStatus.Success
            : PipelineStatus.Error;

        state.FinishedAt = DateTime.UtcNow;
        _stateStore.Set(job.JobId, state);

        _log.LogInformation(
            "[PIPELINE_WORKER] JobId={JobId} finished with status {Status} — CorrelationId={CorrelationId}",
            job.JobId, state.OverallStatus, correlationId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RunStepAsync(
        PipelineStepState step,
        PipelineExecutionState state,
        CancellationToken ct,
        Func<Task<bool>> action)
    {
        step.Status = StepStatus.Running;
        _stateStore.Set(state.JobId, state);
        await PushSignalRAsync(state, step, ct);

        try
        {
            var success = await action();
            step.Status = success ? StepStatus.Success : StepStatus.Error;
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Error;
            step.Error  = ex.Message;
            _log.LogError(ex,
                "Pipeline step {StepNumber} ({StepName}) failed — JobId={JobId}",
                step.StepNumber, step.Name, state.JobId);
        }

        _stateStore.Set(state.JobId, state);
        await PushSignalRAsync(state, step, ct);
    }

    private async Task SkipRemainingAsync(
        PipelineExecutionState state, int fromStep, CancellationToken ct)
    {
        foreach (var step in state.Steps.Where(s => s.StepNumber >= fromStep))
            step.Status = StepStatus.Skipped;

        state.OverallStatus = PipelineStatus.Error;
        state.FinishedAt    = DateTime.UtcNow;
        _stateStore.Set(state.JobId, state);

        await _hub.Clients.Group(state.JobId.ToString())
            .SendAsync("StepUpdated", BuildSignalRPayload(state,
                state.Steps.Last(s => s.StepNumber >= fromStep)), ct);
    }

    private Task PushSignalRAsync(
        PipelineExecutionState state,
        PipelineStepState step,
        CancellationToken ct)
        => _hub.Clients.Group(state.JobId.ToString())
            .SendAsync("StepUpdated", BuildSignalRPayload(state, step), ct);

    private static object BuildSignalRPayload(
        PipelineExecutionState state, PipelineStepState step)
        => new
        {
            jobId         = state.JobId,
            correlationId = state.CorrelationId,
            stepNumber    = step.StepNumber,
            stepName      = step.Name,
            status        = step.Status.ToString(),
            durationMs    = step.DurationMs,
            stepResult    = step.Result
        };

    private static long Elapsed(DateTime start)
        => (long)(DateTime.UtcNow - start).TotalMilliseconds;
}
