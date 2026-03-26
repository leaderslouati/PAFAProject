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
using PAFA.Extraction.Commands.SharePoint;
using PAFA.Infrastructure.Parsing;
using PAFA.Infrastructure.Persistence;
using PAFA.Infrastructure.Repositories;
using PAFA.Infrastructure.Repository;
using PAFA.Infrastructure.SharePoint;
using PAFA.Infrastructure.Storage;
using PAFA.Reports.Batch.Configuration;
using PAFA.Reports.Batch.Core;

namespace PAFA.BatchReports;

/// <summary>
/// Single entry point for the PAFA data pipeline.
//
// Modes:
//   --ingest                     : Traite tous les fichiers dans SharePoint /{year}/{month:D2}/
//   --ingest --year N --month N  : Force la période (ex: --year 2025 --month 7)
//   --reports                    : Génère les rapports PDF/Excel depuis la DB
//   (aucun argument)             : Pipeline complet — ingest + reports
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var (year, month) = ResolvePeriod(args);

            if (year.HasValue && month.HasValue)
                Console.WriteLine($"[PAFA Batch] Période forcée : {year}-{month:D2}");
            else
                Console.WriteLine($"[PAFA Batch] Période : mois courant UTC ({DateTime.UtcNow:yyyy-MM})");

            var host = CreateHostBuilder(args, year, month).Build();

            using var scope = host.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("PAFA Batch Starting");

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
        IServiceScope scope, ILogger logger, int? year, int? month)
    {
        var ingestCode = await RunIngestionAsync(scope, logger, year, month);
        if (ingestCode != 0)
            logger.LogWarning("Ingestion had failures — generating reports from available data.");

        return await RunReportsAsync(scope, logger);
    }

    static async Task<int> RunIngestionAsync(
        IServiceScope scope, ILogger logger, int? year, int? month)
    {
        var now = DateTime.UtcNow;
        var targetYear  = year  ?? now.Year;
        var targetMonth = month ?? now.Month;

        logger.LogInformation(
            "═══ SharePoint Ingestion — dossier source : /{Year}/{Month:D2}/ ═══",
            targetYear, targetMonth);
        try
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new DownloadParrFilesCommand(year, month));

            logger.LogInformation(
                "Ingestion terminée — {Downloaded} téléchargés, {Imported} importés, {Failed} en erreur",
                result.FilesDownloaded, result.FilesImported, result.FilesFailed);

            return result.FilesFailed > 0 && result.FilesImported == 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion échouée");
            return 1;
        }
    }

    static async Task<int> RunReportsAsync(IServiceScope scope, ILogger logger)
    {
        logger.LogInformation("═══ Report Generation (PDF/Excel) ═══");
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
    //  PERIOD RESOLUTION — now returns nullable
    // ════════════════════════════════════════════════════════════════

    static (int? year, int? month) ResolvePeriod(string[] args)
    {
        // 1. CLI args: --year 2025 --month 2
        var yearArg  = GetArg(args, "--year");
        var monthArg = GetArg(args, "--month");

        if (int.TryParse(yearArg, out int y) &&
            int.TryParse(monthArg, out int m) && m is >= 1 and <= 12)
            return (y, m);

        // 2. Env vars
        var envYear  = Environment.GetEnvironmentVariable("PAFA_TargetYear");
        var envMonth = Environment.GetEnvironmentVariable("PAFA_TargetMonth");

        if (int.TryParse(envYear, out int ey) &&
            int.TryParse(envMonth, out int em) && em is >= 1 and <= 12)
            return (ey, em);

        // 3. No period specified → use current UTC month (folder-based detection)
        return (null, null);
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
    //  HOST BUILDER
    // ════════════════════════════════════════════════════════════════

    static IHostBuilder CreateHostBuilder(string[] args, int? year, int? month) =>
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
                var batchSettings = context.Configuration
                    .GetSection(BatchReportSettings.SectionName)
                    .Get<BatchReportSettings>() ?? new BatchReportSettings();
                if (year.HasValue) batchSettings.TargetYear = year.Value;
                if (month.HasValue) batchSettings.TargetMonth = month.Value;
                services.AddSingleton(batchSettings);

                services.AddDbContext<PafaDbContext>(options =>
                    options.UseNpgsql(
                        context.Configuration.GetConnectionString("DefaultConnection"),
                        npgsql => npgsql.EnableRetryOnFailure(batchSettings.MaxRetryAttempts)));

                services.AddScoped<IUnitOfWork,              UnitOfWork>();
                services.AddScoped<IIngestionJobRepository,  IngestionJobRepository>();
                services.AddScoped<IIngestionFileRepository, IngestionFileRepository>();
                services.AddScoped<IShipperRepository,       ShipperRepository>();
                services.AddScoped<IReportRepository,        ReportRepository>();
                services.AddScoped<IMetricValueRepository,   MetricValueRepository>();

                // ── SharePoint — Source de fichiers PARR ────────────────
                services.Configure<SharePointSettings>(
                    context.Configuration.GetSection(SharePointSettings.SectionName));
                services.AddScoped<IRemoteFileSource, SharePointFileSource>();
                services.AddScoped<IFileSourceSettings>(sp =>
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SharePointSettings>>().Value);

                services.Configure<BlobStorageSettings>(
                    context.Configuration.GetSection(BlobStorageSettings.SectionName));
                var blobProvider = context.Configuration["BlobStorage:Provider"] ?? "Local";
                if (blobProvider.Equals("MinIO", StringComparison.OrdinalIgnoreCase))
                    services.AddSingleton<IBlobStorageService, MinioBlobStorageService>();
                else
                    services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();

                // ── File Parsing ────────────────────────────────────────
                services.AddScoped<IFileParser, ExcelFileParser>();
                services.AddScoped<IFileParser, CsvFileParser>();
                services.AddScoped<IFileParser, XmlFileParser>();
                services.AddScoped<FileParserFactory>();

                services.AddMediatR(cfg =>
                    cfg.RegisterServicesFromAssembly(
                        typeof(UploadParrFilesCommand).Assembly));

                services.AddScoped<ReportGenerator, PdfReportGenerator>();
                services.AddScoped<ReportGenerator, ExcelReportGenerator>();
                services.AddScoped<BatchReportOrchestrator>();

                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });
            });
}

enum BatchMode { Full, Ingest, Reports }