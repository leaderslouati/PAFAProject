# 🏗️ ARCHITECTURE FINALE — PAFA Reports 2A & 2B + Dashboard

## 🎯 Décisions d'Architecture

### 1. Model Power BI : Dual-Dataset Strategy

**Choix:** 2 fichiers PBIX séparés (un par audience)

#### Dataset 1: `PAFA_Schedule_2A.pbix` (Anonymisé - Public)
```
Tables:
├── vw_dim_shipper (shipper alias codes only)
├── vw_dim_date (time dimension)
├── v_parr_industry (fact - anonymised)
├── product_classes (reference)
└── vw_2a1_leaderboard (aggregated)

Measures: 20+ DAX
Refresh: Daily 04:00 UTC
RLS: By audience/region
```

#### Dataset 2: `PAFA_Schedule_2B.pbix` (Non-Anonymisé - Restricted)
```
Tables:
├── vw_dim_shipper (real names + aliases)
├── vw_dim_date (time dimension)
├── v_parr_pac (fact - real shipper names)
├── product_classes (reference)
└── vw_2a2_no_meter (detail analysis)

Measures: 20+ DAX (identical to 2A)
Refresh: Daily 04:00 UTC
RLS: By shipper (users see only their data)
```

**Rationale:**
- ✅ Separate security models (anonymity vs. real names)
- ✅ Independent refresh schedules
- ✅ Easier audit trail
- ✅ Scalable to multiple datasets per shipper

---

### 2. Data Flow : From Files to Reports

```
┌──────────────────────────────────────────────────────────┐
│ Step 1: INPUT (Monthly Files)                            │
├──────────────────────────────────────────────────────────┤
│ SharePoint → MOD520A__PAF_Reports_Apr26_*.xlsx          │
│             → Downloaded to Blob: /inbound/{Y}/{M}/     │
└────────────┬─────────────────────────────────────────────┘
             │
┌────────────▼─────────────────────────────────────────────┐
│ Step 2: VALIDATION (Existing Pipeline)                   │
├──────────────────────────────────────────────────────────┤
│ ExcelInspectionService.Inspect() → Apply 6 rules         │
│ ✅ Valid: /processed/{Y}/{M}/ → metric_values INSERT    │
│ ❌ Invalid: /quarantine/ → ValidationError INSERT       │
└────────────┬─────────────────────────────────────────────┘
             │
┌────────────▼─────────────────────────────────────────────┐
│ Step 3: DATABASE (Star Schema)                            │
├──────────────────────────────────────────────────────────┤
│ Tables:                                                   │
│ ├── metric_values (EAV: shipper × metric × value)        │
│ ├── shippers (dimension)                                 │
│ ├── shipperAlias (anonymization mapping)                 │
│ └── product_classes (dimension)                          │
│                                                           │
│ Views (NEW):                                             │
│ ├── vw_dim_date (time dimension)                         │
│ ├── vw_2a1_leaderboard (ranking)                         │
│ ├── vw_2a1_distribution (histogram)                      │
│ └── vw_2a2_no_meter (detail analysis)                    │
│                                                           │
│ Views (EXISTING):                                        │
│ ├── vw_dim_shipper (shipper ref)                         │
│ ├── fact_read_performance (pivoted fact)                 │
│ ├── v_parr_industry (2A - anonymised)                    │
│ └── v_parr_pac (2B - non-anonymised)                     │
└────────────┬─────────────────────────────────────────────┘
             │
┌────────────▼─────────────────────────────────────────────┐
│ Step 4: POWER BI MODELS (Dual-Dataset)                   │
├──────────────────────────────────────────────────────────┤
│ Dataset 2A (Anonymised):                                 │
│ ├── Tables: dim_shipper (alias), fact (anonymised)      │
│ ├── Measures: 20+ DAX (Avg, Compliance, Trends)         │
│ ├── RLS: None (already anonymised)                       │
│ └── Refresh: Daily 04:00 UTC                            │
│                                                           │
│ Dataset 2B (Non-Anonymised):                             │
│ ├── Tables: dim_shipper (real name), fact (real name)   │
│ ├── Measures: 20+ DAX (identical)                        │
│ ├── RLS: By real shipper name                            │
│ └── Refresh: Daily 04:00 UTC                            │
└────────────┬─────────────────────────────────────────────┘
             │
        ┌────┴────┬──────────────┬──────────────┐
        │          │              │              │
    ┌───▼────┐ ┌──▼────┐  ┌─────▼──────┐  ┌───▼────┐
    │Report  │ │Report │  │Dashboard   │  │API     │
    │2A PDF  │ │2B PDF │  │PPTX Exec   │  │REST    │
    │(PBI)   │ │(PBI)  │  │(RB+Export) │  │(.NET)  │
    └────┬───┘ └──┬────┘  └─────┬──────┘  └───┬────┘
         │         │            │             │
         └─────────┴────────────┴─────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│ Step 5: EXPORT & DELIVERY                   │
├───────────────────────────────────────────┤
│ Azure Blob Storage (/reports/{Y}/{M}/)     │
│ ├── PARR_2025_04_Schedule_2A.pptx (v1)    │
│ ├── PARR_2025_04_Schedule_2B.pptx (v1)    │
│ ├── PARR_2025_04_Dashboard.pptx (v1)      │
│ └── SAS URLs (read-only, 7-day expiry)    │
│                                            │
│ API Endpoints:                             │
│ ├── POST /api/reports/export               │
│ ├── GET /api/reports/{id}/download         │
│ ├── POST /api/dataset/refresh              │
│ └── GET /api/metrics/{period}              │
└────────────────────────────────────────────┘
```

