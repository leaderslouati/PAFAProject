# 🚀 API REST pour Reports Power BI — Guide d'Implémentation

## 📋 Endpoints Requis

### Endpoint 1: Export Report to PPTX

**Route:** `POST /api/reports/export`

```csharp
// File: src/PAFA.Api/Controllers/ReportsController.cs

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPowerBiExportService _exportService;
    private readonly IBlobStorageService _blobStorage;
    
    /// <summary>
    /// Export Power BI report to PPTX format
    /// </summary>
    /// <param name="request">Report ID, dataset ID, filters</param>
    /// <returns>Download URL with SAS token</returns>
    [HttpPost("export")]
    [Authorize(Roles = "ReportViewer,ReportAdmin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExportReportResponse>> ExportReportAsync(
        [FromBody] ExportReportRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        try
        {
            var command = new ExportReportCommand(
                ReportId: request.ReportId,
                DatasetId: request.DatasetId,
                Format: request.Format ?? "pptx",
                ReportingPeriod: request.ReportingPeriod,
                Filters: request.Filters ?? new Dictionary<string, List<string>>()
            );
            
            var result = await _mediator.Send(command, ct);
            
            return Accepted(new ExportReportResponse
            {
                ExportJobId = result.JobId,
                Status = "queued",
                StatusUrl = Url.Action("GetExportStatus", new { jobId = result.JobId }),
                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(5)
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

**Request Body:**
```json
{
  "reportId": "7e5c6a5a-8b2d-4e3f-9a1b-2c5d8e9f0a1b",
  "datasetId": "9f1c6d7e-3a4b-5c2d-1e8f-9a2b3c4d5e6f",
  "reportingPeriod": "2025-04-30",
  "format": "pptx",
  "filters": {
    "ProductClass": ["PC1", "PC2"],
    "ShipperCode": ["SSE", "BGT"]
  }
}
```

**Response (202 Accepted):**
```json
{
  "exportJobId": "job-uuid-1234",
  "status": "queued",
  "statusUrl": "/api/reports/export/job-uuid-1234/status",
  "estimatedCompletionTime": "2025-04-11T15:45:00Z"
}
```

---

### Endpoint 2: Get Export Status

**Route:** `GET /api/reports/export/{jobId}/status`

```csharp
[HttpGet("export/{jobId}/status")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<ExportStatusResponse>> GetExportStatusAsync(
    string jobId,
    CancellationToken ct)
{
    var query = new GetExportStatusQuery(jobId);
    var result = await _mediator.Send(query, ct);
    
    if (result == null)
        return NotFound(new { error = "Export job not found" });
    
    return Ok(new ExportStatusResponse
    {
        JobId = result.JobId,
        Status = result.Status, // "pending" | "inProgress" | "completed" | "failed"
        DownloadUrl = result.Status == "completed" ? result.DownloadUrl : null,
        ErrorMessage = result.Status == "failed" ? result.ErrorMessage : null,
        ExpiresAt = result.ExpiresAt,
        CreatedAt = result.CreatedAt
    });
}
```

**Response (200 OK):**
```json
{
  "jobId": "job-uuid-1234",
  "status": "completed",
  "downloadUrl": "https://pafareports.blob.core.windows.net/reports/2025-04/PARR_2025_04_Schedule_2A.pptx?sv=2021-06-08&...",
  "expiresAt": "2025-04-18T15:45:00Z",
  "createdAt": "2025-04-11T15:00:00Z"
}
```

---

### Endpoint 3: Download Report

**Route:** `GET /api/reports/{reportId}/download`

```csharp
[HttpGet("{reportId}/download")]
[AllowAnonymous]
public async Task<FileResult> DownloadReportAsync(
    string reportId,
    [FromQuery] string? version = "latest",
    CancellationToken ct = default)
{
    try
    {
        var query = new GetReportQuery(reportId, version);
        var report = await _mediator.Send(query, ct);
        
        var stream = await _blobStorage.DownloadStreamAsync(report.BlobPath, ct);
        
        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            $"{report.Title}_{report.ReportingPeriod:yyyy-MM-dd}.pptx"
        );
    }
    catch (FileNotFoundException ex)
    {
        return NotFound();
    }
}
```

---

### Endpoint 4: List Reports

**Route:** `GET /api/reports?period=2025-04&type=2A,2B`

```csharp
[HttpGet]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<ActionResult<ListReportsResponse>> ListReportsAsync(
    [FromQuery] string? period = null,
    [FromQuery] string? type = null,
    [FromQuery] int pageSize = 50,
    [FromQuery] int pageNumber = 1,
    CancellationToken ct = default)
{
    var query = new ListReportsQuery(
        Period: period,
        Type: type?.Split(',').ToList(),
        PageSize: pageSize,
        PageNumber: pageNumber
    );
    
    var result = await _mediator.Send(query, ct);
    
    return Ok(new ListReportsResponse
    {
        Reports = result.Reports.Select(r => new ReportSummary
        {
            Id = r.Id,
            Title = r.Title,
            Period = r.ReportingPeriod.ToString("yyyy-MM"),
            Audience = r.Audience,
            Status = r.Status,
            GeneratedAt = r.GeneratedAt,
            DownloadUrl = r.FilePath_PPTX,
            ExpiresAt = r.FilePath_PPTX != null 
                ? DateTime.UtcNow.AddDays(7) 
                : (DateTime?)null
        }).ToList(),
        TotalCount = result.TotalCount,
        PageNumber = pageNumber,
        PageSize = pageSize
    });
}
```

**Response (200 OK):**
```json
{
  "reports": [
    {
      "id": "uuid-1",
      "title": "Schedule 2A - Industry",
      "period": "2025-04",
      "audience": "Industry",
      "status": "published",
      "generatedAt": "2025-04-01T03:00:00Z",
      "downloadUrl": "https://pafareports.blob.core.windows.net/reports/...",
      "expiresAt": "2025-05-01T03:00:00Z"
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 50
}
```

---

### Endpoint 5: Get Power BI Embed Token

**Route:** `POST /api/embed/token`

```csharp
[HttpPost("embed/token")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<ActionResult<EmbedTokenResponse>> GetEmbedTokenAsync(
    [FromBody] EmbedTokenRequest request,
    CancellationToken ct)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    try
    {
        var query = new GetEmbedTokenQuery(
            ReportId: request.ReportId,
            DatasetId: request.DatasetId,
            ExpiryMinutes: request.ExpiryMinutes ?? 60,
            RlsAudiences: request.Audiences ?? new List<string>()
        );
        
        var result = await _mediator.Send(query, ct);
        
        return Ok(new EmbedTokenResponse
        {
            Token = result.Token,
            ExpiresAt = result.ExpiresAt,
            EmbedUrl = $"https://app.powerbi.com/reportEmbed?reportId={request.ReportId}",
            ReportId = request.ReportId
        });
    }
    catch (UnauthorizedAccessException)
    {
        return Unauthorized();
    }
}
```

---

### Endpoint 6: Refresh Dataset

**Route:** `POST /api/dataset/{datasetId}/refresh`

```csharp
[HttpPost("{datasetId}/refresh")]
[Authorize(Roles = "ReportAdmin")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
public async Task<ActionResult<RefreshResponseDto>> RefreshDatasetAsync(
    string datasetId,
    [FromBody] RefreshRequest request,
    CancellationToken ct)
{
    try
    {
        var command = new RefreshDatasetCommand(
            DatasetId: datasetId,
            Type: request.Type ?? "full",
            NotifyOnCompletion: request.NotifyOnCompletion ?? false,
            NotificationEmail: request.NotificationEmail
        );
        
        var result = await _mediator.Send(command, ct);
        
        return Accepted(new RefreshResponseDto
        {
            RefreshId = result.RefreshId,
            Status = "queued",
            DatasetId = datasetId,
            PollUrl = Url.Action("GetRefreshStatus", new { datasetId, refreshId = result.RefreshId }),
            EstimatedDuration = 300  // 5 minutes
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}
```

---

### Endpoint 7: Get Refresh Status

**Route:** `GET /api/dataset/{datasetId}/refresh/{refreshId}/status`

```csharp
[HttpGet("{datasetId}/refresh/{refreshId}/status")]
[Authorize(Roles = "ReportAdmin")]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<ActionResult<RefreshStatusDto>> GetRefreshStatusAsync(
    string datasetId,
    string refreshId,
    CancellationToken ct)
{
    var query = new GetRefreshStatusQuery(datasetId, refreshId);
    var result = await _mediator.Send(query, ct);
    
    if (result == null)
        return NotFound();
    
    return Ok(new RefreshStatusDto
    {
        RefreshId = refreshId,
        DatasetId = datasetId,
        Status = result.Status, // "inProgress" | "completed" | "failed"
        StartTime = result.StartTime,
        EndTime = result.EndTime,
        Duration = result.EndTime.HasValue 
            ? (int)(result.EndTime.Value - result.StartTime).TotalSeconds 
            : 0,
        RowsProcessed = result.RowsProcessed,
        Errors = result.Errors ?? new List<string>()
    });
}
```

---

### Endpoint 8: Get Metrics Data

**Route:** `GET /api/metrics/{period}?class=PC1,PC2&shipper=SSE,BGT`

```csharp
[HttpGet("{period}")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<ActionResult<MetricsDataResponse>> GetMetricsAsync(
    string period,  // "2025-04-30"
    [FromQuery] List<string>? classes = null,
    [FromQuery] List<string>? shippers = null,
    CancellationToken ct = default)
{
    try
    {
        var query = new GetMetricsQuery(
            ReportingPeriod: DateOnly.Parse(period),
            ProductClasses: classes,
            ShipperCodes: shippers
        );
        
        var result = await _mediator.Send(query, ct);
        
        return Ok(new MetricsDataResponse
        {
            Period = period,
            Metrics = result.Metrics.Select(m => new MetricDto
            {
                ShipperCode = m.ShipperCode,
                ShipperName = m.ShipperName,
                ProductClass = m.ProductClass,
                ReadPerformancePct = m.ReadPerformancePct,
                EstimatedReadPct = m.EstimatedReadPct,
                TotalSites = m.TotalSites,
                ComplianceStatus = m.ReadPerformancePct >= 97.5 ? "compliant" : "non-compliant",
                Score = m.ReadPerformancePct
            }).ToList(),
            Summary = new MetricsSummary
            {
                AvgReadPerf = result.Metrics.Average(m => m.ReadPerformancePct),
                CompliantCount = result.Metrics.Count(m => m.ReadPerformancePct >= 97.5),
                NonCompliantCount = result.Metrics.Count(m => m.ReadPerformancePct < 97.5),
                RowCount = result.Metrics.Count
            }
        });
    }
    catch (FormatException)
    {
        return BadRequest(new { error = "Invalid date format. Use YYYY-MM-DD" });
    }
}
```

---

## 📝 Models (DTOs)

```csharp
// Request/Response models for documentation

public record ExportReportRequest(
    string ReportId,
    string DatasetId,
    string ReportingPeriod,
    string? Format = "pptx",
    Dictionary<string, List<string>>? Filters = null
);

public record ExportReportResponse(
    string ExportJobId,
    string Status,
    string StatusUrl,
    DateTime EstimatedCompletionTime
);

public record ExportStatusResponse(
    string JobId,
    string Status,
    string? DownloadUrl,
    string? ErrorMessage,
    DateTime ExpiresAt,
    DateTime CreatedAt
);

public record ListReportsResponse(
    List<ReportSummary> Reports,
    int TotalCount,
    int PageNumber,
    int PageSize
);

public record ReportSummary(
    string Id,
    string Title,
    string Period,
    string Audience,
    string Status,
    DateTime GeneratedAt,
    string? DownloadUrl,
    DateTime? ExpiresAt
);

public record EmbedTokenRequest(
    string ReportId,
    string DatasetId,
    int? ExpiryMinutes = 60,
    List<string>? Audiences = null
);

public record EmbedTokenResponse(
    string Token,
    DateTime ExpiresAt,
    string EmbedUrl,
    string ReportId
);

public record RefreshRequest(
    string? Type = "full",
    bool? NotifyOnCompletion = false,
    string? NotificationEmail = null
);

public record RefreshResponseDto(
    string RefreshId,
    string Status,
    string DatasetId,
    string PollUrl,
    int EstimatedDuration
);

public record RefreshStatusDto(
    string RefreshId,
    string DatasetId,
    string Status,
    DateTime StartTime,
    DateTime? EndTime,
    int Duration,
    int RowsProcessed,
    List<string> Errors
);

public record MetricsDataResponse(
    string Period,
    List<MetricDto> Metrics,
    MetricsSummary Summary
);

public record MetricDto(
    string ShipperCode,
    string ShipperName,
    string ProductClass,
    decimal ReadPerformancePct,
    decimal EstimatedReadPct,
    int TotalSites,
    string ComplianceStatus,
    decimal Score
);

public record MetricsSummary(
    decimal AvgReadPerf,
    int CompliantCount,
    int NonCompliantCount,
    int RowCount
);
```

---

## 🔐 Security Configuration

```csharp
// appsettings.json

{
  "Authentication": {
    "AzureAd": {
      "TenantId": "your-tenant-id",
      "ClientId": "your-client-id",
      "Scopes": ["api://pafa-api/Reports.ReadWrite"]
    }
  },
  "Authorization": {
    "Roles": {
      "ReportViewer": ["view:reports", "download:reports"],
      "ReportAdmin": ["manage:reports", "refresh:dataset", "export:reports"]
    }
  },
  "RateLimit": {
    "Enabled": true,
    "PerMinute": 60,
    "PerHour": 1000
  }
}
```

---

## ✅ Testing Checklist

- [ ] All endpoints return correct status codes
- [ ] Authentication/authorization working
- [ ] Request validation (bad input handling)
- [ ] Response formats match documentation
- [ ] Performance < 2 seconds per endpoint
- [ ] Error messages are user-friendly
- [ ] Swagger documentation auto-generated

---

## 📚 Swagger Configuration

```csharp
// Program.cs

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PAFA Reports API",
        Version = "v1",
        Description = "Power BI reports export, metrics, and dataset management"
    });
    
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter Azure AD token"
    });
});

app.UseSwagger();
app.UseSwaggerUI(options => {
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PAFA Reports API v1");
});
```

Then visit: `https://localhost:5000/swagger`

---

## 🚀 Deployment

### Step 1: Build
```bash
dotnet build PAFAProject.sln
```

### Step 2: Test
```bash
dotnet test PAFAProject.Tests.sln
```

### Step 3: Publish
```bash
dotnet publish -c Release -o ./publish
```

### Step 4: Deploy to Azure App Service
```bash
az webapp deployment source config-zip --resource-group PAFA --name pafa-api --src-path publish.zip
```

