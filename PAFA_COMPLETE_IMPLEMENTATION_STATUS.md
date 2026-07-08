# PAFA — État Complet de l'Implémentation & Guide d'Architecture
**Mis à jour :** 2026-07-01  
**Scope :** Pipeline complet SharePoint → PostgreSQL → Power BI Report Builder

**Source officielle du mapping :** `Files/PARR Reports - Mapping.xlsx`  
(Feuilles : *2B PARR Reports - Non Anonymise*, *2A PARR Reports - Anonymised*, *Dashboards*)

---

## TABLE DES MATIÈRES

1. [Architecture Globale](#1-architecture-globale)
2. [Fichiers Source → Mapping Reports](#2-fichiers-source--mapping-reports)
3. [Ce Qui Est Implémenté](#3-ce-qui-est-implémenté)
4. [Ce Qui Manque](#4-ce-qui-manque)
5. [Schéma Base de Données](#5-schéma-base-de-données)
6. [Comment Insérer les Fichiers (SharePoint OU Local)](#6-comment-insérer-les-fichiers)
7. [Vues SQL → Power BI : Mapping complet](#7-vues-sql--power-bi-mapping-complet)
8. [Connexion Power BI Report Builder](#8-connexion-power-bi-report-builder)
9. [Vues SQL : Statut d'Implémentation](#9-vues-sql--statut-dimplémentation)
10. [Roadmap des Actions Immédiates](#10-roadmap-des-actions-immédiates)

---

## 1. Architecture Globale

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        FLUX DE DONNÉES PAFA                                  │
└─────────────────────────────────────────────────────────────────────────────┘

  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌───────────┐
  │  SharePoint  │────▶│ Blob Storage │────▶│  PostgreSQL  │────▶│ Power BI  │
  │  (Input)     │     │  (MinIO/     │     │  (DB + Views)│     │  Reports  │
  │  /{year}/{mm}│     │   Azure)     │     │              │     │  2A + 2B  │
  └──────────────┘     └──────────────┘     └──────────────┘     └───────────┘
         │                    │                    │                    │
    ~25 fichiers         /inbound/           metric_values         41 rapports
    XLSX par mois        /processed/         ingestion_files       (PDF/PPTX)
    (voir Section 2)     /quarantine/        ingestion_jobs
                         /reports/

  ┌──────────────────────────────────────────────────────────────────────────┐
  │                    PIPELINE EN 3 ÉTAPES                                   │
  │                                                                           │
  │  STEP 1: ImportFilesHandler                                               │
  │    • Liste les fichiers SharePoint /{year}/{mm}/                          │
  │    • Anti-doublon : vérifie DB (GetAlreadyLoadedFileNamesAsync)           │
  │    • Skip si fichier déjà traité + inchangé (LastModified identique)      │
  │    • Re-traite si le fichier a été modifié depuis la dernière ingestion   │
  │    • Upload vers Blob /inbound/{year}/{mm}/                               │
  │    • Patch SharePoint : ProcessingStatus = "Processing"                  │
  │                                                                           │
  │  STEP 2: ParseAndValidateFilesHandler                                     │
  │    • 6 règles de validation (FOLD-001, NAME-001..004, + 6 règles métier)  │
  │    • Valide : reste dans /inbound/                                        │
  │    • Échoue : déplace vers /quarantine/ + URL SAS 7 jours                │
  │                                                                           │
  │  STEP 3: PersistFilesHandler                                              │
  │    • Valide : /inbound/ → /processed/ + IngestionFile(Processed)         │
  │    • Échoue : IngestionFile(Failed) + ValidationError[] + Email           │
  │    • Mappe chaque ligne Excel → MetricValue (via MetricValueMapper)       │
  │    • Déclenche refresh Power BI si ≥1 fichier persisté                   │
  └──────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Fichiers Source → Mapping Reports

> **Source officielle :** `Files/PARR Reports - Mapping.xlsx`  
> Colonnes du fichier : *Report Name | File Source | File/Report Name | Comments | To be Changed | Change Details*

### Règle d'or : MOD520A est la source principale

**`MOD520A__PAF_Reports_MMMYY_Non Anonymised.xlsx`** alimente les feuilles **2A.1 à 2A.10** et **2B.1 à 2B.10** — c'est le fichier central du pipeline PARR.

---

### Schedule 2A — Anonymisé (19 feuilles de calcul)

| Feuille | Titre Officiel | Source (CDSP/DDP) | Fichier Source Exact | Vue SQL |
|---|---|---|---|---|
| **2A.1** | Estimated & Check Reads | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a1_leaderboard` + `vw_2a1_distribution` |
| **2A.2** | No Meter Recorded in SP | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` + `YYYYMM_MMMYYYY_SupplyPointCounts` | `vw_2a2_no_meter` |
| **2A.3** | No Meter Recorded and data flows received | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a3_read_performance_industry` |
| **2A.4** | Shipper Transfer Read Performance | CDSP/SharePoint & DDP | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` / `Transfer Read Performance` | `vw_2a4_transfer_read` |
| **2A.5** | Read Performance | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a5_read_performance` |
| **2A.6** | Meter Read Validity Monitoring | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a6_meter_validity` |
| **2A.7** | No Read 1,2,3 or 4 — Class 1/2/3/4 | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a7_no_reads` |
| **2A.8** | AQ Corrections by Reason Code | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a8_aq_corrections` |
| **2A.9** | Standard CF AQ > 732,000 kWh | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a9_standard_cf` |
| **2A.10** | Replaced Meter Reads | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2a10_replaced_reads` |
| **2A.11a** | Sites above Class 1 threshold (not in Class 1) | CDSP/SharePoint | `EUC09_Reporting_PAC_YYYY_MM` | `vw_2a11a_euc_class1_above` |
| **2A.11b** | Sites reclassified to Class 1 (Shipper & CDSP) | CDSP/SharePoint | `EUC09_Reporting_PAC_YYYY_MM` | `vw_2a11b_euc_reclassified` |
| **2A.12a** | Class 4 Monthly read perf — % portfolio AQ | DDP | `Class 4 Read Performance` | `vw_2a12a_class4_monthly_read` |
| **2A.12b** | Class 4 Monthly read perf — % portfolio AQ (2) | DDP | `Class 4 Read Performance` | `vw_2a12b_class4_monthly_read` |
| **2A.12c** | Class 4 Annual read perf — % portfolio AQ | DDP | `Class 4 Read Performance` | `vw_2a12c_class4_annual_read` |
| **2A.13** | Breakdown of AQ overdue a Meter Reading | CDSP/SharePoint | `AQ at Risk MMM YYYY For PAFA` | `vw_2a13_aq_overdue` |
| **2A.14** | Confirmed Energy Theft — submissions & objections | CDSP/SharePoint | `Confirmed Energy Theft Claim/Withdrawal objections_P41/P106` | `vw_2a14_energy_theft` |
| **2A.15** | Sites converted PC 2/3 → PC4 (low read submission) | DDP | `Supply Points Reclassified to Class 4 (PAC)` | `vw_2a15_pc_reclassified` |
| **2A.16** | Class 2/3 Individual Read Perf vs Min % | DDP | `Supply Points with Minimum Threshold (PAC)` | `vw_2a16_min_threshold` |
| **2A.17** | IGT Must Read — Known Meter Issue flag | DDP | `IGT Must Read - PARR Reports` | `vw_2a17_igt_must_read` |
| **2A.18** | Corrective Opening Meter Reading Rejections | CDSP/SharePoint | `2B.21 Corrective Opening Meter Reading Rejections_MMM-YY` | `vw_2a18_comr_rejections` |
| **2A.19** | Class 4 Vacant Sites | DDP | `PARR Reports` (DDP) | `vw_2a19_vacant_sites` |

> ⚠️ **Note :** Le fichier mapping officiel liste 22 entrées pour 2A car certaines feuilles ont des sous-onglets (2A.11a/b, 2A.12a/b/c). Le rapport final contient bien **19 onglets** au sens du fichier Excel de destination.

---

### Schedule 2B — Non-Anonymisé (22 feuilles de calcul)

| Feuille | Titre Officiel | Source (CDSP/DDP) | Fichier Source Exact | Vue SQL |
|---|---|---|---|---|
| **2B.1** | Estimated & Check Reads | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b1_estimated_check_reads` |
| **2B.2** | No Meter Recorded in SP | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b2_no_meter` |
| **2B.3** | No Meter Recorded and data flows received | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b3_no_meter_dataflows` |
| **2B.4** | Shipper Transfer Read Performance | CDSP/SharePoint & DDP | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` / `Transfer Read Performance` | `vw_2b4_transfer_read` |
| **2B.5** | Read Performance | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b5_read_performance` |
| **2B.6** | Meter Read Validity Monitoring | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b6_meter_validity` |
| **2B.7** | No Read 1,2,3 or 4 — Class 1/2/3/4 | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b7_no_reads` |
| **2B.8** | AQ Corrections by Reason Code | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b8_aq_corrections` |
| **2B.9** | Standard CF AQ > 732,000 kWh | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b9_standard_cf` |
| **2B.10** | Replaced Meter Reads | CDSP/SharePoint | `MOD520A__PAF_Reports_MMMYY_Non Anonymised` | `vw_2b10_replaced_reads` |
| **2B.11a** | AQ Portfolio Calculation | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11a_aq_portfolio` |
| **2B.11b** | AQ Portfolio Calculation Increase | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11b_aq_portfolio_inc` |
| **2B.11c** | AQ Portfolio Calculation Decrease 12m rolling | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11c_aq_portfolio_dec` |
| **2B.11d** | AQ Portfolio Calculation by frequency (1/4/12/24/36+) | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11d_aq_portfolio_freq` |
| **2B.11e** | AQ Portfolio Calculation by Month | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11e_aq_portfolio_month` |
| **2B.11f** | AQ Portfolio Increase 12m rolling | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11f_aq_portfolio_inc_12m` |
| **2B.11g** | AQ Portfolio Decrease 12m rolling | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11g_aq_portfolio_dec_12m` |
| **2B.11h** | AQ Portfolio Calculation failure by reason code | CDSP/SharePoint | `Rpt_1364_PARR AQ report_YYYY-MM` | `vw_2b11h_aq_portfolio_reason` |
| **2B.14a** | Sites above Class 1 threshold (not in Class 1) | CDSP/SharePoint | `EUC09_Reporting_PAC_YYYY_MM` | `vw_2b14a_euc_class1_above` |
| **2B.14b** | Sites reclassified to Class 1 (Shipper & CDSP) | CDSP/SharePoint | `EUC09_Reporting_PAC_YYYY_MM` | `vw_2b14b_euc_reclassified` |
| **2B.15a** | Class 4 Monthly read perf — % portfolio AQ | DDP | `Class 4 Read Performance` | `vw_2b15a_class4_monthly_read` |
| **2B.15b** | Class 4 Monthly read perf — % portfolio AQ (2) | DDP | `Class 4 Read Performance` | `vw_2b15b_class4_monthly_read` |
| **2B.15c** | Class 4 Annual read perf — % portfolio AQ | DDP | `Class 4 Read Performance` | `vw_2b15c_class4_annual_read` |
| **2B.16** | Breakdown of AQ overdue a Meter Reading | CDSP/SharePoint | `AQ at Risk MMM YYYY For PAFA` | `vw_2b16_aq_overdue` |
| **2B.17** | Confirmed Energy Theft — submissions & objections | CDSP/SharePoint | `Confirmed Energy Theft Claim/Withdrawal objections_P41/P106` | `vw_2b17_energy_theft` |
| **2B.18** | Sites converted PC 2/3 → PC4 (low read submission) | DDP | `Supply Points Reclassified to Class 4 (PAC)` | `vw_2b18_pc_reclassified` |
| **2B.19** | Class 2/3 Individual Read Perf vs Min % | DDP | `Supply Points with Minimum Threshold (PAC)` | `vw_2b19_min_threshold` |
| **2B.20** | IGT Must Read — Known Meter Issue flag | DDP | `IGT Must Read - PARR Reports` | `vw_2b20_igt_must_read` |
| **2B.21** | Corrective Opening Meter Reading Rejections | CDSP/SharePoint | `2B.21 Corrective Opening Meter Reading Rejections_MMM-YY` | `vw_2b21_comr_rejections` |
| **2B.22** | Class 4 Vacant Sites | DDP | `PARR Reports` (DDP) | `vw_2b22_vacant_sites` |

> ⚠️ **Note :** 2B a 22 onglets dans le rapport final mais 30 entrées dans le mapping (11a-h = 8 sous-onglets comptés comme 1 groupe, 14a-b = 2, 15a-c = 3).

---

### Fichiers Source Disponibles dans `Files/Source Files/`

| Fichier Disponible | Alimente |
|---|---|
| `MOD520A__PAF_Reports_Apr26_Non Anonymised.xlsx` | **2A.1 à 2A.10** + **2B.1 à 2B.10** |
| `202604_Apr2026_SupplyPointCounts.xlsx` | 2A.2 (calcul des pourcentages) |
| `EUC09_Reporting_PAC_2026_04.xlsx` | 2A.11a, 2A.11b, 2B.14a, 2B.14b |
| `Rpt_1364_PARR AQ report_2026-04.xlsx` | **2B.11a à 2B.11h** |
| `AQ at Risk Mar 2026_For PAFA.xlsx` | 2A.13, 2B.16 |
| `Shipper Transfer Read Performance (5).xlsx` | 2A.4 (complément DDP) |
| `Read Performance by Shipper (5).xlsx` | 2A.12a/b/c — `Class 4 Read Performance` |
| `Confirmed Energy Theft Claim objections_P106.xlsx` | 2A.14, 2B.17 |
| `Confirmed Energy Theft Claim objections_P41.xlsx` | 2A.14, 2B.17 |
| `Confirmed Energy Theft Withdrawal objections_P106.xlsx` | 2A.14, 2B.17 |
| `Confirmed Energy Theft Withdrawal objections_P41.xlsx` | 2A.14, 2B.17 |
| `Supply Points and AQ with Minimum Percentage Not met (4) - PC2.xlsx` | 2A.16, 2B.19 |
| `Supply Points and AQ with Minimum Percentage Not met (5) - PC3.xlsx` | 2A.16, 2B.19 |
| `Supply Points with Minimum Percentage Requirement (4) PC2.xlsx` | 2A.15, 2B.18 |
| `Supply Points with Minimum Percentage Requirement (5) - PC3.xlsx` | 2A.15, 2B.18 |
| `Report 1 - Percentage MPRN removed from Must Read.xlsx` | 2A.17, 2B.20 (IGT) |
| `Report 1A - Sites set to Vacant within the month.xlsx` | 2A.19, 2B.22 (Vacant) |
| `Report 1B - Count of Vacant sites at the end of the month (1).xlsx` | 2A.19, 2B.22 (Vacant) |
| `Report 2 - Percentage MPRN removed from Must Read age bucket.xlsx` | 2A.17, 2B.20 |
| `Report 2 - Proportion of sites set as Vacant at the end of each Month (3).xlsx` | 2A.19, 2B.22 |
| `Report 3 - Count MPRN removed from Must Read.xlsx` | 2A.17, 2B.20 |
| `2B.21 Corrective Opening Meter Reading Rejections_Apr-26.xlsx` | **2A.18**, 2B.21 |
| `Class 3 conversion due to low read submission (AQ & Count) (2).xlsx` | Contexte 2A.15/2B.18 |
| `Class 3 conversion due to low read submission (Percentage) (2).xlsx` | Contexte 2A.15/2B.18 |

---

## 3. Ce Qui Est Implémenté

### ✅ Pipeline d'Ingestion (100%)

| Composant | Fichier | Statut |
|---|---|---|
| ImportFilesHandler | `src/PAFA.Extraction/Handlers/Pipeline/ImportFilesHandler.cs` | ✅ |
| ParseAndValidateFilesHandler | `src/PAFA.Extraction/Handlers/Pipeline/ParseAndValidateFilesHandler.cs` | ✅ |
| PersistFilesHandler | `src/PAFA.Extraction/Handlers/Pipeline/PersistFilesHandler.cs` | ✅ |
| Anti-doublon (filename + date) | `GetAlreadyLoadedFileNamesAsync` + `GetLoadedFileModificationDatesAsync` | ✅ |
| Patch SharePoint (Processing→Processed) | `IRemoteFileSource.PatchStatusAsync()` | ✅ |
| Blob Storage (MinIO + Local) | `LocalBlobStorageService` + `MinioBlobStorageService` | ✅ |
| ExcelInspectionService | `src/PAFA.Infrastructure/Parsing/ExcelInspectionService.cs` | ✅ |
| MetricValueMapper (60+ clés) | `src/PAFA.Extraction/Mappers/MetricValueMapper.cs` | ✅ |

### ✅ Schéma Base de Données (100%)

| Table | Description | Statut |
|---|---|---|
| `ingestion_jobs` | Conteneur d'une exécution pipeline | ✅ |
| `ingestion_files` | Un fichier Excel traité | ✅ |
| `metric_values` | EAV — 1 ligne = 1 shipper × 1 period × 1 metric | ✅ |
| `validation_errors` | Erreurs de validation détaillées | ✅ |
| `shippers` | Master shippers | ✅ |
| `shipper_alias` | Codes alias anonymisés (Schedule 2A) | ✅ |
| `product_classes` | PC1, PC2, PC3, PC4 | ✅ |
| `euc_bands` | Bandes EUC09 | ✅ |
| `lookup_values` | Valeurs de lookup (MRE codes, reasons, bands) | ✅ |
| `report_definitions` | Définitions des 41 rapports | ✅ |

### ✅ Vues SQL Base (Créées)

| Vue | Usage Power BI |
|---|---|
| `vw_dim_date` | Slicer temporel (toutes les feuilles) |
| `vw_dim_shipper` | Dimension shipper avec alias |
| `fact_read_performance` | Table de faits centrale |
| `v_parr_industry` | Schedule 2A — vue principale anonymisée |
| `v_parr_pac` | Schedule 2B — vue principale non-anonymisée |
| `vw_2a1_leaderboard` | 2A.1 — classement par performance |
| `vw_2a1_distribution` | 2A.1 — histogramme de distribution |
| `vw_2a2_no_meter` | 2A.2 — sites sans compteur |

### ✅ Intégration Power BI

| Composant | Statut |
|---|---|
| PowerBiDatasetRefreshService | ✅ Déclenché après ingestion |
| PowerBiBatchExportService | ✅ Export mensuel 41 rapports |
| ExportPowerBiCsvQueryHandler | ✅ Export CSV pour Power BI |
| Batch Reports Orchestrator | ✅ PDF + Excel generation |

---

## 4. Ce Qui Manque

### ❌ Vues SQL (Priorité HAUTE)

```
sql/03-views-2a-complete.sql   → À créer : 2A.4 complète, 2A.5-2A.19
sql/04-views-2b-complete.sql   → À créer : 2B.1-2B.22 (toutes)
```

### ❌ Mapping Fichier → ReportCode (Priorité HAUTE)

Le `MetricValueMapper` ne renseigne pas le champ `ReportCode` sur `MetricValue`.  
Il faut un mapping : `MOD520A → ["2A.1","2A.2","2A.5"]`, etc.

**Fichier à créer :** `src/PAFA.Extraction/Mappers/FileToReportCodeMapper.cs`

### ❌ Script d'Import Direct (Priorité MOYENNE)

Pour les fichiers locaux `Files/Source Files/` (dev/test sans SharePoint) :  
**Fichier à créer :** `tools/Import-LocalSourceFiles.ps1`

### ❌ Connexion Power BI Report Builder (Priorité MOYENNE)

Documentation et fichiers `.rds` (shared data sources) pour Report Builder :  
**À créer :** `docs/powerbi/REPORT_BUILDER_CONNECTION.md`

---

## 5. Schéma Base de Données

### Table `metric_values` — Structure Clé

```sql
-- Grain : 1 ligne = 1 shipper × 1 période × 1 metric_key × 1 product_class
CREATE TABLE metric_values (
    id              UUID PRIMARY KEY,
    reporting_period DATE NOT NULL,       -- Ex: 2026-04-01 (premier du mois)
    shipper_short_code VARCHAR(50),       -- "NGS" (réel) ou "Gitega" (alias 2A)
    shipper_id      UUID REFERENCES shippers(id),  -- NULL pour 2A anonymisé
    metric_key      VARCHAR(100) NOT NULL,  -- "read_performance_pct", "est_pct", ...
    value           NUMERIC(18,6),         -- % ou count
    text_value      VARCHAR,               -- "-" ou valeurs non-numériques
    product_class_code VARCHAR(10),        -- "PC1", "PC2", "PC3", "PC4", NULL
    report_code     VARCHAR(20),           -- "2A.1", "2B.5", etc.
    euc_code        VARCHAR(10),           -- Pour 2A.9/2A.10
    lookup_value_id INT,                   -- Pour dimensions secondaires
    ingestion_file_id UUID NOT NULL,       -- Traçabilité
    is_deleted      BOOLEAN DEFAULT FALSE
);
```

### Index Critiques pour Power BI

```sql
CREATE INDEX ix_mv_period_shipper ON metric_values(reporting_period, shipper_short_code);
CREATE INDEX ix_mv_metric_key ON metric_values(metric_key);
CREATE INDEX ix_mv_report_code ON metric_values(report_code);
CREATE INDEX ix_mv_product_class ON metric_values(product_class_code);
```

---

## 6. Comment Insérer les Fichiers

### 6.1 Via SharePoint (Production) — Flux Normal

```
1. L'utilisateur dépose les fichiers dans SharePoint : /{year}/{mm}/
   Ex: /2026/04/MOD520A__PAF_Reports_Apr26_Non Anonymised.xlsx

2. Le cron (PAFA.BatchReports) s'exécute les jours 18-21 du mois suivant :
   dotnet run --project src/PAFA.BatchReports -- --ingest --year 2026 --month 4

3. Le pipeline :
   a. Liste les fichiers SharePoint dans /2026/04/
   b. Vérifie la DB : skips les fichiers déjà traités + inchangés
   c. Télécharge vers Blob /inbound/2026/04/
   d. Valide (6 règles)
   e. Persiste dans metric_values
   f. Déclenche refresh Power BI
```

### 6.2 Via Script Local (Dev/Test) — Bypass SharePoint

Voir `tools/Import-LocalSourceFiles.ps1` (créé dans ce package).

```powershell
# Exemple : importer tous les fichiers du mois d'avril 2026
.\tools\Import-LocalSourceFiles.ps1 `
    -SourceFolder "Files\Source Files" `
    -Year 2026 `
    -Month 4 `
    -ConnectionString "Host=localhost;Port=5432;Database=pafadb;Username=pafa;Password=pafa"
```

### 6.3 Via API REST (Manuel)

```http
POST /api/ingestion/trigger
Content-Type: application/json

{
  "year": 2026,
  "month": 4,
  "triggerSource": "Manual"
}
```

### 6.4 Upload Direct vers MinIO (Dev)

```powershell
# Copier les fichiers dans le dossier MinIO /inbound/2026/04/
$files = Get-ChildItem "Files\Source Files\*.xlsx"
foreach ($f in $files) {
    # Via mc (MinIO Client)
    mc cp $f.FullName local/data/inbound/2026/04/$($f.Name)
}

# Puis déclencher le pipeline depuis STEP 2 uniquement
dotnet run --project src/PAFA.BatchReports -- --ingest --skip-download --year 2026 --month 4
```

---

## 7. Vues SQL → Power BI : Mapping Complet

### Architecture des Datasets Power BI

```
Dataset PARR_2A (Anonymisé — Public)          Dataset PARR_2B (Non-Anonymisé — PAC)
├── vw_dim_date                                ├── vw_dim_date
├── vw_dim_shipper (alias uniquement)          ├── vw_dim_shipper (noms réels)
├── v_parr_industry                            ├── v_parr_pac
├── vw_2a1_leaderboard                         ├── vw_2b1_leaderboard
├── vw_2a1_distribution                        ├── vw_2b1_distribution
├── vw_2a2_no_meter                            ├── vw_2b2_no_meter
├── vw_2a3_read_performance_industry           ├── vw_2b3_read_performance_pac
├── vw_2a4_transfer_read                       ├── vw_2b4_transfer_read
├── vw_2a5_read_performance                    ├── vw_2b5_read_performance
├── vw_2a6_meter_validity                      ├── vw_2b6_meter_validity
├── vw_2a7_no_reads                            ├── vw_2b7_no_reads
├── vw_2a8_aq_corrections                      ├── vw_2b8_aq_corrections
├── vw_2a9_standard_cf                         ├── vw_2b9_standard_cf
├── vw_2a10_replaced_reads                     ├── vw_2b10_replaced_reads
├── vw_2a11_min_pct                            ├── vw_2b11_min_pct
├── vw_2a12a_aq_class1                         ├── vw_2b12a_aq_class1
├── vw_2a12b_aq_supply                         ├── vw_2b12b_aq_supply
├── vw_2a13_aq_at_risk                         ├── vw_2b13_aq_at_risk
├── vw_2a14_class3_conv                        ├── vw_2b14_class3_conv
├── vw_2a15_theft_claims                       ├── vw_2b15_theft_claims
├── vw_2a16_theft_wd                           ├── vw_2b16_theft_wd
├── vw_2a17_mprn_must_read                     ├── vw_2b17_mprn_must_read
├── vw_2a18_vacant_in_month                    ├── vw_2b18_vacant_in_month
└── vw_2a19_vacant_eod                         ├── vw_2b19_vacant_eod
                                               ├── vw_2b20_aq_overdue
                                               ├── vw_2b21_comr_rejections
                                               └── vw_2b22_igt_issues
```

### Correspondance Feuille → Vue SQL → Métriques (basée sur mapping officiel)

| Feuille | Vue SQL | Source Fichier | Colonnes Clés (metric_key) |
|---|---|---|---|
| 2A.1 | `vw_2a1_leaderboard` + `vw_2a1_distribution` | MOD520A | `read_performance_pct`, `estimated_read_pct`, `check_read_count`, `total_site_count` |
| 2A.2 | `vw_2a2_no_meter` | MOD520A + SupplyPointCounts | `no_meter_spr_count`, `no_read_count_1yr..4yr`, `total_site_count` |
| 2A.3 | `vw_2a3_read_performance_industry` | MOD520A | `no_meter_spr_count`, `data_flows_received`, `total_site_count` |
| 2A.4 | `vw_2a4_transfer_read` | MOD520A + Transfer Read (DDP) | `transfer_read_succ_pct`, `transfer_read_total` |
| 2A.5 | `vw_2a5_read_performance` | MOD520A | `read_performance_pct`, `total_site_count`, product_class |
| 2A.6 | `vw_2a6_meter_validity` | MOD520A | `mre01026_pct..mre01030_pct`, `invalid_read_count` |
| 2A.7 | `vw_2a7_no_reads` | MOD520A | `no_read_count_1yr..4yr` avec euc_code, split 4 tabs/class |
| 2A.8 | `vw_2a8_aq_corrections` | MOD520A | `aq_correction_count` avec reason |
| 2A.9 | `vw_2a9_standard_cf` | MOD520A | `std_corr_factor_count` avec euc_code |
| 2A.10 | `vw_2a10_replaced_reads` | MOD520A | `replaced_read_count` avec euc_code |
| 2A.11a | `vw_2a11a_euc_class1_above` | **EUC09** | `class1_above_thresh_count`, `class1_above_thresh_aq_gwh` |
| 2A.11b | `vw_2a11b_euc_reclassified` | **EUC09** | `class1_reclassified_count` |
| 2A.12a | `vw_2a12a_class4_monthly_read` | **Class 4 Read Perf (DDP)** | `aq_read_perf_monthly_293k_pct` |
| 2A.12b | `vw_2a12b_class4_monthly_read` | **Class 4 Read Perf (DDP)** | `aq_read_perf_smart_amr_pct` |
| 2A.12c | `vw_2a12c_class4_annual_read` | **Class 4 Read Perf (DDP)** | `aq_read_perf_annual_pct` |
| 2A.13 | `vw_2a13_aq_overdue` | **AQ at Risk file** | `aq_at_risk_gwh`, `aq_at_risk_pct`, `aq_overdue_count` |
| 2A.14 | `vw_2a14_energy_theft` | **Energy Theft Claim/WD objections** | `energy_theft_count`, `theft_objection_count`, `theft_claim_obj_pct` |
| 2A.15 | `vw_2a15_pc_reclassified` | **Supply Points Reclassified (DDP)** | `pc2_to4_conv_count`, `class3_conv_count`, `class3_conv_pct` |
| 2A.16 | `vw_2a16_min_threshold` | **Supply Points Min Threshold (DDP)** | `min_pct_req_pct`, `min_pct_not_met_count` |
| 2A.17 | `vw_2a17_igt_must_read` | **IGT Must Read (DDP)** | `igt_known_issue_count`, `mprn_removed_pct`, `must_read_age_pct` |
| 2A.18 | `vw_2a18_comr_rejections` | **COMR Rejections file (2B.21)** | `comr_count`, `comr_rejections`, `comr_reject_recv_pct` |
| 2A.19 | `vw_2a19_vacant_sites` | **PARR Reports DDP** | `class4_vacant_sites`, `vacant_eod_count`, `vacant_proportion_age_pct` |

---

## 8. Connexion Power BI Report Builder

### Configuration de la Source de Données

**Type :** PostgreSQL via ODBC ou Npgsql  
**Driver requis :** Npgsql (installé via NuGet ou ODBC driver)

```
Serveur   : localhost (dev) ou Azure Postgres (prod)
Port      : 5432
Base      : pafadb
Schema    : public
User      : pafa_readonly
Password  : [secret Key Vault]
```

### Shared Data Source (.rds) pour Report Builder

```xml
<!-- PARR_PostgreSQL.rds -->
<RptDataSource xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               Name="PARR_PostgreSQL">
  <ConnectionProperties>
    <DataProvider>OLEDB-MD</DataProvider>
    <ConnectString>
      Server=localhost;Port=5432;Database=pafadb;User Id=pafa_readonly;Password=***;
    </ConnectString>
  </ConnectionProperties>
</RptDataSource>
```

### Dataset Query par Feuille — Exemples

**Feuille 2A.1 — Leaderboard :**
```sql
SELECT 
    shipper_alias,
    report_month,
    product_class_code,
    read_perf_pct,
    estimated_pct,
    check_read_count,
    total_sites,
    rank_in_class
FROM vw_2a1_leaderboard
WHERE report_month = @ReportMonth  -- paramètre Report Builder
ORDER BY rank_in_class;
```

**Feuille 2A.3 — Industry Read Performance :**
```sql
SELECT 
    shipper_alias,
    report_month,
    product_class_code,
    read_perf_pct,
    compliance_status,
    rank_overall
FROM v_parr_industry
WHERE report_month = @ReportMonth
ORDER BY rank_overall;
```

**Feuille 2B.5 — Read Performance PAC (non-anonymisé) :**
```sql
SELECT 
    shipper_real_name,
    report_month,
    product_class_code,
    read_perf_pct,
    compliance_status
FROM v_parr_pac
WHERE report_month = @ReportMonth
ORDER BY shipper_real_name;
```

### Paramètre Report Builder Standard

Ajouter un paramètre `@ReportMonth` de type `Text` dans chaque rapport :
- Default value : `=Format(DateAdd("m", -1, Today()), "yyyy-MM")`  
  (mois précédent automatiquement)

---

## 9. Vues SQL : Statut d'Implémentation

### Scripts SQL Disponibles

| Script | Contenu | Statut |
|---|---|---|
| `sql/01-create-tables.sql` | Toutes les tables | ✅ Complet |
| `sql/02-create-views-powerbi.sql` | vw_dim_date, vw_dim_shipper, fact_read_performance, v_parr_industry, v_parr_pac, vw_2a1_*, vw_2a2_* | ✅ Complet |
| `sql/script_final_report_2A_hiba_18-06.sql` | vw_2a1 à vw_2a9 (partielles) | ⚠️ Partiel |
| `sql/03-views-2a-complete.sql` | vw_2a10 à vw_2a19 | ✅ **CRÉÉ** (ce package) |
| `sql/04-views-2b-complete.sql` | vw_2b1 à vw_2b22 | ✅ **CRÉÉ** (ce package) |

### Ordre d'Exécution

```bash
# 1. Tables (une seule fois)
psql -U pafa -d pafadb -f sql/01-create-tables.sql

# 2. Vues de base + 2A.1/2A.2
psql -U pafa -d pafadb -f sql/02-create-views-powerbi.sql

# 3. Vues 2A complètes (2A.3 à 2A.19)
psql -U pafa -d pafadb -f sql/03-views-2a-complete.sql

# 4. Vues 2B complètes (2B.1 à 2B.22)
psql -U pafa -d pafadb -f sql/04-views-2b-complete.sql
```

---

## 10. Roadmap des Actions Immédiates

### Semaine 1 — SQL & Data

```
Jour 1 : Exécuter 01, 02, 03, 04 SQL scripts
Jour 1 : Injecter les fichiers Sources via tools/Import-LocalSourceFiles.ps1
Jour 2 : Vérifier les données dans metric_values (SELECT COUNT(*) GROUP BY report_code)
Jour 2 : Tester chaque vue : SELECT * FROM vw_2a1_leaderboard LIMIT 10;
```

### Semaine 2 — Power BI

```
Jour 3 : Connecter Power BI Desktop à PostgreSQL
Jour 3 : Importer les vues (Get Data → PostgreSQL → Import mode)
Jour 4 : Créer les relations : vw_dim_date ↔ toutes les vues (via report_month)
Jour 4 : Créer les mesures DAX de base
Jour 5 : Construire Report 2A (19 pages)
```

### Semaine 3 — Report Builder

```
Jour 6 : Installer Npgsql ODBC driver
Jour 7 : Créer la shared data source PARR_PostgreSQL.rds
Jour 7 : Créer le dataset par feuille avec @ReportMonth paramètre
Jour 8 : Tester export PDF complet 2A + 2B
Jour 9 : Publier sur Power BI Service
```

### Checklist de Validation

```
[ ] SELECT COUNT(*) FROM metric_values WHERE report_code IS NOT NULL > 0
[ ] SELECT DISTINCT report_code FROM metric_values ORDER BY 1  (41 codes)
[ ] SELECT * FROM v_parr_industry WHERE report_month = '2026-04' LIMIT 5
[ ] SELECT * FROM vw_2b21_comr_rejections WHERE report_month = '2026-04'
[ ] Power BI Dataset refresh complète sans erreur
[ ] Export PDF Schedule 2A — 19 pages générées
[ ] Export PDF Schedule 2B — 22 pages générées
```

---

*Généré automatiquement — Source : analyse du code PAFA au 2026-07-01*