---

### 3. Security Model : RLS + Service Principal

#### Power BI RLS (Row-Level Security)

**Role: ShipperUser (v_parr_pac only)**
```dax
-- Restricts rows to current user's shipper
[real_shipper_name] = USERPRINCIPALNAME()

-- Example:
-- User: john.smith@sse.com
-- Sees only: SSE Energy Solutions rows
-- Does NOT see: BGT, OVO, EON, NPW rows
```

**Role: Admin**
```dax
-- No filter - sees all data
-- TRUE()
```

**Role: Industry (v_parr_industry only)**
```dax
-- No shipper filter (already anonymised)
-- TRUE()
```

#### Service Principal for Refresh
```json
{
  "TenantId": "your-tenant-id",
  "ClientId": "service-principal-id",
  "ClientSecret": "***",
  "PowerBiServicePrincipalId": "***"
}
```

**Usage:**
- Scheduled dataset refresh (daily 04:00 UTC)
- Batch export reports (monthly)
- No user intervention needed

---

### 4. Database Schema : Star Schema + EAV

```sql
-- DIMENSION TABLES
shippers
├── Id (PK)
├── short_code (e.g., "SSE")
├── name (e.g., "SSE Energy Solutions")
├── is_active
└── region, network_code, ...

shipperAlias
├── Id (PK)
├── ShipperId (FK → shippers)
├── AliasCode (e.g., "A001")
└── IsDeleted (soft delete)

product_classes
├── Code (e.g., "PC1")
├── Name
├── MinReadPercentage (e.g., 97.5)
└── ...

-- FACT TABLE (EAV - Entity-Attribute-Value)
metric_values
├── Id (PK)
├── ReportingPeriod (DateOnly)
├── ShipperShortCode (e.g., "SSE")
├── MetricKey (e.g., "read_performance_pct")
├── Value (decimal)
├── ProductClassCode (e.g., "PC1")
└── IngestionFileId (FK → IngestionFile)

-- VIEWS
vw_dim_shipper
├── shipper_code
├── real_shipper_name
├── alias_code
└── is_active

v_parr_industry (2A)
├── shipper_code (ALIAS ONLY)
├── product_class
├── read_perf_pct
├── estimated_pct
├── ... (NO real_shipper_name)

v_parr_pac (2B)
├── shipper_code
├── real_shipper_name
├── product_class
├── read_perf_pct
├── estimated_pct
└── ... (INCLUDES real_shipper_name)

fact_read_performance
├── report_month
├── shipper_code
├── product_class
├── read_perf_pct
├── estimated_pct
├── total_sites
├── is_compliant
└── unc_threshold
```

