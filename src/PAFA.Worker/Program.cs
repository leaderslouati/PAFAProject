using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Pipeline;
using PAFA.Infrastructure.Parsing;
using PAFA.Infrastructure.Persistence;
using PAFA.Infrastructure.Repositories;
using PAFA.Infrastructure.Repository;
using PAFA.Infrastructure.Services.PowerBi;
using PAFA.Infrastructure.SharePoint;
using PAFA.Infrastructure.Storage;
using PAFA.Messaging.Configuration;
using PAFA.Messaging.Services;
using PAFA.Notifications.Settings;
using PAFA.Worker.BackgroundServices;
using PAFA.Worker.Hubs;
using PAFA.Worker.State;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════
//  API
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "PAFA Worker API",
        Version     = "v1.0",
        Description = "Pipeline orchestration endpoints (run + status) + SignalR hub"
    });
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
});

builder.Services.AddSignalR();

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
            ValidIssuer              = builder.Configuration["Jwt:Issuer"]   ?? "pafa-api",
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "pafa-client",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════════════════
//  DATABASE
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddDbContext<PafaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// ═══════════════════════════════════════════════════════════════════════
//  REPOSITORIES + UNIT OF WORK
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddScoped<IUnitOfWork,              UnitOfWork>();
builder.Services.AddScoped<IIngestionJobRepository,  IngestionJobRepository>();
builder.Services.AddScoped<IIngestionFileRepository, IngestionFileRepository>();
builder.Services.AddScoped<IShipperRepository,       ShipperRepository>();

// ═══════════════════════════════════════════════════════════════════════
//  MEDIATR — scan PAFA.Extraction pipeline handlers
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ImportFilesCommand).Assembly));

// ═══════════════════════════════════════════════════════════════════════
//  EXCEL INSPECTION
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddScoped<ExcelInspectionService>();

// ═══════════════════════════════════════════════════════════════════════
//  NOTIFICATIONS — Azure Service Bus (PAFA.Messaging)
// ═══════════════════════════════════════════════════════════════════════

builder.Services.Configure<NotificationSettings>(
    builder.Configuration.GetSection(NotificationSettings.SectionName));
builder.Services.Configure<ServiceBusSettings>(
    builder.Configuration.GetSection(ServiceBusSettings.SectionName));

builder.Services.AddSingleton<ServiceBusNotificationService>();
builder.Services.AddSingleton<IEmailService>(sp =>
    sp.GetRequiredService<ServiceBusNotificationService>());

// ═══════════════════════════════════════════════════════════════════════
//  POWER BI — dataset refresh after persist step
// ═══════════════════════════════════════════════════════════════════════

var pbiBatchSettings = builder.Configuration
    .GetSection(PowerBiBatchExportSettings.SectionName)
    .Get<PowerBiBatchExportSettings>() ?? new PowerBiBatchExportSettings();

builder.Services.AddSingleton(pbiBatchSettings);
builder.Services.AddScoped<PowerBiDatasetRefreshService>();

// ═══════════════════════════════════════════════════════════════════════
//  SHAREPOINT
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
//  PIPELINE STATE STORE + BACKGROUND SERVICE
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddSingleton<IPipelineStateStore, InMemoryPipelineStateStore>();
builder.Services.AddSingleton<IPipelineBackgroundService, PipelineBackgroundService>();
builder.Services.AddHostedService(sp =>
    (PipelineBackgroundService)sp.GetRequiredService<IPipelineBackgroundService>());

// ═══════════════════════════════════════════════════════════════════════
//  BUILD
// ═══════════════════════════════════════════════════════════════════════

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PAFA Worker API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PipelineHub>("/hubs/pipeline");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("PAFA Worker started — pipeline hub at /hubs/pipeline");

app.Run();
