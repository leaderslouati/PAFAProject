using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Infrastructure.Data;
using PAFA.Infrastructure.Repositories;
using PAFA.Reports.Queries.PowerBi;


var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════
//  API CONFIGURATION
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PAFA Import API",
        Version = "v1.0",
        Description = "API for PARR file ingestion and processing pipeline",
        Contact = new OpenApiContact
        {
            Name = "PAFA Team",
            Email = "pafa-support@company.com"
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
//  DATABASE CONFIGURATION
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddDbContext<PafaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// ═══════════════════════════════════════════════════════════════════════
//  REPOSITORY PATTERN & UNIT OF WORK
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IIngestionJobRepository, IngestionJobRepository>();
builder.Services.AddScoped<IIngestionFileRepository, IngestedFileRepository>();
builder.Services.AddScoped<IShipperRepository, ShipperRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IMetricValueRepository, MetricValueRepository>();

// ═══════════════════════════════════════════════════════════════════════
//  MEDIATR (CQRS PATTERN)
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddMediatR(cfg =>
{
    // Scanne les commandes (Écritures)
    cfg.RegisterServicesFromAssembly(typeof(UploadParrFilesCommand).Assembly);

    // Scanne les requêtes (Lectures pour Power BI)
    cfg.RegisterServicesFromAssembly(typeof(GetMetricsQuery).Assembly);
});

// ═══════════════════════════════════════════════════════════════════════
//  RABBITMQ & MASSTRANSIT (ASYNC MESSAGING)
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// ═══════════════════════════════════════════════════════════════════════
//  CORS (FOR FRONTEND DEVELOPMENT)
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:3000" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

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
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "PAFA Import API Documentation";
        c.EnableTryItOutByDefault();
    });
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// ═══════════════════════════════════════════════════════════════════════
//  STARTUP LOGGING
// ═══════════════════════════════════════════════════════════════════════

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("PAFA Import API started successfully");
logger.LogInformation("Environment: {Env}", app.Environment.EnvironmentName);

app.Run();