---

### 5. Refresh Strategy : Incremental + Full

#### Daily Refresh (04:00 UTC)
```yaml
Trigger: Power BI Scheduler
Duration: 5 minutes (Import mode)
Scope: All datasets

Process:
  1. Refresh vw_dim_date (static, rarely changes)
  2. Refresh fact_read_performance (latest metric_values)
  3. Refresh v_parr_industry (v_parr_pac for 2B)
  4. Refresh aggregated views (leaderboard, distribution)
  5. Notify users on Slack (completion status)
```

#### Weekly Full Refresh (Monday 03:00 UTC)
```yaml
Trigger: Power BI Scheduler
Duration: 15 minutes
Scope: All tables + relationships

Process:
  1. Clear all cache
  2. Re-import all tables from PostgreSQL
  3. Rebuild relationships
  4. Refresh all DAX measures
  5. Test data integrity
```

#### On-Demand Refresh (API)
```
POST /api/dataset/{datasetId}/refresh

-- Triggered by:
-- - Pipeline completion (PersistFilesHandler)
-- - Admin request (UI)
-- - Custom schedule (future)
```

---

### 6. Export & Versioning Strategy

#### Report Versioning
```
Naming: PARR_{YYYY}_{MM}_Schedule_{2A|2B|Dashboard}_v{N}.pptx

Examples:
├── PARR_2025_03_Schedule_2A_v1.pptx
├── PARR_2025_03_Schedule_2A_v2.pptx (if re-exported)
├── PARR_2025_03_Schedule_2B_v1.pptx
├── PARR_2025_03_Dashboard_v1.pptx
├── PARR_2025_04_Schedule_2A_v1.pptx (next month)
└── ...

-- Blob Structure:
reports/
├── 2025-03/
│   ├── PARR_2025_03_Schedule_2A_v1.pptx
│   ├── PARR_2025_03_Schedule_2A_v2.pptx
│   ├── PARR_2025_03_Schedule_2B_v1.pptx
│   └── metadata.json
└── 2025-04/
    ├── PARR_2025_04_Schedule_2A_v1.pptx
    └── metadata.json
```

#### SAS URLs with Constraints
```
URL: https://pafareports.blob.core.windows.net/reports/2025-04/PARR_2025_04_Schedule_2A_v1.pptx
     ?sv=2021-06-08
     &sr=b
     &sig=***
     &se=2025-04-18T15:45:00Z  (7-day expiry)
     &sp=r               (read-only)

Constraints:
- Single read-only access
- 7-day expiry
- Non-transferable
- No download to save (unless allowed)
```

---

### 7. API Rate Limiting & Throttling

```
Rate Limits:
├── Export endpoint: 10 req/min per user
├── Download endpoint: 100 req/min
├── Refresh endpoint: 1 req/min per dataset (admin only)
├── Metrics endpoint: 60 req/min per user
└── Embed token: 100 req/min

Backoff Strategy:
├── 1st retry: 5 seconds
├── 2nd retry: 10 seconds
├── 3rd retry: 30 seconds
└── Fail after 3 retries
```

---

### 8. Error Handling & Retry Logic

