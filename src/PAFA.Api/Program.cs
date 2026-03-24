using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Commands.Users;
using PAFA.Extraction.Helpers;
using PAFA.Extraction.Services;
using PAFA.Extraction.Validations;
using PAFA.Infrastructure.Parsing;
using PAFA.Infrastructure.Persistence;
using PAFA.Infrastructure.Repositories;
using PAFA.Infrastructure.Repository;
using PAFA.Infrastructure.Services;
using PAFA.Infrastructure.Services.PowerBi;
using PAFA.Infrastructure.SharePoint;
using PAFA.Infrastructure.Storage;
using PAFA.Reports.Handlers;
using PAFA.Reports.Writers;
using System.Text;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════
//  API CONFIGURATION
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "PAFA Import API",
        Version     = "v1.0",
        Description = "API for PARR file ingestion, dashboard and export endpoints",
        Contact     = new OpenApiContact { Name = "PAFA Team", Email = "pafa-support@company.com" }
    });

    // Bearer token button in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });

    // Enable XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// ═══════════════════════════════════════════════════════════════════════
//  AUTHENTICATION — JWT Bearer
// ═══════════════════════════════════════════════════════════════════════

var jwtKey = builder.Configuration["Jwt:Key"]
             ?? "PAFA_DEFAULT_DEV_KEY_CHANGE_IN_PRODUCTION_32CH";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"] ?? "pafa-api",
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "pafa-client",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ══════════════════════════════════════════════════════════════════════
//  DATABASE
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddDbContext<PafaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// ═══════════════════════════════════════════════════════════════════════
//  REPOSITORIES
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddScoped<IUnitOfWork,              UnitOfWork>();
builder.Services.AddScoped<IIngestionJobRepository,  IngestionJobRepository>();
builder.Services.AddScoped<IIngestionFileRepository, IngestionFileRepository>();
builder.Services.AddScoped<IShipperRepository,       ShipperRepository>();
builder.Services.AddScoped<IReportRepository,        ReportRepository>();
builder.Services.AddScoped<IMetricValueRepository,   MetricValueRepository>();
builder.Services.AddScoped<IPafaUserRepository,      PafaUserRepository>();

// ═══════════════════════════════════════════════════════════════════════
//  SERVICES
// ═══════════════════════════════════════════════════════════════════════

// Scoped in-memory cache shared across Parse → Validate → Persist handlers
// within the same HTTP request.
builder.Services.AddScoped<PAFA.Extraction.Services.FilePipelineCache>();

// ── Ingestion pipeline queue + background worker ────────────────────────────
// La queue est Singleton : partagée entre le contrôleur HTTP (producteur)
// et le worker background (consommateur).
builder.Services.AddSingleton<IIngestionPipelineQueue, IngestionPipelineQueue>();
builder.Services.AddHostedService<PAFA.Api.BackgroundServices.IngestionPipelineWorker>();

// POC: logs the email. Swap for SmtpEmailService / SendGridEmailService in prod.
builder.Services.AddScoped<IEmailService, LoggingEmailService>();
builder.Services.AddScoped<ISharePointFileHelper, SharePointFileHelper>();
// ═══════════════════════════════════════════════════════════════════════
//  POWER BI EMBEDDED — Service Principal (App Owns Data)
// ═══════════════════════════════════════════════════════════════════════

var pbiSettings = builder.Configuration
    .GetSection(PowerBiSettings.SectionName)
    .Get<PowerBiSettings>() ?? new PowerBiSettings();

builder.Services.AddSingleton(pbiSettings);
builder.Services.AddSingleton<PowerBiClientFactory>();
builder.Services.AddScoped<IPowerBiExportService, PowerBiExportService>();

// ── Power BI Batch Export — monthly automated export of 41 reports ──

var pbiBatchSettings = builder.Configuration
    .GetSection(PowerBiBatchExportSettings.SectionName)
    .Get<PowerBiBatchExportSettings>() ?? new PowerBiBatchExportSettings();

builder.Services.AddSingleton(pbiBatchSettings);
builder.Services.AddScoped<PowerBiDatasetRefreshService>();
builder.Services.AddScoped<IPowerBiBatchExportService, PowerBiBatchExportService>();

