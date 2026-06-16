# ⚡ PAFA — Checklist Exécution Rapide

**Date:** 2026-06-14  
**Durée estimée:** 2-3 heures  
**Responsable:** Équipe implémentation

---

## 📋 Prérequis (Vérifier en premier)

- [ ] ✅ .NET 9 SDK installé (`dotnet --version`)
- [ ] ✅ dotnet-ef CLI installé (`dotnet ef --version`)
- [ ] ✅ PostgreSQL 14+ accessible (`psql --version`)
- [ ] ✅ Power BI Desktop installé
- [ ] ✅ Accès Azure (pour Storage Account + AD)
- [ ] ✅ Git repository local à jour

---

## 🔧 Phase 1: Base de Données (30 min)

### Étape 1.1: Exécuter SQL tables

```powershell
# Depuis psql ou pgAdmin
psql -h localhost -d pafa -U postgres -f sql/01-create-tables.sql

# Vérifier
psql -h localhost -d pafa -U postgres -c "
  SELECT table_name FROM information_schema.tables 
  WHERE table_schema = 'public' AND table_type = 'BASE TABLE' 
  ORDER BY table_name;"
```

**Résultat attendu:** 11 tables (reports, ingestion_jobs, metric_values, etc.)

**Temps:** 5 min

---

### Étape 1.2: Exécuter SQL views

```powershell
# Depuis psql
psql -h localhost -d pafa -U postgres -f sql/02-create-views-powerbi.sql

# Vérifier
psql -h localhost -d pafa -U postgres -c "
  SELECT table_name FROM information_schema.views 
  WHERE table_schema = 'public' AND table_name LIKE 'vw_%' OR table_name LIKE 'v_%'
  ORDER BY table_name;"
```

**Résultat attendu:** 8 vues (vw_dim_date, v_parr_industry, vw_2a1_leaderboard, etc.)

**Temps:** 5 min

---

### Étape 1.3: Appliquer migration EF Core

```powershell
cd c:\Users\hlouati\Desktop\PAFAProject

dotnet ef database update \
  --project src/PAFA.Infrastructure \
  --startup-project src/PAFA.Api

# Résultat
# Build started...
# Applying migration 'AddReportingAndExportTables'
# Done. Success!
```

**Temps:** 10 min

---

### Étape 1.4: Vérifier seed data

```powershell
psql -h localhost -d pafa -U postgres -c "
  SELECT id, code, label, audience 
  FROM report_types 
  ORDER BY id;"
```

**Résultat attendu:**
```
 id | code |           label           | audience
----+------+---------------------------+----------
  1 | SCH2A | Industry Peer Comparison   | Industry
  2 | SCH2B | Performance Assurance Comm | PAC
```

**Temps:** 3 min

---

## 💻 Phase 2: Code C# (60 min)

### Étape 2.1: Ajouter fichier PowerBiExportService

```powershell
# Copier code depuis: docs/EXPORT_REPORTS_COMPLETE_GUIDE.md (Section 4)
# Vers: src/PAFA.Api/Services/PowerBiExportService.cs

# Fichier créé (code fourni dans le guide)
```

**Temps:** 20 min (copy-paste + review)

---

### Étape 2.2: Installer NuGet packages

```powershell
cd src/PAFA.Api

dotnet add package Microsoft.PowerBI.Api --version 1.47.0
dotnet add package Azure.Identity --version 1.13.0
dotnet add package Azure.Storage.Blobs --version 12.19.0
dotnet add package Microsoft.Identity.Client --version 4.60.0

# Vérifier
dotnet build
```

**Temps:** 15 min

---

### Étape 2.3: Mettre à jour Program.cs

```csharp
// Ajouter dans Program.cs:
builder.Services.AddScoped<IPowerBiExportService, PowerBiExportService>();

// Azure Blob client
var blobUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/exports");
var blobContainerClient = new BlobContainerClient(blobUri, new DefaultAzureCredential());
builder.Services.AddSingleton(blobContainerClient);
```

**Temps:** 10 min

---

### Étape 2.4: Configurer appsettings.Development.json

```json
{
  "PowerBi": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientSecret": "YOUR_SECRET_HERE",
    "WorkspaceId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Report2AId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Report2BId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Dataset2AId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Dataset2BId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;",
    "ContainerName": "exports",
    "AccountName": "your-storage-account"
  }
}
```

**Temps:** 15 min

---

## 📊 Phase 3: Power BI & Service Principal (60 min)

### Étape 3.1: Créer rapports Power BI (en parallèle avec Phase 2)

**2A_Industry_Comparison.pbix (Anonymisé):**
1. Open Power BI Desktop
2. Get Data > PostgreSQL
   - Server: localhost
   - Database: pafa
3. Load tables: v_parr_industry, vw_dim_date, vw_dim_shipper
4. Create relationships: shipper ← fact ← date
5. Add 19 slides (schedule 2A)
6. Import DAX measures from docs/powerbi/DAX_MEASURES.md
7. Save & Publish > Workspace: "PAFA-Reports"

**2B_PAC_Performance.pbix (Non-Anonymisé):**
1. Same structure as 2A
2. Use v_parr_pac (real shipper names)
3. Implement RLS (Row-Level Security)
4. 22 slides for Schedule 2B

**Temps:** 90 min (peut être fait en parallèle avec Phase 2)

---