```csharp
// Pseudo-code for resilience

public async Task<ReportExportResult> ExportReportAsync(
    string reportId, 
    CancellationToken ct)
{
    var maxRetries = 3;
    var retryDelays = new[] { 5, 10, 30 }; // seconds
    
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var result = await _powerBiClient.ExportAsync(reportId, ct);
            
            // Success - upload to Blob
            await _blobStorage.UploadAsync(result.Stream, $"reports/...", ct);
            
            return new ReportExportResult(Success: true, JobId: result.Id);
        }
        catch (TimeoutException ex) when (attempt < maxRetries)
        {
            _logger.LogWarning($"Attempt {attempt} failed, retrying in {retryDelays[attempt-1]}s");
            await Task.Delay(retryDelays[attempt - 1] * 1000, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Export failed after {attempt} attempts");
            
            if (attempt == maxRetries)
            {
                // Notify admin
                await _notificationService.NotifyExportFailureAsync(reportId, ex.Message, ct);
                throw;
            }
        }
    }
    
    throw new InvalidOperationException("Export failed after all retries");
}
```

---

### 9. Monitoring & Observability

```
Metrics to Track:
├── Dataset refresh duration (target: < 5 min)
├── Report export duration (target: < 2 min)
├── API response time (p95: < 2 sec)
├── RLS validation time (< 100ms)
├── Blob upload success rate (target: 99.9%)
└── Daily refresh success rate (target: 99%)

Alerts:
├── Refresh fails → Slack notification
├── Export > 5 min → Warning in logs
├── RLS query > 500ms → Performance flag
├── Blob storage > 80% → Storage warning
└── API errors > 5% → Page incident
```

---

## 📊 TECHNOLOGY STACK

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| **Database** | PostgreSQL | 14+ | Data persistence, views |
| **ORM** | Entity Framework Core | 9.0 | Migrations, queries |
| **API** | ASP.NET Core | 9.0 | REST endpoints |
| **BI** | Power BI Service | Premium | Reports, dashboards, RLS |
| **Report Builder** | SQL Server Reporting Services | 2019+ | PPTX export |
| **Storage** | Azure Blob Storage | Standard | Report archival, SAS URLs |
| **Auth** | Azure AD / OAuth 2.0 | - | User identity, service principal |
| **Messaging** | Azure Service Bus | Standard | Notifications |
| **Logging** | Application Insights | - | Monitoring, diagnostics |

---

## 🔐 COMPLIANCE & SECURITY

### GDPR Compliance
- ✅ Anonymisation in v_parr_industry (no PII)
- ✅ RLS in v_parr_pac (restricted to authorized users)
- ✅ SAS URL expiry (automatic cleanup)
- ✅ Audit logs (all access tracked)

### Data Retention
- ✅ Reports: 90 days (then archived)
- ✅ Logs: 30 days
- ✅ Audit trail: 1 year
- ✅ Backups: 7 days

### Encryption
- ✅ In-transit: TLS 1.2+
- ✅ At-rest: Azure Storage encryption (AES-256)
- ✅ Database: SSL connection

---

## ✅ DEPLOYMENT CHECKLIST

### Pre-Deployment
- [ ] All SQL views created & tested
- [ ] Power BI datasets imported & validated
- [ ] DAX measures tested (accuracy, performance)
- [ ] API endpoints tested (load test, security)
- [ ] RLS roles configured & validated
- [ ] SAS URL generation tested
- [ ] Refresh schedule configured
- [ ] Alerts & monitoring set up

### Deployment
- [ ] Database migrations applied
- [ ] Power BI workspaces created (Dev, UAT, Prod)
- [ ] Service principal registered
- [ ] API deployed to App Service
- [ ] Storage account configured
- [ ] DNS entries updated

### Post-Deployment
- [ ] Test end-to-end flow (file → report → download)
- [ ] Verify RLS (test with multiple users)
- [ ] Monitor first refresh (24h)
- [ ] User training completed
- [ ] Go-live announcement

---

**Version:** 1.0  
**Date:** 11 Juin 2026  
**Author:** Tech Lead + Power BI Architect  
**Status:** Ready for Implementation

