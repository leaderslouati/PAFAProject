# PAFA — Pipeline for Automatic File Aggregation

> **Platform:** .NET 9 · **Database:** PostgreSQL · **File Source:** SharePoint Online (Microsoft Graph) · **Storage:** MinIO / Local · **Real-time:** SignalR

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture](#2-architecture)
3. [Solution Structure](#3-solution-structure)
4. [Domain Model](#4-domain-model)
5. [Ingestion Pipeline](#5-ingestion-pipeline)
6. [File Validation Rules](#6-file-validation-rules)
7. [API Reference](#7-api-reference)
8. [Authentication & Roles](#8-authentication--roles)
9. [Reporting & Exports](#9-reporting--exports)
10. [Batch Process (PAFA.BatchReports)](#10-batch-process-pafabatchreports)
11. [Real-time Notifications (SignalR)](#11-real-time-notifications-signalr)
12. [Configuration Reference](#12-configuration-reference)
13. [Database & Migrations](#13-database--migrations)
14. [Developer Onboarding](#14-developer-onboarding)
15. [Environment Setup](#15-environment-setup)
16. [Running the Application](#16-running-the-application)
17. [Extending the Platform](#17-extending-the-platform)

---

## 1. Project Overview

PAFA is a **monthly data ingestion and reporting platform** built for energy-sector compliance. It automates the collection of **PARR (Performance and Reporting Records)** files from SharePoint Online, validates them against business rules, stores structured metrics in PostgreSQL, and generates regulatory reports (PDF, Excel) and Power BI exports.

### Key Capabilities

| Capability | Description |
|---|---|
| **Automated Ingestion** | Cron-triggered (days 18–21 of each month) download from SharePoint |
| **Manual Trigger** | Admin can trigger ingestion at any time via `POST /api/ingest` |
| **File Validation** | Structural + business rule validation with per-row error tracking |
| **Metrics Storage** | Normalised `MetricValue` rows stored in PostgreSQL per shipper/period |
| **Report Generation** | Batch PDF + Excel report generation via `PAFA.BatchReports` |
| **Power BI Export** | CSV export optimised for Power BI consumption |
| **Dashboard API** | Aggregated KPI summary endpoint for frontend dashboard |
| **Real-time** | SignalR hub pushes ingestion progress to connected clients |
| **User Management** | Role-based access control with 4 fixed roles |

---

## 2. Architecture

```
???????????????????????????????????????????????????????????????????????
?                        PAFA Platform                                ?
?                                                                     ?
?  ????????????????    ????????????????????????????????????????????  ?
?  ? PAFA.Api     ?    ?  PAFA.BatchReports (Console App)         ?  ?
?  ? (ASP.NET 9)  ?    ?  --ingest | --reports | (full pipeline)  ?  ?
?  ????????????????    ????????????????????????????????????????????  ?
?         ?                               ?                           ?
?         ?          MediatR (CQRS)       ?                           ?
?         ?                               ?                           ?
?  ????????????????????????????????????????????????????????????????  ?
?  ?                     PAFA.Extraction                          ?  ?
?  ?  Commands / Handlers / Validators / Mappers                  ?  ?
?  ?                                                              ?  ?
?  ?  DownloadParrFilesCommand  ???  SharePoint (Graph API)       ?  ?
?  ?  UploadParrFilesCommand    ???  Parse ? Validate ? Store     ?  ?
?  ????????????????????????????????????????????????????????????????  ?
?         ?                                                           ?
?         ?                                                           ?
?  ????????????????????????????????????????????????????????????????  ?
?  ?                   PAFA.Infrastructure                        ?  ?
?  ?  Repositories · EF Core · File Parsers · Blob Storage        ?  ?
?  ?  SharePointFileSource · LocalBlobStorage · MinioBlobStorage  ?  ?
?  ?????????????????????????????????????????????????????????????  ?  ?
?                             ?                                       ?
?                             ?                                       ?
?                  ??????????????????????????                        ?
?                  ?  PostgreSQL (pafadb)    ?                        ?
?                  ??????????????????????????                        ?
?                                                                     ?
?  ????????????????  ????????????????  ???????????????????????????? ?
?  ? PAFA.Domain  ?  ?PAFA.Messaging?  ?     PAFA.Reports         ? ?
?  ? Entities     ?  ?  Events      ?  ?  PDF/Excel/CSV Writers   ? ?
?  ? Interfaces   ?  ?              ?  ?  Dashboard Queries       ? ?
?  ? Enums        ?  ?              ?  ?  BatchReportOrchestrator ? ?
?  ????????????????  ????????????????  ???????????????????????????? ?
???????????????????????????????????????????????????????????????????????
```

### Design Patterns

- **CQRS with MediatR** — all operations go through `IRequest<TResult>` commands/queries
- **Repository + Unit of Work** — all database access goes through `IUnitOfWork`
- **Clean Architecture** — `Domain` has zero external dependencies; `Infrastructure` implements domain interfaces
- **Strategy Pattern** — `IFileParser`, `IReportWriter`, `IBlobStorageService` are swappable implementations
- **Factory Pattern** — `FileParserFactory` resolves the correct parser by file extension

---

## 3. Solution Structure

```
PAFAProject/
??? src/
?   ??? PAFA.Api/                   ? ASP.NET Core 9 Web API
?   ?   ??? Controllers/
?   ?   ?   ??? IngestionController.cs    (POST /api/ingest)
?   ?   ?   ??? ImportController.cs       (POST /api/import/upload)
?   ?   ?   ??? ValidationController.cs   (GET /api/validation)
?   ?   ?   ??? DashboardController.cs    (GET /api/dashboard/summary)
?   ?   ?   ??? PowerBiController.cs      (GET /api/reports/powerbi)
?   ?   ?   ??? UsersController.cs        (POST /api/users)
?   ?   ?   ??? BatchReportController.cs  (POST /api/batch/trigger)
?   ?   ?   ??? HealthController.cs       (GET /api/health)
?   ?   ??? Hubs/
?   ?   ?   ??? IngestionHub.cs           (SignalR)
?   ?   ??? Program.cs
?   ?   ??? appsettings.json
?   ?
?   ??? PAFA.Domain/                ? Core business entities & contracts (no external deps)
?   ?   ??? Entities/
?   ?   ?   ??? Authentication/     (PafaUser, PafaRole, PafaUserRole)
?   ?   ?   ??? Ingestion/          (IngestionJob, IngestionFile, MetricValue, ValidationError)
?   ?   ?   ??? Referential/        (Shipper, ProductClass, ShipperAlias)
?   ?   ?   ??? Reporting/          (Report, ReportType, FactReadPerformance)
?   ?   ??? Enums/                  (PAFAEnums, ExportJobStatus)
?   ?   ??? IRepository/            (IBaseRepository, IUnitOfWork, ...)
?   ?   ??? Interfaces/             (IBlobStorageService, IRemoteFileSource, ...)
?   ?   ??? Constants/              (FileNamingConstants)
?   ?
?   ??? PAFA.Extraction/            ? CQRS Commands, Handlers, Validators, Mappers
?   ?   ??? Commands/
?   ?   ?   ??? Import/             (UploadParrFilesCommand)
?   ?   ?   ??? SharePoint/         (DownloadParrFilesCommand)
?   ?   ?   ??? Validation/         (DTOs for validation responses)
?   ?   ?   ??? Export/             (PowerBiCsvRowDto, DashboardSummaryDto)
?   ?   ?   ??? Users/              (CreateUserCommand)
?   ?   ??? Handlers/
?   ?   ?   ??? SharePoint_Online/  (DownloadParrFilesCommandHandler)
?   ?   ?   ??? ImportFile/         (UploadParrFilesHandler, ParseAndValidateFileHandler)
?   ?   ?   ??? Users/              (CreateUserCommandHandler)
?   ?   ??? Validations/
?   ?       ??? FileNameValidator.cs
?   ?       ??? FolderPathValidator.cs
?   ?       ??? ImportValidationService.cs
?   ?
?   ??? PAFA.Infrastructure/        ? EF Core, Repositories, Parsers, Storage, SharePoint
?   ?   ??? Data/                   (PafaDbContext, PafaDbContextFactory)
?   ?   ??? EntityConfigurations/
?   ?   ??? Migrations/
?   ?   ??? Parsing/                (ExcelFileParser, CsvFileParser, XmlFileParser, FileParserFactory)
?   ?   ??? Repository/             (BaseRepository, UnitOfWork, all repo implementations)
?   ?   ??? Services/               (IngestionScheduleService, LoggingEmailService)
?   ?   ??? SharePoint/             (SharePointFileSource, SharePointSettings)
?   ?   ??? Storage/                (LocalBlobStorageService, MinioBlobStorageService, BlobStorageSettings)
?   ?
?   ??? PAFA.Messaging/             ? Domain events (ready for RabbitMQ / Azure Service Bus)
?   ?   ??? Events/
?   ?       ??? FileIngestedEvent.cs
?   ?       ??? FileProcessedEvent.cs
?   ?       ??? FileReadyEvent.cs
?   ?       ??? ValidationFailedEvent.cs
?   ?
?   ??? PAFA.Notifications/         ? Placeholder for push notifications
?   ?
?   ??? PAFA.Reports/               ? Report generation logic
?   ?   ??? Batch/
?   ?   ?   ??? Core/               (BatchReportOrchestrator, PdfReportGenerator, ExcelReportGenerator)
?   ?   ?   ??? Configuration/      (BatchReportSettings)
?   ?   ?   ??? Models/             (ReportGenerationContext, ReportGenerationResult)
?   ?   ??? Dashboard/              (GetDashboardSummaryQuery + Handler)
?   ?   ??? Reports/                (ExportPowerBiCsvQuery + Handler)
?   ?   ??? Writers/                (CsvReportWriter, ExcelReportWriter, PdfReportWriter)
?   ?
?   ??? PAFA.BatchReports/          ? Standalone console app (cron / CI trigger)
?       ??? Program.cs
?
??? README.md
```

---

## 4. Domain Model

### Ingestion

```
IngestionJob (1) ???????????????? (*) IngestionFile
  ? Id (GUID)                          ? Id (GUID)
  ? JobName                            ? FileName
  ? ReportingPeriod (DateOnly)         ? SourceSystem (CDSP | DDP | AD_HOC)
  ? Status (enum)                      ? FileType (Xlsx | Csv | Xml)
  ? FilesDownloaded / Processed        ? BlobPath
  ? TriggeredBy (enum)                 ? Status (enum)
  ? RetryCount                         ? ValidationStatus (enum)
  ? ParentJobId ? self (retry chain)   ? RowsRead / Valid / Rejected
  ???????????????????????????????????? ? ValidationErrors (*)
                                       ??? MetricValues (*)
```

### MetricValue

Each validated data row is exploded into one `MetricValue` per metric key:

| Column | Type | Example |
|---|---|---|
| `ReportingPeriod` | `DateOnly` | `2025-02-01` |
| `ShipperShortCode` | `string` | `"SHP01"` |
| `MetricKey` | `string` | `"ReadPerfPct"` |
| `Value` | `decimal` | `98.4` |
| `ProductClassCode` | `string?` | `"1"` |

### Referential

| Entity | Purpose |
|---|---|
| `Shipper` | Energy shipper — holds `ShortCode` (SSC), market dates, portfolio size |
| `ProductClass` | Product classification with AQ threshold and minimum read percentage |
| `ShipperProductClass` | Many-to-many: which product classes a shipper is registered for |
| `ShipperAlias` | Alternative names mapped back to canonical `ShipperShortCode` |

### Reporting

| Entity | Purpose |
|---|---|
| `ReportType` | Schedule 2A (Industry/anonymised) or 2B (PAC/non-anonymised) |
| `Report` | Monthly report instance with PDF/Excel file paths and status |
| `FactReadPerformance` | Keyless entity mapped to the `fact_read_performance` SQL view |

### Enums

| Enum | Values |
|---|---|
| `IngestionJobStatus` | `Started`, `Processing`, `Completed`, `Failed`, `PartiallyCompleted`, `Cancelled` |
| `IngestionFileStatus` | `Downloaded`, `Validating`, `Valid`, `Invalid`, `Loaded`, `Failed` |
| `ValidationStatus` | `Pending`, `Passed`, `PassedWithWarnings`, `Failed` |
| `FileType` | `Xlsx`, `Xls`, `Csv`, `Xml` |
| `JobTrigger` | `Scheduler`, `Manual`, `Api`, `Retry` |
| `TriggerMode` | `Automatic`, `Manual` |
| `ReportAudience` | `Industry` (2A — anonymised), `PAC` (2B — non-anonymised) |
| `ExportFormat` | `Csv`, `Excel`, `Pdf`, `PowerBiEmbedded` |

---

## 5. Ingestion Pipeline

### Automatic Schedule

The cron window runs **days 18–21 of every month at 02:00 UTC**:

```
Cron: 0 2 18-21 * *   (configurable via IngestionSchedule:CronExpression)
```

### Full Pipeline Flow

```
1. IngestionScheduleService.ResolveTriggerMode()
        ?
        ?
2. DownloadParrFilesCommand dispatched via MediatR
        ?
        ?
3. DownloadParrFilesCommandHandler
    ??? FOLD-002: validate inbound folder path structure
    ??? TestConnectionAsync() ? SharePoint Online (Microsoft Graph)
    ??? ListFilesAsync() ? enumerate {BaseInboundPath}/{YYYY}/{MM}/
    ?
    ??? For each file:
        ??? FOLD-001: file is inside the expected Year/Month folder
        ??? NAME-001..004: file name convention check
        ??? DownloadFileAsync() ? bytes in memory
        ??? BlobStorage.UploadAsync() ? save to landing-zone
        ?
        ?
4. UploadParrFilesCommand dispatched via MediatR
        ?
        ?
5. UploadParrFilesCommandHandler
    ??? Create IngestionJob + IngestionFile records in DB
    ??? FileParserFactory ? parse bytes (Excel / CSV / XML)
    ??? ImportValidationService ? row-level business rules
    ??? Persist ValidationErrors to DB
    ??? MetricValueMapper ? explode rows ? MetricValue[]
    ??? Persist MetricValues to DB
    ??? Update IngestionJob / IngestionFile status
    ?
    ??? On success ? MoveFileAsync() ? /Processed/{YYYY}/{MM}/
        On failure ? MoveFileAsync() ? /Failed/{YYYY}/{MM}/
```

### TriggerSource Values

| Value | Set by |
|---|---|
| `CRON_AUTO` | Background hosted service (inside automatic window) |
| `MANUAL_API` | Admin via `POST /api/ingest` |
| `MANUAL_REPROCESS` | Admin via `POST /api/ingest/reprocess` |

### Reprocess / Retry Chain

When a reprocess is triggered, the new `IngestionJob` is linked to the previous one:

```
IngestionJob  (original   · Failed      · RetryCount = 0)
    ??? IngestionJob  (retry 1    · ParentJobId = original.Id · RetryCount = 1)
            ??? IngestionJob  (retry 2 · ParentJobId = retry1.Id  · RetryCount = 2)
```

---

## 6. File Validation Rules

### File Name Rules

| Rule | Severity | Description |
|---|---|---|
| `NAME-001` | **ERROR** | File name contains prohibited character(s): `* / ? : < > \| " \` |
| `NAME-002` | **ERROR** | Prefix not in allowed list (`MOD520A`, `RPT_1364`, `MOD700`, `EUC09`, `TRANSFER`, `CLASS4AQ`) |
| `NAME-003` | WARNING | Month token unreadable — file processed but flagged |
| `NAME-004` | **ERROR** | Extension not allowed (accepted: `.xlsx`, `.xls`, `.csv`, `.xml`) |

**Expected file name convention:**

```
{PREFIX}__{MonthToken}[YY[YY]][_vN].{ext}

Examples:
  MOD520A__Feb25.xlsx
  RPT_1364__07_v2.csv
  MOD700__February2025.xlsx
```

### Folder Path Rules

| Rule | Severity | Description |
|---|---|---|
| `FOLD-001` | **SKIP** | File found outside expected `{BaseInboundPath}/{YYYY}/{MM}` folder |
| `FOLD-002` | **ABORT** | Constructed inbound path does not conform to `Year/Month` structure |

### Content Validation Rules

| Rule | Severity | Description |
|---|---|---|
| `VAL-002` | **ERROR** | File is empty — no data rows detected |
| `VAL-003` | **ERROR** | `ReportingPeriod` field missing |
| `VAL-004` | **ERROR** | `ReportingPeriod` format invalid (accepted: `MMM-YY` e.g. `Feb-25` or `YYYY-MM`) |
| `VAL-005` | **ERROR** | `ShipperShortCode` field missing |
| `VAL-011` | INFO | PC1 `ReadPerformancePct` below 97.5% UNC threshold — shipper non-compliant (row imported but flagged) |
| `VAL-013` | **ERROR** | Duplicate `ShipperShortCode + Period` within the same file |

> Rules tagged **ERROR** are blocking — the affected row or file is rejected.
> Rules tagged **INFO / WARNING** are non-blocking — the row is imported but recorded in `ValidationErrors`.

---

## 7. API Reference

Base URL (dev): `http://localhost:5000`

Swagger UI: `http://localhost:5000/swagger`

### Health

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/health` | None | Quick DB connectivity check (Kubernetes liveness probe) |
| `GET` | `/api/health/full` | None | Full check: DB + SharePoint + Blob Storage |

### Ingestion

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/ingest` | `PafaAdmin` | Manually trigger the full SharePoint ingestion pipeline |
| `POST` | `/api/ingest/reprocess` | `PafaAdmin` | Re-trigger a specific period after file corrections |
| `GET` | `/api/ingest/schedule/status` | None | Returns cron window state and next window opening |

**`POST /api/ingest`** query params:

| Param | Type | Description |
|---|---|---|
| `year` | `int?` | Target year (2020–2040). Defaults to current UTC year |
| `month` | `int?` | Target month (1–12). Defaults to current UTC month |

**`POST /api/ingest/reprocess`** request body:

```json
{
  "year": 2025,
  "month": 2,
  "fileNameFilter": ["MOD520A__Feb25.xlsx"]
}
```

**Response codes:**

| Code | Meaning |
|---|---|
| `200 OK` | All files imported successfully |
| `207 Multi-Status` | Partial success — some files failed or were skipped |
| `500 Internal Server Error` | Total failure — no files imported |

### Import (direct upload)

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/import/upload` | None | Upload a PARR file directly (multipart/form-data) |
| `GET` | `/api/import/{fileId}/errors` | None | Get validation errors for an uploaded file |

### Validation

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/validation/{fileId}` | None | Full validation error list for a specific file |
| `GET` | `/api/validation/job/{jobId}` | None | Validation summary for all files in a job |

### Dashboard

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/dashboard/summary` | None | KPI summary: shipper count, PC1 compliance, avg read performance |

Query params: `?year=2025&month=2` (optional — defaults to all data)

### Reports & Exports

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/reports/powerbi` | None | Export metrics as CSV for Power BI |
| `GET` | `/api/reports/export/pdf` | None | Export report as PDF |
| `GET` | `/api/reports/export/excel` | None | Export report as Excel (.xlsx) |

Query params: `?year=2025&month=2` (optional)

### Users

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/users` | `PafaAdmin` | Create a new PAFA user account |
| `GET` | `/api/users/{id}` | `PafaAdmin` | Get a user by ID |

**`POST /api/users`** request body:

```json
{
  "firstName": "Alice",
  "lastName":  "Martin",
  "email":     "alice.martin@company.com",
  "jobTitle":  "Analyst",
  "department":"Gemserv",
  "roleIds":   [1]
}
```

### Batch

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/batch/trigger` | None | Trigger `PAFA.BatchReports` as a child process |

### SignalR Hub

| URL | Protocol |
|---|---|
| `/hubs/ingestion` | WebSocket (SignalR) |

---

## 8. Authentication & Roles

PAFA uses **JWT Bearer** authentication.

### Roles

| ID | Name | Access |
|---|---|---|
| 1 | `PafaUser` | Read reports, export, dashboard |
| 2 | `PafaAdmin` | Full access + user management + trigger ingestion |
| 3 | `PacMember` | Read B-reports (non-anonymised, Schedule 2B) |
| 4 | `Shipper` | Read own data only (A-reports, Schedule 2A, anonymised) |

### JWT Configuration

| Parameter | Default (dev) |
|---|---|
| Issuer | `pafa-api` |
| Audience | `pafa-client` |
| Signing Key | `Jwt:Key` in `appsettings.json` |

Pass the token in every protected request:

```http
Authorization: Bearer <your-jwt-token>
```

> ?? **Production**: replace the default signing key and store it in Azure Key Vault or a secrets manager. Never commit real keys to source control.

---

## 9. Reporting & Exports

### Power BI Export

`GET /api/reports/powerbi?year=2025&month=2`

Returns a CSV file `PAFA_PowerBI_2025_02.csv` with one row per shipper/period:

| Column | Source Metric Key |
|---|---|
| `PeriodeDate` | `ReportingPeriod` |
| `ShipperCode` | `ShipperShortCode` |
| `ReadPerformancePct` | `readperformancepct` |
| `EstimatedReadPct` | `estimatedreadpct` |
| `AqOverdueCount` | `aqoverduecount` |
| `TotalSiteCount` | `totalsitecount` |
| `ProductClass` | `productclass` |

### SQL Views (Power BI Direct Query)

Three views are created via migration `AddPowerBiReportingViews`:

| View | Purpose |
|---|---|
| `vw_dim_shipper` | Dimension: shipper code + name |
| `vw_dim_product_class` | Dimension: product class with AQ threshold |
| `fact_read_performance` | Fact: pivoted metric values per shipper/period/product class with compliance flag |

### Batch Report Formats

| Format | Writer class | Output filename |
|---|---|---|
| PDF | `PdfReportWriter` | `PAFA_Report_{year}_{month}.pdf` |
| Excel | `ExcelReportWriter` | `PAFA_Report_{year}_{month}.xlsx` |
| CSV | `CsvReportWriter` | `PAFA_PowerBI_{year}_{month}.csv` |

---

## 10. Batch Process (PAFA.BatchReports)

`PAFA.BatchReports` is a standalone **.NET 9 console application** that can be run on a schedule or triggered from the API (`POST /api/batch/trigger`).

### CLI Usage

```bash
# Full pipeline: ingest + generate reports (default when no flag is given)
dotnet run --project src/PAFA.BatchReports

# Ingest only — current UTC month
dotnet run --project src/PAFA.BatchReports -- --ingest

# Ingest only — specific period
dotnet run --project src/PAFA.BatchReports -- --ingest --year 2025 --month 2

# Generate reports only (from existing DB data)
dotnet run --project src/PAFA.BatchReports -- --reports
```

### Environment Variables (alternative to CLI flags)

```bash
PAFA_TargetYear=2025
PAFA_TargetMonth=2
```

### Exit Codes

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | Partial failure (some files failed or reports had errors) |
| `2` | Fatal error (DB unreachable, uncaught exception) |

### Batch Modes Summary

| Mode | Flag | What it does |
|---|---|---|
| `Full` | _(none)_ | SharePoint ingestion + PDF/Excel report generation |
| `Ingest` | `--ingest` | Download + validate + import files from SharePoint |
| `Reports` | `--reports` | Generate PDF/Excel from data already in the DB |

---

## 11. Real-time Notifications (SignalR)

Connect to the ingestion hub at:

```
ws://localhost:5000/hubs/ingestion
```

### Events Pushed to Clients

| Event name | When published |
|---|---|
| `FileDownloaded` | File successfully picked up from SharePoint and saved to blob storage |
| `ProcessingComplete` | File parsed, validated, and metrics inserted into DB |
| `ValidationError` | File failed validation — frontend should display an alert |

### Domain Events (PAFA.Messaging)

These records are defined and ready to be wired to a message broker (RabbitMQ / Azure Service Bus):

| Event | Key payload fields |
|---|---|
| `FileIngestedEvent` | `JobId`, `FileId`, `FileName`, `RowsRead`, `RowsValid`, `RowsRejected`, `Status` |
| `FileProcessedEvent` | `IngestionJobId`, `IngestionFileId`, `MetricsInserted`, `ProcessedAt` |
| `FileReadyEvent` | File available in the landing zone |
| `ValidationFailedEvent` | `IngestionFileId`, `ErrorMessage`, `ErrorCount`, `FailedAt` |

---

## 12. Configuration Reference

Full annotated `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=pafadb;Username=postgres;Password=postgres"
  },

  "IngestionSchedule": {
    "CronWindowStartDay": 18,
    "CronWindowEndDay":   21,
    "CronExpression":     "0 2 18-21 * *",
    "TimeZone":           "UTC"
  },

  "BlobStorage": {
    "Provider":   "Local",      // "Local" | "MinIO"
    "LocalPath":  "./storage",  // used when Provider = Local
    "Endpoint":   "localhost:9000",
    "AccessKey":  "minioadmin",
    "SecretKey":  "minioadmin",
    "UseSsl":     false
  },

  "SharePoint": {
    "TenantId":     "<Azure-AD-Tenant-GUID>",
    "ClientId":     "<App-Registration-GUID>",
    "ClientSecret": "<client-secret>",
    "SiteUrl":      "https://<tenant>.sharepoint.com/sites/<site>",
    "SiteId":       "<hostname,siteId,webId>",
    "DriveId":      "",         // leave empty to use the default site drive
    "BaseInboundPath": "",      // empty = root of drive; e.g. "/PARR"
    "ProcessedPath": "/Processed",
    "FailedPath":    "/Failed",
    "FilePattern":   "*.xlsx",
    "AllowedFilePrefixesList":         ["MOD520A","RPT_1364","MOD700","EUC09","TRANSFER","CLASS4AQ"],
    "AllowedExtensionsList":           [".xlsx",".xls",".csv",".xml"],
    "EnforceYearMonthFolderStructure": true
  },

  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:3001"
    ]
  },

  "Jwt": {
    "Key":      "PAFA_DEFAULT_DEV_KEY_CHANGE_IN_PRODUCTION_32CH",
    "Issuer":   "pafa-api",
    "Audience": "pafa-client"
  }
}
```

### Blob Storage Providers

| `Provider` value | Implementation class | Use case |
|---|---|---|
| `Local` | `LocalBlobStorageService` | Local development (files saved to disk) |
| `MinIO` | `MinioBlobStorageService` | On-premise or Docker |
| _(future)_ | `AzureBlobStorageService` | Production on Azure |

### SharePoint Folder Structure Expected by the Platform

```
{BaseInboundPath}/
??? {YYYY}/
?   ??? {MM}/
?       ??? MOD520A__Feb25.xlsx    ? inbound PARR files
?       ??? RPT_1364__02.csv
??? Processed/
?   ??? {YYYY}/{MM}/               ? moved here after successful import
??? Failed/
    ??? {YYYY}/{MM}/               ? moved here after failed import
```

---

## 13. Database & Migrations

### Schema Overview

| Table | Description |
|---|---|
| `pafa_users` | Application users |
| `pafa_roles` | 4 fixed roles |
| `pafa_user_roles` | User–role join table |
| `shippers` | Energy shippers reference data |
| `product_classes` | Product classification reference |
| `shipper_product_classes` | Shipper × product class mapping |
| `shipper_aliases` | Alternative shipper name mappings |
| `ingestion_jobs` | One job per monthly ingestion run |
| `ingestion_files` | One row per file processed within a job |
| `validation_errors` | Per-row validation findings |
| `metric_values` | Normalised metrics extracted from PARR files |
| `report_types` | Schedule 2A / 2B definitions |
| `reports` | Generated report instances with PDF/Excel file paths |

### SQL Views

| View | Mapped entity |
|---|---|
| `fact_read_performance` | `FactReadPerformance` (keyless) |
| `vw_dim_shipper` | Power BI dimension |
| `vw_dim_product_class` | Power BI dimension |

### Migration Commands

```bash
# Apply all pending migrations
dotnet ef database update \
  --project src/PAFA.Infrastructure \
  --startup-project src/PAFA.Api

# Create a new migration
dotnet ef migrations add <MigrationName> \
  --project src/PAFA.Infrastructure \
  --startup-project src/PAFA.Api

# Remove the last migration (before applying)
dotnet ef migrations remove \
  --project src/PAFA.Infrastructure \
  --startup-project src/PAFA.Api
```

---

## 14. Developer Onboarding

### Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **9.0+** | Build & run all projects |
| [PostgreSQL](https://www.postgresql.org/download/) | 14+ | Database |
| [Git](https://git-scm.com/) | any | Source control |
| [Docker](https://www.docker.com/) _(optional)_ | any | Run MinIO locally |
| [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) | latest | IDE |
| EF Core CLI | 9.0+ | Run migrations (`dotnet tool install -g dotnet-ef`) |

### Step-by-Step Setup

#### 1. Clone the repository

```bash
git clone https://github.com/leaderslouati/PAFAProject.git
cd PAFAProject
git checkout feature/sharepointconfiguration-implementation-cron
```

#### 2. Set up PostgreSQL

```bash
# Using psql
psql -U postgres -c "CREATE DATABASE pafadb;"
```

Or via SQL:

```sql
CREATE DATABASE pafadb;
CREATE USER postgres WITH PASSWORD 'postgres';
GRANT ALL PRIVILEGES ON DATABASE pafadb TO postgres;
```

#### 3. Configure `appsettings.json`

Edit `src/PAFA.Api/appsettings.json` and update the connection string if needed:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=pafadb;Username=postgres;Password=postgres"
}
```

#### 4. Install EF Core CLI (if not already installed)

```bash
dotnet tool install --global dotnet-ef
```

#### 5. Apply database migrations

```bash
dotnet ef database update \
  --project src/PAFA.Infrastructure \
  --startup-project src/PAFA.Api
```

#### 6. (Optional) Start MinIO for blob storage

```bash
docker run -d \
  -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin \
  -e MINIO_ROOT_PASSWORD=minioadmin \
  --name pafa-minio \
  minio/minio server /data --console-address ":9001"
```

Then update `appsettings.json`:

```json
"BlobStorage": {
  "Provider": "MinIO",
  "Endpoint": "localhost:9000",
  "AccessKey": "minioadmin",
  "SecretKey": "minioadmin",
  "UseSsl": false
}
```

MinIO Console: `http://localhost:9001`

#### 7. Configure SharePoint (if testing real ingestion)

Register an **App Registration** in Azure Active Directory:

1. Go to **Azure Portal ? Azure AD ? App registrations ? New registration**
2. Add API permission: `Microsoft Graph ? Application ? Sites.Read.All`
3. Grant admin consent
4. Create a client secret under **Certificates & secrets**
5. Copy `TenantId`, `ClientId`, `ClientSecret`

Retrieve the SharePoint `SiteId` via Graph Explorer:

```
GET https://graph.microsoft.com/v1.0/sites/{hostname}:/{site-relative-path}
```

Then update `appsettings.json`:

```json
"SharePoint": {
  "TenantId":     "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "ClientId":     "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
  "ClientSecret": "your-secret",
  "SiteUrl":      "https://contoso.sharepoint.com/sites/PAFA",
  "SiteId":       "contoso.sharepoint.com,aaa,bbb"
}
```

#### 8. Build the solution

```bash
dotnet build PAFAProject.sln
```

#### 9. Run the API

```bash
dotnet run --project src/PAFA.Api
```

- API: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`

---

## 15. Environment Setup

### `appsettings.Development.json` (local overrides)

Create `src/PAFA.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  },
  "SharePoint": {
    "EnforceYearMonthFolderStructure": false
  },
  "BlobStorage": {
    "Provider": "Local",
    "LocalPath": "./storage"
  }
}
```

### Override via Environment Variables

All config keys can be overridden using `__` as the hierarchy separator:

```bash
# Database
ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=pafadb;..."

# JWT (production secret)
Jwt__Key="your-production-secret-key-minimum-32-characters"

# Blob provider
BlobStorage__Provider="MinIO"
BlobStorage__Endpoint="minio.internal:9000"

# SharePoint secret (prefer Key Vault in production)
SharePoint__ClientSecret="your-azure-client-secret"
```

For `PAFA.BatchReports`, all `PAFA_`-prefixed environment variables are loaded automatically:

```bash
PAFA_TargetYear=2025
PAFA_TargetMonth=2
PAFA_ConnectionStrings__DefaultConnection="Host=..."
```

---

## 16. Running the Application

### Start the API

```bash
dotnet run --project src/PAFA.Api
```

### Start the Batch Process

```bash
# Full pipeline (ingest + reports) for current month
dotnet run --project src/PAFA.BatchReports

# Ingest only — force period
dotnet run --project src/PAFA.BatchReports -- --ingest --year 2025 --month 2

# Reports only
dotnet run --project src/PAFA.BatchReports -- --reports
```

### Check Health

```bash
# Quick check (DB only)
curl http://localhost:5000/api/health

# Full check (DB + SharePoint + Blob)
curl http://localhost:5000/api/health/full
```

### Manually Trigger Ingestion

```bash
# Requires a valid PafaAdmin JWT token
curl -X POST "http://localhost:5000/api/ingest?year=2025&month=2" \
  -H "Authorization: Bearer <your-jwt-token>"
```

### Reprocess a Period

```bash
curl -X POST "http://localhost:5000/api/ingest/reprocess" \
  -H "Authorization: Bearer <your-jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "year": 2025,
    "month": 2,
    "fileNameFilter": ["MOD520A__Feb25.xlsx"]
  }'
```

### Check Schedule Status

```bash
curl http://localhost:5000/api/ingest/schedule/status
```

---

## 17. Extending the Platform

### Add a New File Parser

1. Implement `IFileParser` in `PAFA.Infrastructure/Parsing/`
2. Register it in `Program.cs`:

```csharp
builder.Services.AddScoped<IFileParser, YourNewParser>();
```

### Add a New Blob Storage Provider

1. Implement `IBlobStorageService` in `PAFA.Infrastructure/Storage/`
2. Add a condition in `Program.cs`:

```csharp
if (blobProvider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
```

### Add a New Report Writer

1. Implement `IReportWriter` in `PAFA.Reports/Writers/`
2. Register it in `Program.cs`:

```csharp
builder.Services.AddScoped<IReportWriter, YourNewWriter>();
```

### Add a New Content Validation Rule

Add a private method in `ImportValidationService` and call it inside `Validate()`:

```csharp
private static void ValidateYourRule(RawDataRow row, List<ValidationFinding> findings)
{
    // ...
    findings.Add(new ValidationFinding(
        "VAL-0XX", "FieldName", value,
        ValidationSeverity.Error,
        "Your error message.",
        row.RowNumber, row.SheetName));
}
```

### Wire a Message Broker

`PAFA.Messaging` contains domain event definitions ready for **RabbitMQ** or **Azure Service Bus**. Implement publishers in the command handlers and consumers in `PAFA.Notifications`.

---

## Branch Strategy

| Branch | Purpose |
|---|---|
| `main` | Production-ready code |
| `feature/sharepointconfiguration-implementation-cron` | Current active development branch |

---

## Support

- **Team:** PAFA Team  
- **Email:** pafa-support@company.com  
- **Repository:** https://github.com/leaderslouati/PAFAProject
