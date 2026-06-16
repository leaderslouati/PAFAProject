# 📊 Guide Complet : Export des Reports XLSX & PPTX via Power BI

**Objectif:** Générer et exporter les rapports Schedule 2A et 2B au format Excel et PowerPoint, et les stocker sur Azure Blob Storage avec gestion des SAS URLs pour l'accès sécurisé.

---

## 📋 Table des matières

1. [Architecture générale](#architecture)
2. [Prérequis](#prérequis)
3. [Configuration Power BI (Desktop → Service)](#configuration-power-bi)
4. [Implémentation C# — PowerBiExportService](#implémentation-csharp)
5. [Automatisation & Orchestration](#automatisation)
6. [Instructions pas-à-pas pour l'export](#instructions-export)
7. [Dépannage](#dépannage)

---

## <a name="architecture"></a>🏗️ Architecture générale

```
┌─────────────────┐
│  PostgreSQL DB  │  ← metric_values, fact_read_performance, views
└────────┬────────┘
         │
    ┌────▼─────────────────────┐
    │  PAFA.Api                 │
    │  - EmbedController        │  REST endpoints
    │  - ReportController       │
    └────┬──────────────────────┘
         │
    ┌────▼─────────────────────────┐
    │  PowerBiExportService        │  ← Implémentation C#
    │  - ExportToFileAsync()       │
    │  - GetDatasetAsync()         │
    │  - RefreshDatasetAsync()     │
    └────┬──────────────────────────┘
         │
    ┌────▼─────────────────────────┐
    │  Power BI Service            │  (Online)
    │  - Report: 2A (Anonymisé)    │
    │  - Report: 2B (Non-anonymisé)│
    │  - Dataset refresh           │
    │  - Export API                │
    └────┬──────────────────────────┘
         │
    ┌────▼─────────────────────────┐
    │  Azure Blob Storage          │
    │  /exports/                   │
    │  ├── 2A_2026-06_*.pptx       │
    │  ├── 2A_2026-06_*.xlsx       │
    │  ├── 2B_2026-06_*.pptx       │
    │  └── 2B_2026-06_*.xlsx       │
    └──────────────────────────────┘

Reports table (PostgreSQL):
  ├── file_path_pptx = blob URL + SAS token
  ├── file_path_excel = blob URL + SAS token
  ├── published_at = DateTime.UtcNow
  └── status = 'Published'
```

---

## <a name="prérequis"></a>✅ Prérequis

### 1. Power BI Pro License
- Utilisateur avec licence Power BI Pro (vous l'avez mentionné ✓)
- Accès au Power BI Service (app.powerbi.com)

### 2. Service Principal (pour automatisation)
Créer une **Application Azure AD** qui accédera à Power BI en votre nom:

```powershell
# Via Azure AD (ou Portal.azure.com)
# 1. Create app registration
# 2. Create Client Secret (save it securely!)
# 3. Note: Application (client) ID, Tenant ID, Secret

# Variables to store:
# - $tenantId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
# - $clientId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
# - $clientSecret = "**SECRET**"
```

### 3. Power BI Admin Tenant Settings
- Enable "Allow service principals to use Power BI APIs" (Admin Portal > Tenant Settings)
- Give the app access to your workspace (Admin > Workspaces > Assign)

### 4. Azure Storage Account
- Storage Account name and key (or connection string)
- Container name: `exports` (create if not present)

### 5. Required NuGet Packages
```xml
<!-- PAFA.Api.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.PowerBI.Api" Version="1.47.0" />
  <PackageReference Include="Azure.Identity" Version="1.13.0" />
  <PackageReference Include="Azure.Storage.Blobs" Version="12.19.0" />
  <PackageReference Include="Microsoft.Identity.Client" Version="4.60.0" />
</ItemGroup>
```

---

## <a name="configuration-power-bi"></a>🔧 Configuration Power BI (Desktop → Service)

### Step 1: Créer le PBIX en Power BI Desktop

**Fichier:** `src/PAFA.Reports/2A_Industry_Comparison.pbix`  
**Audience:** Industry (Anonymisé)

```
1. Open Power BI Desktop
2. New Project
3. Connect to PostgreSQL database
   - Connection String: Server={host}; Database={db}; User ID={user}; Password={pwd}
   - Query: SELECT * FROM fact_read_performance
   - Import mode (RECOMMENDED for exports) OR DirectQuery
4. Create visuals for Schedule 2A reports:
   - Table: vw_dim_shipper (alias, portfolio_size)
   - Chart: vw_2a1_leaderboard (rank_in_class, read_perf_pct)
   - Histogram: vw_2a1_distribution (perf_bin, shipper_count)
   - Table: vw_2a2_no_meter (no_read_4yr, no_read_4yr_pct)
   - Slicer: vw_dim_date (month_year_text)
5. Apply DAX measures (from docs/powerbi/DAX_MEASURES.md):
   - Count Total
   - Compliance %
   - Read Performance Avg
   - MoM Change %
6. Format slides (19 slides for 2A)
7. Save: 2A_Industry_Comparison.pbix
```

**Fichier:** `src/PAFA.Reports/2B_PAC_Performance.pbix`  
**Audience:** PAC (Non-Anonymisé)

```
1. Create similar structure to 2A
2. Use v_parr_pac instead of v_parr_industry (real shipper names)
3. Implement RLS (Row-Level Security):
   - DAX Role: CREATE ROLE [Shipper_PAC]
   - Mapping: [ShipperName] = USERNAME() or similar (per user/org)
4. 22 slides for 2B
5. Save: 2B_PAC_Performance.pbix
```

### Step 2: Publier à Power BI Service

```
1. Dans Power BI Desktop:
   File > Publish
   Select workspace (e.g. "PAFA-Reports")
   
2. Note the Report IDs:
   - 2A: {report-id-2a}
   - 2B: {report-id-2b}
   
3. Workspace ID: {workspace-id}
   (visible in URL: app.powerbi.com/groups/{workspace-id}/reports)
```

---

## <a name="implémentation-csharp"></a>💻 Implémentation C# — PowerBiExportService

### File: `src/PAFA.Api/Services/PowerBiExportService.cs`

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.Rest;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PAFA.Api.Services;

/// <summary>
/// Service pour exporter les rapports Power BI vers PPTX/PDF/XLSX
/// et les uploader sur Azure Blob Storage.
/// </summary>
public interface IPowerBiExportService
{
    Task<ExportResult> ExportReportAsync(string reportId, ExportFormat format);
    Task<string> UploadToBlobAsync(byte[] fileBytes, string fileName);
    Task<string> GenerateSasUrlAsync(string blobPath, int expirationMinutes = 10080);
    Task RefreshDatasetAsync(string datasetId);
}

public enum ExportFormat
{
    PowerPoint,  // PPTX
    Excel,       // XLSX
    Pdf
}

public record ExportResult(
    bool Success,
    byte[] FileBytes,
    string FileName,
    string? ErrorMessage = null
);

public class PowerBiExportService : IPowerBiExportService
{
    private readonly PowerBiSettings _settings;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly ILogger<PowerBiExportService> _logger;

    public PowerBiExportService(
        IConfiguration configuration,
        BlobContainerClient blobContainerClient,
        ILogger<PowerBiExportService> logger)
    {
        _settings = new PowerBiSettings();
        configuration.GetSection("PowerBi").Bind(_settings);
        _blobContainerClient = blobContainerClient;
        _logger = logger;
    }

    /// <summary>
    /// Exporte un rapport Power BI au format spécifié (PPTX, XLSX, PDF).
    /// </summary>
    public async Task<ExportResult> ExportReportAsync(string reportId, ExportFormat format)
    {
        try
        {
            _logger.LogInformation($"Starting export of report {reportId} to {format}");

            // 1. Get access token via Service Principal
            var token = await GetServicePrincipalTokenAsync();

            // 2. Create Power BI client
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var powerBiClient = new PowerBIClient(
                new Uri("https://api.powerbi.com/"),
                new TokenCredentials(token, "Bearer"));

            // 3. Export report (use ExportToFileInGroupAsync)
            var exportFormat = format switch
            {
                ExportFormat.PowerPoint => "PPTX",
                ExportFormat.Excel => "XLSX",
                ExportFormat.Pdf => "PDF",
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            // Note: SDK may differ — adapt based on Microsoft.PowerBI.Api version
            // Example using HTTP call directly if SDK doesn't expose Export endpoint:
            var exportUrl = 
                $"https://api.powerbi.com/v1.0/myorg/groups/{_settings.WorkspaceId}/reports/{reportId}/ExportTo";

            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, exportUrl)
            {
                Content = new System.Net.Http.StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { format = exportFormat }),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Export failed: {response.StatusCode} - {errorContent}");
                return new ExportResult(false, Array.Empty<byte>(), "", errorContent);
            }

            var fileBytes = await response.Content.ReadAsByteArrayAsync();
            var extension = format switch
            {
                ExportFormat.PowerPoint => "pptx",
                ExportFormat.Excel => "xlsx",
                ExportFormat.Pdf => "pdf",
                _ => "bin"
            };

            var fileName = $"Report_{reportId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{extension}";

            _logger.LogInformation($"Export successful: {fileName} ({fileBytes.Length} bytes)");
            return new ExportResult(true, fileBytes, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed with exception");
            return new ExportResult(false, Array.Empty<byte>(), "", ex.Message);
        }
    }

    /// <summary>
    /// Upload fichier vers Azure Blob Storage.
    /// Retourne le chemin blob (sans SAS token).
    /// </summary>
    public async Task<string> UploadToBlobAsync(byte[] fileBytes, string fileName)
    {
        try
        {
            var blobClient = _blobContainerClient.GetBlobClient(fileName);

            using var ms = new MemoryStream(fileBytes);
            await blobClient.UploadAsync(ms, overwrite: true);

            _logger.LogInformation($"Uploaded to blob: {blobClient.Uri}");
            return blobClient.Name; // Return relative path
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Upload to blob failed: {fileName}");
            throw;
        }
    }

    /// <summary>
    /// Génère une SAS URL pour un blob (accès temporaire sécurisé).
    /// </summary>
    public async Task<string> GenerateSasUrlAsync(string blobPath, int expirationMinutes = 10080)
    {
        try
        {
            var blobClient = _blobContainerClient.GetBlobClient(blobPath);

            // SAS permissions: Read only
            var sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = _blobContainerClient.Name,
                BlobName = blobPath,
                Resource = "b",  // blob
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            // Generate SAS token
            var sasUri = _blobContainerClient.GenerateSasUri(sasBuilder)?.ToString();

            _logger.LogInformation($"Generated SAS URL for {blobPath} (expires in {expirationMinutes} min)");
            return sasUri ?? throw new InvalidOperationException("Failed to generate SAS URI");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"SAS generation failed: {blobPath}");
            throw;
        }
    }

    /// <summary>
    /// Déclenche un refresh du dataset Power BI.
    /// </summary>
    public async Task RefreshDatasetAsync(string datasetId)
    {
        try
        {
            _logger.LogInformation($"Refreshing dataset {datasetId}");

            var token = await GetServicePrincipalTokenAsync();
            using var httpClient = new System.Net.Http.HttpClient();

            var refreshUrl = 
                $"https://api.powerbi.com/v1.0/myorg/groups/{_settings.WorkspaceId}/datasets/{datasetId}/refreshes";

            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, refreshUrl);
            request.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Refresh failed: {response.StatusCode} - {errorContent}");
                throw new InvalidOperationException($"Dataset refresh failed: {errorContent}");
            }

            _logger.LogInformation($"Dataset refresh initiated: {datasetId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dataset refresh failed");
            throw;
        }
    }

    /// <summary>
    /// Obtient un token d'accès via Service Principal (MSAL).
    /// </summary>
    private async Task<string> GetServicePrincipalTokenAsync()
    {
        try
        {
            var app = ConfidentialClientApplicationBuilder.Create(_settings.ClientId)
                .WithClientSecret(_settings.ClientSecret)
                .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
                .Build();

            var scopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

            var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire token for Service Principal");
            throw;
        }
    }
}

/// <summary>
/// Configuration pour Power BI Service Principal.
/// </summary>
public class PowerBiSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string Report2AId { get; set; } = string.Empty;
    public string Report2BId { get; set; } = string.Empty;
    public string Dataset2AId { get; set; } = string.Empty;
    public string Dataset2BId { get; set; } = string.Empty;
}
```

### File: `appsettings.json`

```json
{
  "PowerBi": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientSecret": "USE_AZURE_KEYVAULT_IN_PROD",
    "WorkspaceId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Report2AId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Report2BId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Dataset2AId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Dataset2BId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "ContainerName": "exports"
  }
}
```

### File: `Program.cs` (Dependency Injection)

```csharp
// Add to services
builder.Services.AddScoped<IPowerBiExportService, PowerBiExportService>();

var blobConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
var containerName = builder.Configuration["AzureStorage:ContainerName"];
var blobContainerClient = new BlobContainerClient(
    new Uri($"https://{builder.Configuration["AzureStorage:AccountName"]}.blob.core.windows.net/{containerName}"),
    new DefaultAzureCredential());
builder.Services.AddSingleton(blobContainerClient);
```

---

## <a name="automatisation"></a>🤖 Automatisation & Orchestration

### Option 1: Batch Job via PAFA.BatchReports

**Fichier:** `src/PAFA.BatchReports/Program.cs`

```csharp
using PAFA.Api.Services;
using PAFA.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<IPowerBiExportService, PowerBiExportService>();
builder.Services.AddDbContext<PafaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var host = builder.Build();

// Export all pending reports
using (var scope = host.Services.CreateScope())
{
    var exportService = scope.ServiceProvider.GetRequiredService<IPowerBiExportService>();
    var dbContext = scope.ServiceProvider.GetRequiredService<PafaDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // 1. Refresh datasets
        await exportService.RefreshDatasetAsync("DATASET_2A_ID");
        await Task.Delay(5000); // Wait for refresh
        await exportService.RefreshDatasetAsync("DATASET_2B_ID");
        await Task.Delay(5000);

        // 2. Export reports
        var result2A = await exportService.ExportReportAsync("REPORT_2A_ID", ExportFormat.PowerPoint);
        var result2AXls = await exportService.ExportReportAsync("REPORT_2A_ID", ExportFormat.Excel);
        
        var result2B = await exportService.ExportReportAsync("REPORT_2B_ID", ExportFormat.PowerPoint);
        var result2BXls = await exportService.ExportReportAsync("REPORT_2B_ID", ExportFormat.Excel);

        // 3. Upload to blob & get SAS URLs
        if (result2A.Success)
        {
            var blobPath = await exportService.UploadToBlobAsync(result2A.FileBytes, result2A.FileName);
            var sasUrl = await exportService.GenerateSasUrlAsync(blobPath, expirationMinutes: 43200); // 30 days

            // Save to DB
            var report = new Report
            {
                ReportTypeId = 1, // SCH2A
                ScheduleNumber = 1,
                Title = "2A.1 Estimated and Check Reads",
                ReportingPeriod = DateOnly.FromDateTime(DateTime.Now),
                Audience = ReportAudience.Industry,
                Status = ReportStatus.Published,
                FilePath_PPTX = sasUrl,
                PublishedAt = DateTime.UtcNow
            };
            dbContext.Reports.Add(report);
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Export batch completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Export batch failed");
        throw;
    }
}
```

### Option 2: Kubernetes CronJob

**Fichier:** `src/PAFA.BatchReports/kubernetes-cronjob.yaml`

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: pafa-export-reports
  namespace: pafa
spec:
  schedule: "0 2 18 * *"  # 2 AM UTC on 18th of each month (after ingestion)
  jobTemplate:
    spec:
      template:
        spec:
          serviceAccountName: pafa-batch
          containers:
          - name: batch-export
            image: pafa:latest
            imagePullPolicy: IfNotPresent
            command:
            - /app/PAFA.BatchReports
            env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef:
                  name: pafa-db
                  key: connection-string
            - name: PowerBi__ClientSecret
              valueFrom:
                secretKeyRef:
                  name: pafa-powerbi
                  key: client-secret
            - name: AzureStorage__ConnectionString
              valueFrom:
                secretKeyRef:
                  name: pafa-azure-storage
                  key: connection-string
            resources:
              requests:
                memory: "512Mi"
                cpu: "250m"
              limits:
                memory: "1Gi"
                cpu: "500m"
          restartPolicy: OnFailure
```

---

## <a name="instructions-export"></a>📝 Instructions pas-à-pas pour l'export

### Phase 1: Configuration initiale (ONE-TIME)

**Step 1.1:** Configurer Service Principal

```powershell
# 1. Create app in Azure AD
Connect-AzureAD
$app = New-AzureADApplication -DisplayName "PAFA-PowerBI-Exporter"
$secret = New-AzureADApplicationPasswordCredential -ObjectId $app.ObjectId
$app
$secret

# Save these IDs:
# - App ID (Client ID): $app.AppId
# - Directory ID (Tenant ID): from Azure AD properties
# - Secret: $secret.Value
```

**Step 1.2:** Donner les permissions au Service Principal

```powershell
# 1. In Power BI Admin Portal
# Go to Tenant Settings > API Settings > Allow service principals to use read-only Power BI admin APIs
# Enable the setting

# 2. Assign to workspace
# Workspace > Access > Add principal > Service principal (by name/ID)
# Role: Admin or Contributor

# 3. Grant API permissions in Azure AD
Connect-AzureAD
$servicePrincipal = Get-AzureADServicePrincipal -Filter "AppId eq '$clientId'"
$api = Get-AzureADServicePrincipal -Filter "DisplayName eq 'Power BI Service'"
$permissionId = "4ae37656-d745-4b64-94ab-3928b3ce0201"  # API access permission
New-AzureADServiceAppRoleAssignment -ObjectId $servicePrincipal.ObjectId -PrincipalId $servicePrincipal.ObjectId -ResourceId $api.ObjectId -Id $permissionId
```

**Step 1.3:** Créer Container Azure Blob

```powershell
$storageAccount = Get-AzStorageAccount -Name "pafast" -ResourceGroupName "pafa-rg"
$ctx = $storageAccount.Context
New-AzStorageContainer -Name "exports" -Context $ctx -Permission Off  # Private
```

**Step 1.4:** Publier les PBIX à Power BI Service

```
Dans Power BI Desktop (ou Power BI Web):
1. Create workspace: "PAFA-Reports"
2. Publish 2A_Industry_Comparison.pbix → workspace
3. Publish 2B_PAC_Performance.pbix → workspace
4. Note Report IDs from URL: app.powerbi.com/groups/{ws-id}/reports/{report-id}
5. Update appsettings.json with IDs
```

### Phase 2: Exécution manuelle (DEV/TEST)

**Step 2.1:** Depuis la ligne de commande

```powershell
# Navigate to batch project
cd src/PAFA.BatchReports

# Set environment
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Run
dotnet run

# Expected output:
# [INFO] Refreshing dataset 2A...
# [INFO] Refreshing dataset 2B...
# [INFO] Exporting report 2A to PPTX...
# [INFO] Exporting report 2B to XLSX...
# [INFO] Uploading to blob...
# [INFO] Export batch completed successfully
```

**Step 2.2:** Depuis l'API (REST Call)

```powershell
# Trigger via API endpoint
$headers = @{
    "Authorization" = "Bearer $(Get-AzAccessToken -ResourceTypeName ServiceManagement).Token"
    "Content-Type" = "application/json"
}

$body = @{
    reportIds = @("report-2a-id", "report-2b-id")
    formats = @("PPTX", "XLSX")
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://pafa-api.example.com/api/reports/export" `
    -Method Post `
    -Headers $headers `
    -Body $body
```

### Phase 3: Automatisation mensuelle

**Step 3.1:** Déployer Kubernetes CronJob

```powershell
kubectl apply -f src/PAFA.BatchReports/kubernetes-cronjob.yaml
kubectl get cronjob -n pafa
kubectl logs -n pafa cronjob.batch/pafa-export-reports
```

**Step 3.2:** Vérifier les logs

```powershell
# Check last run
kubectl describe cronjob pafa-export-reports -n pafa

# Check pod logs
kubectl logs -n pafa -l job-name=pafa-export-reports-* --tail=100
```

### Phase 4: Vérification & validation

**Step 4.1:** Vérifier les blobs uploadés

```powershell
# List blobs
$blobs = Get-AzStorageBlob -Container "exports" -Context $ctx
$blobs | Select-Object Name, Length, LastModified

# Example output:
# Name                                    Length        LastModified
# Report_2A_20260614_020000.pptx           45000000     2026-06-14 02:00:00
# Report_2A_20260614_020000.xlsx           8000000      2026-06-14 02:00:10
# Report_2B_20260614_020015.pptx           48000000     2026-06-14 02:00:15
# Report_2B_20260614_020015.xlsx           9000000      2026-06-14 02:00:25
```

**Step 4.2:** Vérifier la base de données

```sql
SELECT 
    id, 
    reporting_period, 
    status, 
    file_path_pptx, 
    file_path_excel, 
    published_at
FROM reports
WHERE reporting_period = DATE_TRUNC('month', NOW())::DATE
ORDER BY published_at DESC;

-- Expected:
-- 4 rows (2A & 2B, each PPTX + XLSX)
-- file_path_* = full blob URL with SAS token
-- published_at = recent timestamp
```

**Step 4.3:** Accéder aux fichiers

```powershell
# Get SAS URL from reports table
$sasUrl = (SELECT file_path_pptx FROM reports WHERE id = '...')

# Download in browser or via PowerShell
Invoke-WebRequest -Uri $sasUrl -OutFile "C:\downloads\Report_2A.pptx"

# Or open in Power BI
# File > Open > From web > paste $sasUrl
```

---

## <a name="dépannage"></a>🔧 Dépannage

### ❌ Erreur: "Access Denied" lors du refresh

**Cause:** Service Principal n'a pas les permissions

**Solution:**
```powershell
# 1. Vérify Service Principal is in workspace
# Admin > Workspaces > pafa-reports > Access > Search by app name

# 2. Check API permissions in Azure AD
# App > API Permissions > Power BI Service > API access (admin consent given?)

# 3. Enable tenant setting
# Admin Portal > Tenant Settings > API Settings > Allow service principals...
```

### ❌ Erreur: "Export format PPTX not available"

**Cause:** SDK version mismatch or API changed

**Solution:**
```csharp
// Check SDK version
Install-Package Microsoft.PowerBI.Api -Version 1.47.0

// Use HTTP endpoint directly instead of SDK method
var exportUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{wsId}/reports/{reportId}/ExportTo";
// POST with { "format": "PPTX" } JSON body
```

### ❌ Erreur: "Blob upload timeout"

**Cause:** Large file or slow network

**Solution:**
```csharp
// Increase timeout & use chunked upload
var blobClient = new BlobClient(uri, new DefaultAzureCredential());
var uploadOptions = new BlobUploadOptions { TransferOptions = new StorageTransferOptions { MaximumConcurrency = 4 } };
await blobClient.UploadAsync(stream, uploadOptions);
```

### ❌ Erreur: "SAS token expired"

**Cause:** Expiration time too short

**Solution:**
```csharp
// Increase expiration to 30 days (43200 minutes)
var sasUri = await exportService.GenerateSasUrlAsync(blobPath, expirationMinutes: 43200);
```

---

## 📊 Résumé des fichiers

| Fichier | Rôle |
|---------|------|
| `sql/01-create-tables.sql` | Schéma PostgreSQL (tables, contraintes, seed data) |
| `sql/02-create-views-powerbi.sql` | 8 vues optimisées pour Power BI |
| `src/PAFA.Api/Services/PowerBiExportService.cs` | Service export/blob/SAS |
| `src/PAFA.BatchReports/Program.cs` | Orchestration batch (refresh + export + upload) |
| `appsettings.json` | Configuration Power BI / Azure Storage |
| `kubernetes-cronjob.yaml` | Scheduling mensuel (Kubernetes) |

---

**✅ Checklist d'implémentation**

- [ ] Service Principal créé & configuré
- [ ] Permissions tenant settings activées
- [ ] PBIX publiées à Power BI Service
- [ ] IDs (Workspace, Reports, Datasets) notés dans appsettings.json
- [ ] Container "exports" créé sur Azure Storage
- [ ] NuGet packages installées
- [ ] PowerBiExportService implémentée & injectable
- [ ] Batch job exécuté avec succès (dev/test)
- [ ] Blobs uploadés visibles dans Azure Storage
- [ ] SAS URLs générées & stockées dans DB
- [ ] CronJob déployé (prod)
- [ ] Monitoring & alerting configurés

---

**Besoin d'aide?** Référez-vous à:
- [Microsoft Power BI REST API Docs](https://docs.microsoft.com/en-us/rest/api/power-bi/)
- [Azure Storage Blob SDK](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-dotnet)
- Logs de PAFA.BatchReports (Application Insights / stdout)