### Étape 3.2: Configurer Service Principal (Azure Admin)

```powershell
# 1. Register app in Azure AD
Connect-AzureAD
$app = New-AzureADApplication -DisplayName "PAFA-PowerBI-Exporter"
$secret = New-AzureADApplicationPasswordCredential -ObjectId $app.ObjectId

# 2. Save these:
# - App ID (Client ID): $app.AppId
# - Tenant ID: (from Azure AD portal)
# - Secret: $secret.Value

# 3. In Power BI Admin Portal:
# - Go to Tenant Settings
# - Enable "Allow service principals to use Power BI APIs"
# - Assign app to workspace with Admin role
```

**Temps:** 30 min (peut être en parallèle)

---

## 🧪 Phase 4: Test & Validation (20 min)

### Étape 4.1: Tester export (depuis terminal)

```powershell
cd src/PAFA.BatchReports

$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run

# Logs attendus:
# [INFO] Refreshing dataset 2A...
# [INFO] Exporting report 2A to PPTX...
# [INFO] Uploading to blob...
# [INFO] Generated SAS URL...
```

**Temps:** 10 min

---

### Étape 4.2: Vérifier résultats

```sql
-- 1. Vérifier blobs uploadés
SELECT COUNT(*) AS blob_count FROM (
  SELECT blob_name FROM azure_blob_storage
) t;
-- Expected: 4 (2A PPTX + XLSX, 2B PPTX + XLSX)

-- 2. Vérifier base de données
SELECT id, reporting_period, status, file_path_pptx 
FROM reports 
WHERE reporting_period = CURRENT_DATE 
LIMIT 4;
-- Expected: 4 rows avec SAS URLs
```

**Temps:** 5 min

---

### Étape 4.3: Tester téléchargement

```powershell
# Récupérer une SAS URL et télécharger
$sasUrl = "https://pafast.blob.core.windows.net/exports/Report_*.pptx?sv=2021-06-08&..."
Invoke-WebRequest -Uri $sasUrl -OutFile "C:\downloads\Report.pptx"

# Vérifier le fichier
Get-Item "C:\downloads\Report.pptx" | Select-Object Name, Length
```

**Temps:** 5 min

---

## ✅ Checklist finale

**Base de données:**
- [ ] 11 tables créées
- [ ] 8 vues créées
- [ ] Migration EF Core appliquée
- [ ] Seed data présent (ReportTypes, ProductClasses)

**Code C#:**
- [ ] PowerBiExportService créé et injectable
- [ ] NuGet packages installés
- [ ] Program.cs mis à jour (DI)
- [ ] appsettings.json configuré

**Power BI:**
- [ ] PBIX 2A créé (19 slides)
- [ ] PBIX 2B créé (22 slides)
- [ ] Publiés à Power BI Service
- [ ] IDs notés dans appsettings.json

**Azure:**
- [ ] Service Principal créé & autorisé
- [ ] Container "exports" créé
- [ ] Tenant settings activées

**Validation:**
- [ ] Batch job exécuté sans erreur
- [ ] Blobs uploadés visibles
- [ ] SAS URLs générées
- [ ] Base de données mise à jour (reports table)

---

## 📞 Dépannage rapide

### ❌ "Access Denied" lors du refresh
→ Vérifier: Service Principal a Admin role sur workspace

### ❌ "Database update failed"
→ Exécuter: `dotnet ef database update --verbose` pour logs

### ❌ "Blob upload timeout"
→ Vérifier: Connection string + permissions Azure Storage

### ❌ "SAS token invalid"
→ Vérifier: Token not expired, URL format correct

---

## 📊 Timeline résumée

| Phase | Durée | Parallèle |
|-------|-------|----------|
| Phase 1: DB SQL | 30 min | - |
| Phase 2: Code C# | 60 min | + Phase 3 |
| Phase 3: Power BI + Service Principal | 60 min | + Phase 2 |
| Phase 4: Test | 20 min | - |
| **TOTAL** | **2-3 heures** | **Oui** |

---

## 📁 Fichiers clés à lire

1. **Quick Start:** [docs/QUICK_START_IMPLEMENTATION.md](docs/QUICK_START_IMPLEMENTATION.md)
2. **Guide Complet:** [docs/EXPORT_REPORTS_COMPLETE_GUIDE.md](docs/EXPORT_REPORTS_COMPLETE_GUIDE.md)
3. **Résumé:** [docs/IMPLEMENTATION_SUMMARY.md](docs/IMPLEMENTATION_SUMMARY.md)
4. **DAX Measures:** [docs/powerbi/DAX_MEASURES.md](docs/powerbi/DAX_MEASURES.md)
5. **Index:** [docs/DOCUMENTATION_INDEX.md](docs/DOCUMENTATION_INDEX.md)

---

## 🎯 Prochaines étapes (après implémentation)

1. **Déployer K8s CronJob** pour export mensuel automatique
2. **Configurer monitoring & alertes** (Application Insights)
3. **Former les utilisateurs** (Power BI + API)
4. **Documenter runbooks** (ops/troubleshooting)
5. **Mettre en place backup** (blobs + database)

---

**✅ Status:** Ready for implementation  
**Last Updated:** 2026-06-14  
**Questions?** Voir [docs/EXPORT_REPORTS_COMPLETE_GUIDE.md](docs/EXPORT_REPORTS_COMPLETE_GUIDE.md#dépannage)

