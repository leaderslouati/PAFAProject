using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Commands.Sftp;
using PAFA.Infrastructure.Parsing;
using PAFA.Infrastructure.Persistence;
using PAFA.Infrastructure.Repositories;
using PAFA.Infrastructure.Repository;
using PAFA.Infrastructure.Sftp;
using PAFA.Infrastructure.Storage;
using PAFA.Reports.Batch.Configuration;
using PAFA.Reports.Batch.Core;

namespace PAFA.BatchReports;

/// <summary>
/// Single entry point for the entire PAFA data pipeline.
/// Runs as a one-shot process triggered by Kubernetes CronJob (prod)
/// or 'docker compose run' / 'dotnet run' (local dev).
///
/// Modes:
///   --once    (default) : Full pipeline — SFTP → MinIO → Parse → Validate → Insert → Reports
///   --ingest            : SFTP → MinIO → Parse → Validate → Insert DB only
///   --reports           : Generate PDF/Excel from existing DB data only
///   --year N --month N  : Override the target period (default: previous month)
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var (year, month) = ResolvePeriod(args);
            Console.WriteLine($"[PAFA Batch] Target period: {year}-{month:D2}");

            var host = CreateHostBuilder(args, year, month).Build();

            using var scope = host.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("PAFA Batch Starting — {Year}-{Month:D2}", year, month);

            var db = scope.ServiceProvider.GetRequiredService<PafaDbContext>();
            if (!await db.Database.CanConnectAsync())
            {
                logger.LogCritical("Cannot connect to PostgreSQL.");
                return 2;
            }

            var mode = ResolveMode(args);
            logger.LogInformation("Mode: {Mode}", mode);

            return mode switch
            {
                BatchMode.Ingest  => await RunIngestionAsync(scope, logger, year, month),
                BatchMode.Reports => await RunReportsAsync(scope, logger),
                _                 => await RunFullPipelineAsync(scope, logger, year, month)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PAFA Batch] FATAL: {ex.Message}");
            return 2;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  MODES
    // ════════════════════════════════════════════════════════════════

    static async Task<int> RunFullPipelineAsync(
        IServiceScope scope, ILogger logger, int year, int month)
    {
        var ingestCode = await RunIngestionAsync(scope, logger, year, month);
        if (ingestCode != 0)
            logger.LogWarning("Ingestion had failures — generating reports from available data.");

        return await RunReportsAsync(scope, logger);
    }

    static async Task<int> RunIngestionAsync(
        IServiceScope scope, ILogger logger, int year, int month)
    {
        logger.LogInformation("═══ Phase 1: SFTP → MinIO → Parse → Validate → Insert DB ═══");
        try
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new DownloadParrFilesCommand(year, month));

            logger.LogInformation(
                "Ingestion complete — {Downloaded} downloaded, {Imported} imported, {Failed} failed",
                result.FilesDownloaded, result.FilesImported, result.FilesFailed);

            return result.FilesFailed > 0 && result.FilesImported == 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion phase failed");
            return 1;
        }
    }

    static async Task<int> RunReportsAsync(IServiceScope scope, ILogger logger)
    {
        logger.LogInformation("═══ Phase 2: Report Generation (PDF/Excel) ═══");
        try
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<BatchReportOrchestrator>();
            var success = await orchestrator.ExecuteAllAsync();
            logger.LogInformation(success
                ? "Reports generated successfully."
                : "Report generation had failures.");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Report generation failed");
            return 1;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  PERIOD RESOLUTION
    // ════════════════════════════════════════════════════════════════

    static (int year, int month) ResolvePeriod(string[] args)
    {
        var yearArg  = GetArg(args, "--year");
        var monthArg = GetArg(args, "--month");

        if (int.TryParse(yearArg, out int y) &&
            int.TryParse(monthArg, out int m) && m is >= 1 and <= 12)
            return (y, m);

        var envYear  = Environment.GetEnvironmentVariable("PAFA_TargetYear");
        var envMonth = Environment.GetEnvironmentVariable("PAFA_TargetMonth");

        if (int.TryParse(envYear, out int ey) &&
            int.TryParse(envMonth, out int em) && em is >= 1 and <= 12)
            return (ey, em);

        var prev = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);
        return (prev.Year, prev.Month);
    }

    static BatchMode ResolveMode(string[] args)
    {
        if (args.Contains("--ingest"))  return BatchMode.Ingest;
        if (args.Contains("--reports")) return BatchMode.Reports;
        return BatchMode.Full;
    }

    static string? GetArg(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    // ════════════════════════════════════════════════════════════════
    //  HOST BUILDER — all services for the linear pipeline
    // ════════════════════════════════════════════════════════════════

    static IHostBuilder CreateHostBuilder(string[] args, int year, int month) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false)
                    .AddJsonFile(
                        $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                        optional: true)
                    .AddEnvironmentVariables(prefix: "PAFA_");
            })
            .ConfigureServices((context, services) =>
            {
                // ── Batch settings ──────────────────────────────────
                var batchSettings = context.Configuration
                    .GetSection(BatchReportSettings.SectionName)
                    .Get<BatchReportSettings>() ?? new BatchReportSettings();
                batchSettings.TargetYear  = year;
                batchSettings.TargetMonth = month;
                services.AddSingleton(batchSettings);

                // ── Database ────────────────────────────────────────
                services.AddDbContext<PafaDbContext>(options =>
                    options.UseNpgsql(
                        context.Configuration.GetConnectionString("DefaultConnection"),
                        npgsql => npgsql.EnableRetryOnFailure(batchSettings.MaxRetryAttempts)));

                // ── Repositories ────────────────────────────────────
                services.AddScoped<IUnitOfWork,              UnitOfWork>();
                services.AddScoped<IIngestionJobRepository,  IngestionJobRepository>();
                services.AddScoped<IIngestionFileRepository, IngestionFileRepository>();
                services.AddScoped<IShipperRepository,       ShipperRepository>();
                services.AddScoped<IReportRepository,        ReportRepository>();
                services.AddScoped<IMetricValueRepository,   MetricValueRepository>();

                // ── SFTP ────────────────────────────────────────────
                services.Configure<SftpSettings>(
                    context.Configuration.GetSection(SftpSettings.SectionName));
                services.AddScoped<ISftpFileSource, SftpFileDownloader>();

                // ── Blob Storage (MinIO / Local) ────────────────────
                services.Configure<BlobStorageSettings>(
                    context.Configuration.GetSection(BlobStorageSettings.SectionName));
                var blobProvider = context.Configuration["BlobStorage:Provider"] ?? "Local";
                if (blobProvider.Equals("MinIO", StringComparison.OrdinalIgnoreCase))
                    services.AddSingleton<IBlobStorageService, MinioBlobStorageService>();
                else
                    services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();

                // ── File Parsing ────────────────────────────────────
                services.AddScoped<IFileParser, ExcelFileParser>();
                services.AddScoped<IFileParser, CsvFileParser>();
                services.AddScoped<FileParserFactory>();

                // ── MediatR (CQRS handlers) ─────────────────────────
                services.AddMediatR(cfg =>
                    cfg.RegisterServicesFromAssembly(
                        typeof(UploadParrFilesCommand).Assembly));

                // ── Report Generators ───────────────────────────────
                services.AddScoped<ReportGenerator, PdfReportGenerator>();
                services.AddScoped<ReportGenerator, ExcelReportGenerator>();
                services.AddScoped<BatchReportOrchestrator>();

                // ── Logging ─────────────────────────────────────────
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });
            });
}

enum BatchMode { Full, Ingest, Reports }