// ═══════════════════════════════════════════════════════════════════════
//  SHAREPOINT — Source de fichiers PARR (Microsoft Graph)
// ═══════════════════════════════════════════════════════════════════════

builder.Services.Configure<SharePointSettings>(
    builder.Configuration.GetSection(SharePointSettings.SectionName));
builder.Services.AddScoped<IRemoteFileSource, SharePointFileSource>();
builder.Services.AddScoped<IFileSourceSettings>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SharePointSettings>>().Value);


// ═══════════════════════════════════════════════════════════════════════
//  BLOB STORAGE
// ═══════════════════════════════════════════════════════════════════════

builder.Services.Configure<BlobStorageSettings>(
    builder.Configuration.GetSection(BlobStorageSettings.SectionName));

var blobProvider = builder.Configuration["BlobStorage:Provider"] ?? "Local";
if (blobProvider.Equals("MinIO", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IBlobStorageService, MinioBlobStorageService>();
else
    builder.Services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();

// ═══════════════════════════════════════════════════════════════════════
//  FILE PARSING
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddScoped<IFileParser, ExcelFileParser>();
builder.Services.AddScoped<IFileParser, CsvFileParser>();
builder.Services.AddScoped<IFileParser, XmlFileParser>();
builder.Services.AddScoped<FileParserFactory>();

// ═══════════════════════════════════════════════════════════════════════
//  REPORT WRITERS
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddScoped<IReportWriter, CsvReportWriter>();
builder.Services.AddScoped<IReportWriter, ExcelReportWriter>();
builder.Services.AddScoped<IReportWriter, PdfReportWriter>();

// ═══════════════════════════════════════════════════════════════════════
//  FLUENTVALIDATION
// ═══════════════════════════════════════════════════════════════════════

// Registers all IValidator<T> implementations from the Extraction assembly.
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserCommandValidator>();

// ═══════════════════════════════════════════════════════════════════════
//  MEDIATR (CQRS)
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(UploadParrFilesCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(ExportPowerBiCsvQueryHandler).Assembly);
    // Scan the Extraction assembly for CreateUserCommandHandler
    cfg.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
});

// ═══════════════════════════════════════════════════════════════════════
//  SIGNALR
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddSignalR();

// ═══════════════════════════════════════════════════════════════════════
//  CORS
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000"])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ═══════════════════════════════════════════════════════════════════════
//  INGESTION SCHEDULE
// ═══════════════════════════════════════════════════════════════════════

builder.Services.Configure<IngestionScheduleSettings>(
    builder.Configuration.GetSection(IngestionScheduleSettings.SectionName));
builder.Services.AddSingleton<IIngestionScheduleService, IngestionScheduleService>();

// ═══════════════════════════════════════════════════════════════════════
//  BACKGROUND SERVICES
// ═══════════════════════════════════════════════════════════════════════

// The MonthlyReportExportWorker is registered only when the PowerBiBatchExport
// feature is enabled. In production we expect Kubernetes CronJobs to run the
// batch exports; set PowerBiBatchExport:IsEnabled = true to enable the
// hosted worker (not recommended in prod to avoid duplicate runs).
if (pbiBatchSettings.IsEnabled)
{
    builder.Services.AddHostedService<PAFA.Api.BackgroundServices.MonthlyReportExportWorker>();
}
else
{
    // Hosted service is disabled; external schedulers (K8s CronJobs) should handle exports.
}

// ═══════════════════════════════════════════════════════════════════════
//  BUILD APPLICATION
// ═══════════════════════════════════════════════════════════════════════

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════
//  MIDDLEWARE PIPELINE
// ═══════════════════════════════════════════════════════════════════════

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PAFA Import API v1");
        c.RoutePrefix         = "swagger";
        c.DocumentTitle       = "PAFA Import API Documentation";
        c.EnableTryItOutByDefault();
    });
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHub<PAFA.Api.Hubs.IngestionHub>("/hubs/ingestion");

// ═══════════════════════════════════════════════════════════════════════
//  STARTUP LOGGING
// ═══════════════════════════════════════════════════════════════════════

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("PAFA API started — Blob: {Provider}, FileSource: SharePoint", blobProvider);
logger.LogInformation("Ingestion: via PAFA.BatchReports CronJob or POST /api/ingest");

app.Run();