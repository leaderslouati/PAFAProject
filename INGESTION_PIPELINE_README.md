# PAFA — Pipeline d'Ingestion SharePoint → MinIO → Parse → Validate → DB

> Documentation technique basée **exclusivement** sur le code développé.
> Dernière mise à jour : avril 2026.

---

## Table des matières

1. [Vue d'ensemble du flux](#1-vue-densemble-du-flux)
2. [Déclenchement (Cron / API / Manual)](#2-déclenchement)
3. [Connexion SharePoint Online](#3-connexion-sharepoint-online)
4. [Stockage intermédiaire — MinIO (dev) / Azure Blob (prod)](#4-stockage-intermédiaire)
5. [Pré-validation : dossier et nom de fichier](#5-pré-validation--dossier-et-nom-de-fichier)
6. [Parsing multi-format (Excel, CSV, XML)](#6-parsing-multi-format)
7. [Validation métier des données](#7-validation-métier-des-données)
8. [Mapping et persistance en base](#8-mapping-et-persistance-en-base)
9. [Gestion des erreurs et archivage](#9-gestion-des-erreurs-et-archivage)
10. [Entités Domain (schéma DB)](#10-entités-domain)
11. [Infrastructure Docker / Kubernetes](#11-infrastructure-docker--kubernetes)
12. [Diagramme de séquence complet](#12-diagramme-de-séquence-complet)

---

## 1. Vue d'ensemble du flux

```
SharePoint Online (prod)         MinIO (dev)
        │                            │
        ▼                            ▼
┌─────────────────────────────────────────┐
│     IRemoteFileSource.ListFilesAsync    │  ← Liste les fichiers dans /{YYYY}/{MM}/
│     IRemoteFileSource.DownloadFileAsync │  ← Télécharge chaque fichier en byte[]
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  IBlobStorageService.UploadAsync        │  ← Sauvegarde dans landing-zone (MinIO/Local)
│  Bucket: landing-zone/{yyyy/MM}/file    │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│ FOLD-001/002 — FolderPathValidator      │  ← Vérifie la structure /{YYYY}/{MM}/
│ NAME-001..004 — FileNameValidator       │  ← Vérifie le nom du fichier (prefix, ext, mois)
└─────────────┬───────────────────────────┘
              │ Fichier valide structurellement
              ▼
┌─────────────────────────────────────────┐
│  FileParserFactory → IFileParser        │  ← Résolution par extension (.xlsx/.csv/.xml)
│  ExcelFileParser / CsvFileParser /      │
│  XmlFileParser                          │
│  → FileParseResult (List<RawDataRow>)   │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  ImportValidationService.Validate       │  ← VAL-002 à VAL-013 (règles métier)
│  → FileValidationResult                 │
│    ├─ HasBlockingErrors → REJECT        │
│    └─ Warnings only → CONTINUE          │
└─────────────┬───────────────────────────┘
              │ Lignes valides
              ▼
┌─────────────────────────────────────────┐
│  MetricValueMapper.MapToMetricValues    │  ← RawDataRow → MetricValue (une par colonne numérique)
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  UnitOfWork.SaveChangesAsync            │  ← Persist : IngestionJob + IngestionFile +
│  PostgreSQL                             │     ValidationError[] + MetricValue[]
└─────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  SharePoint : MoveFileAsync             │
│  ├─ OK  → /Processed/{YYYY}/{MM}/file   │
│  └─ KO  → /Failed/{YYYY}/{MM}/file      │
└─────────────────────────────────────────┘
```

---

## 2. Déclenchement

Le pipeline peut être déclenché de **3 manières** :

### 2.1 Cron Kubernetes (automatique)

**Fichier** : `src/PAFA.BatchReports/kubernetes-cronjob.yaml`

```yaml
# Production — jours 18-21 du mois à 02:00 UTC
schedule: "0 2 18-21 * *"
concurrencyPolicy: Forbid       # Un seul job à la fois
args: ["--ingest"]               # Mode ingestion uniquement
```

Le CronJob lance le conteneur `PAFA.BatchReports` qui exécute `Program.Main()`.

**TriggerSource** = `"CRON_AUTO"` → **JobTrigger** = `Scheduler`

### 2.2 API REST manuelle

**Fichier** : `src/PAFA.Api/Controllers/IngestionController.cs`

```
POST /api/ingest?year=2025&month=07    → TriggerSource = "MANUAL_API"
POST /api/ingest/reprocess             → TriggerSource = "MANUAL_REPROCESS"
GET  /api/ingest/schedule/status       → État de la fenêtre cron
```

- Le endpoint `POST /api/ingest` est protégé par `[Authorize(Roles = "PafaAdmin")]`
- `IIngestionScheduleService.ResolveTriggerMode()` détermine si l'appel tombe dans la fenêtre automatique (jours 18-21) ou est manuel
- En cas de **reprocess**, le nouveau job est lié au job précédent via `ParentJobId` et `RetryCount`

### 2.3 Upload direct de fichier

**Fichier** : `src/PAFA.Api/Controllers/ImportController.cs`

```
POST /api/import/upload (multipart/form-data)
```

Accepte un fichier uploadé manuellement avec `periodYear`, `periodMonth`, `sourceSystem` en paramètres de formulaire. Si `sourceSystem = "DDP"`, valide d'abord les credentials DDP avant l'ingestion.

### 2.4 Programme Batch (CLI)

**Fichier** : `src/PAFA.BatchReports/Program.cs`

```bash
dotnet run --project src/PAFA.BatchReports -- --ingest
dotnet run --project src/PAFA.BatchReports -- --ingest --year 2025 --month 7
dotnet run --project src/PAFA.BatchReports -- --reports     # Rapports seuls
dotnet run --project src/PAFA.BatchReports -- (aucun arg)   # Pipeline complet ingest + reports
```

Résolution de période : CLI args → variables d'environnement `PAFA_TargetYear`/`PAFA_TargetMonth` → mois courant UTC.

### 2.5 Fenêtre de déclenchement automatique

```
Interface : IIngestionScheduleService

ResolveTriggerMode(utcNow?)
  → TriggerMode.Automatic  si jour 18-21 du mois
  → TriggerMode.Manual     sinon

GetCurrentWindowStatus(utcNow?)
  → ScheduleWindowStatus(IsWithinWindow, WindowStartDay=18, WindowEndDay=21,
                          CurrentDay, TriggerMode, NextWindowOpenAt, CronExpression)
```

Le frontend utilise `GET /api/ingest/schedule/status` pour décider s'il affiche le bouton de déclenchement manuel.

---

## 3. Connexion SharePoint Online

### 3.1 Interface

**Fichier** : `src/PAFA.Domain/Interfaces/IRemoteFileSource.cs`

```csharp
public interface IRemoteFileSource
{
    Task<IReadOnlyList<RemoteFileEntry>> ListFilesAsync(string remotePath, string filePattern, CancellationToken ct);
    Task<byte[]> DownloadFileAsync(string remotePath, CancellationToken ct);
    Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken ct);
    Task<bool> TestConnectionAsync(CancellationToken ct);
}

public sealed record RemoteFileEntry(
    string FileName,
    string FullRemotePath,
    long SizeBytes,
    DateTime LastModified);
```

### 3.2 Implémentation SharePoint

**Fichier** : `src/PAFA.Infrastructure/SharePoint/SharePointFileSource.cs`

- **Authentification** : `ClientSecretCredential` (App-Only OAuth 2.0 via Azure AD)
- **API** : Microsoft Graph API v5 (`GraphServiceClient`)
- **Pagination** : gère automatiquement `OdataNextLink` (Graph retourne max 200 items/page)
- **Download** : pour les fichiers > 4 Mo, utilise `@microsoft.graph.downloadUrl` (téléchargement direct). Sinon, download via le endpoint Graph `/Content`
- **Move** : `PATCH` sur le `DriveItem` pour changer `ParentReference` + `Name`. Crée le dossier destination récursivement si inexistant

### 3.3 Configuration

**Fichier** : `src/PAFA.Infrastructure/SharePoint/SharePointSettings.cs`

```csharp
public class SharePointSettings : IFileSourceSettings
{
    public string TenantId { get; set; }              // GUID Azure AD
    public string ClientId { get; set; }              // App Registration ID
    public string ClientSecret { get; set; }          // Client secret (Key Vault en prod)
    public string SiteUrl { get; set; }               // URL du site SharePoint
    public string SiteId { get; set; }                // Format: "{hostname},{siteId},{webId}"
    public string DriveId { get; set; }               // Vide = drive par défaut
    public string BaseInboundPath { get; set; }       // Ex: "/PARR" → fichiers dans /PARR/2025/07/
    public string ProcessedPath { get; set; }         // "/Processed"
    public string FailedPath { get; set; }            // "/Failed"
    public string FilePattern { get; set; }           // "*.xlsx"
    public List<string> AllowedFilePrefixesList       // ["MOD520A", "RPT_1364", "MOD700", ...]
    public List<string> AllowedExtensionsList          // [".xlsx", ".xls", ".csv", ".xml"]
    public bool EnforceYearMonthFolderStructure        // true (par défaut)
}
```

### 3.4 Structure de dossier SharePoint attendue

```
{BaseInboundPath}/
  └── {YYYY}/
       └── {MM}/
            ├── MOD520A__Feb25.xlsx
            ├── RPT_1364__0225.csv
            └── ...

{ProcessedPath}/             ← Fichiers traités avec succès
  └── {YYYY}/{MM}/

{FailedPath}/                ← Fichiers en erreur
  └── {YYYY}/{MM}/
```

---

## 4. Stockage intermédiaire

### 4.1 Interface

**Fichier** : `src/PAFA.Domain/Interfaces/IBlobStorageService.cs`

```csharp
public interface IBlobStorageService
{
    Task<string> UploadAsync(string fileName, byte[] content, string container = "landing-zone", CancellationToken ct);
    Task<byte[]> DownloadAsync(string blobPath, CancellationToken ct);
    Task<bool> HealthCheckAsync(CancellationToken ct);
}
```

### 4.2 MinIO (environnement Docker)

**Fichier** : `src/PAFA.Infrastructure/Storage/MinioBlobStorageService.cs`

- Client : `Minio.MinioClient` (S3-compatible)
- Crée le bucket automatiquement si inexistant
- Chemin objet : `{container}/{yyyy/MM}/{fileName}`
- Retourne : `"landing-zone/2025/07/MOD520A__Jul25.xlsx"`

### 4.3 Local filesystem (sans Docker)

**Fichier** : `src/PAFA.Infrastructure/Storage/LocalBlobStorageService.cs`

- Écrit les fichiers dans `{LocalPath}/{container}/{yyyy/MM}/{fileName}`
- Même format de chemin retourné que MinIO pour la cohérence

### 4.4 Configuration

**Fichier** : `src/PAFA.Infrastructure/Storage/BlobStorageSettings.cs`

```csharp
public class BlobStorageSettings
{
    public string Provider { get; set; } = "Local";    // "MinIO" | "Local" | "Azure" (futur)
    public string LocalPath { get; set; } = "./storage";
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool UseSsl { get; set; } = false;
}
```

### 4.5 Docker Compose

```yaml
# docker-compose.yml
minio:
  image: minio/minio:latest
  ports: ["9000:9000", "9001:9001"]           # 9000=API, 9001=Console web
  environment:
    MINIO_ROOT_USER: minioadmin
    MINIO_ROOT_PASSWORD: minioadmin
  command: server /data --console-address ":9001"
```

Console MinIO accessible sur `http://localhost:9001`.

---

## 5. Pré-validation : dossier et nom de fichier

Avant même de parser le contenu du fichier, deux couches de validation structurelle s'appliquent.

### 5.1 Validation du dossier (FolderPathValidator)

**Fichier** : `src/PAFA.Extraction/Validations/FolderPathValidator.cs`

| Règle | Vérification | Sévérité |
|-------|-------------|----------|
| **FOLD-001** | Le chemin complet du fichier doit terminer par `/{expectedYear}/{expectedMonth:D2}` | ERROR — fichier déplacé vers `/Failed` |
| **FOLD-002** | Le chemin construit `{BaseInboundPath}/{YYYY}/{MM}` doit être structurellement valide (année 2020-2040, mois 1-12) | ERROR — pipeline arrêté avant listing des fichiers |

```csharp
// FOLD-001 — vérification par fichier
FolderPathValidator.IsValidYearMonthPath("/PARR/2025/07/file.xlsx", 2025, 7)  // → true
FolderPathValidator.IsValidYearMonthPath("/PARR/2025/file.xlsx", 2025, 7)     // → false

// FOLD-002 — vérification du chemin construit
FolderPathValidator.HasValidYearMonthStructure("/PARR/2025/07")               // → true
FolderPathValidator.HasValidYearMonthStructure("/PARR/invalid")               // → false
```

Les vérifications `FOLD-*` sont **désactivables** via `EnforceYearMonthFolderStructure = false` dans la config.

### 5.2 Validation du nom de fichier (FileNameValidator)

**Fichier** : `src/PAFA.Extraction/Validations/FileNameValidator.cs`

Convention attendue : `{PREFIX}__{MonthToken}[YY[YY]][_vN].{ext}`

Exemples valides : `MOD520A__Feb25.xlsx`, `RPT_1364__07_v2.csv`

| Règle | Vérification | Sévérité | Conséquence |
|-------|-------------|----------|-------------|
| **NAME-001** | Caractères interdits : `* / ? : < > \| " \` | ERROR | Fichier skippé |
| **NAME-002** | Préfixe inconnu (pas dans `AllowedFilePrefixes`) | ERROR | Fichier skippé |
| **NAME-003** | Token de mois illisible (ni `MMM`, ni `MM`, ni nom complet) | WARNING | Fichier traité mais flaggé |
| **NAME-004** | Extension non autorisée (pas dans `AllowedExtensions`) | ERROR | Fichier skippé |

**Constantes** : `src/PAFA.Domain/Constants/FileNamingConstants.cs`

```csharp
public static readonly char[] ProhibitedChars = ['*', '/', '?', ':', '<', '>', '|', '"', '\\'];
public const int MinYear = 2020;
public const int MaxYear = 2040;
```

**Résultat retourné** :

```csharp
public sealed record FileNameValidationResult(
    string FileName,
    bool IsValid,                              // false si au moins un ERROR
    string? DetectedPrefix,                    // "MOD520A" détecté
    string? DetectedMonthToken,                // "Feb" détecté
    IReadOnlyList<FileNameFinding> Findings);   // Liste de toutes les findings

public sealed record FileNameFinding(
    string RuleId,     // "NAME-001" .. "NAME-004"
    string Severity,   // "ERROR" | "WARNING"
    string Message);
```

### 5.3 Détection du Source System

Le handler détecte automatiquement le `SourceSystem` depuis le nom du fichier :

```csharp
private static string DetectSourceSystem(string fileName)
{
    // Contient MOD520A, RPT_1364, MOD700, EUC09 → "CDSP"
    // Contient TRANSFER, CLASS4AQ               → "DDP"
    // Défaut                                    → "CDSP"
}
```

---

## 6. Parsing multi-format

### 6.1 Architecture

**Fichier** : `src/PAFA.Infrastructure/Parsing/FileParserFactory.cs`

```
IFileParser (Domain)
   ├── ExcelFileParser  → .xlsx, .xls  (ClosedXML)
   ├── CsvFileParser    → .csv         (CsvHelper)
   └── XmlFileParser    → .xml         (System.Xml.Linq)
```

Toutes les implémentations sont injectées via DI (`IEnumerable<IFileParser>`). La factory résout par extension :

```csharp
public IFileParser GetParser(string fileName)
{
    var ext = Path.GetExtension(fileName);
    return _parsers.FirstOrDefault(p => p.CanHandle(ext))
        ?? throw new NotSupportedException($"No parser for '{ext}'");
}
```

### 6.2 Contrat de sortie

```csharp
public record RawDataRow
{
    public int RowNumber { get; init; }                      // Numéro de ligne (base 1)
    public Dictionary<string, string?> Cells { get; init; }  // Clé = header normalisé (lowercase, sans espaces)
    public string SheetName { get; init; }                   // Onglet source (vide pour CSV)
}

public record FileParseResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string FileName { get; init; }
    public string DetectedFileType { get; init; }    // "MOD520A" | "AQ_REPORT" | "PARR" | "UNKNOWN"
    public List<RawDataRow> Rows { get; init; }
    public Dictionary<string, int> RowsPerSheet { get; init; }
    public int TotalRows => Rows.Count;
}
```

### 6.3 Excel Parser (`ExcelFileParser`)

**Fichier** : `src/PAFA.Infrastructure/Parsing/ExcelFileParser.cs`

- Utilise **ClosedXML** (`XLWorkbook`)
- Lit toutes les feuilles du classeur
- Headers = ligne 1, normalisés : `"Shipper Short Code"` → `"shippershortcode"`
- Gère les types Excel : `DateTime → "yyyy-MM-dd"`, `Number → InvariantCulture string`, `Boolean → ToString()`
- Ignore les lignes entièrement vides
- Détecte le type de fichier : `MOD520A`, `AQ_REPORT`, `NO_READS`, `VACANT_SITES`, `PARR`

### 6.4 CSV Parser (`CsvFileParser`)

**Fichier** : `src/PAFA.Infrastructure/Parsing/CsvFileParser.cs`

- Utilise **CsvHelper** avec `CultureInfo.InvariantCulture`
- `MissingFieldFound = null`, `BadDataFound = null` → tolérant aux lignes incomplètes
- Headers normalisés, trimming automatique
- Ignore les lignes complètement vides

### 6.5 XML Parser (`XmlFileParser`)

**Fichier** : `src/PAFA.Infrastructure/Parsing/XmlFileParser.cs`

Supporte deux structures XML :

```xml
<!-- Structure 1 — éléments enfants comme colonnes -->
<Report>
  <Row>
    <ShipperShortCode>SSE</ShipperShortCode>
    <ReadPerformancePct>97.82</ReadPerformancePct>
  </Row>
</Report>

<!-- Structure 2 — attributs comme colonnes -->
<Report>
  <Row ShipperShortCode="SSE" ReadPerformancePct="97.82" />
</Report>
```

Les deux structures produisent le même `RawDataRow` normalisé.

---

## 7. Validation métier des données

**Fichier** : `src/PAFA.Extraction/Validations/ImportValidationService.cs`

La validation s'exécute **après** le parsing, sur les `RawDataRow`.

### 7.1 Règles implémentées

| Règle | Champ | Vérification | Sévérité | Bloquant ? |
|-------|-------|-------------|----------|------------|
| **VAL-002** | fichier entier | Fichier vide (0 ligne) | ERROR | ✅ Oui |
| **VAL-003** | `ReportingPeriod` | Champ manquant | ERROR | ✅ Oui |
| **VAL-004** | `ReportingPeriod` | Format invalide (attendu : `MMM-YY`, `YYYY-MM`, `MMM YY`, `YYYY-MM-DD`) | ERROR | ✅ Oui |
| **VAL-005** | `ShipperShortCode` | Champ manquant | ERROR | ✅ Oui |
| **VAL-011** | `ReadPerformancePct` | PC1 < 97.5% — shipper non-conforme UNC | INFO | ❌ Non |
| **VAL-013** | `SSC + Period` | Doublon dans le même fichier | ERROR | ✅ Oui (2ème occurrence rejetée) |

### 7.2 Résolution de colonnes

Le service cherche les valeurs dans les cellules avec **plusieurs aliases** :

```
"reportingperiod" OU "period" OU "month"
"shippershortcode" OU "ssc" OU "code"
"readperformancepct" OU "readperformance" OU "readperf"
"productclass" OU "pc" OU "class"
```

### 7.3 Gestion des fractions Excel

Les pourcentages Excel (stockés comme 0.975) sont automatiquement convertis :

```csharp
// Si 0 < value ≤ 1.0 → value * 100
// 0.975 → 97.5%
```

### 7.4 Résultat

```csharp
public sealed record FileValidationResult(
    bool HasBlockingErrors,                   // true si au moins 1 ERROR
    List<ValidationFinding> Findings,         // Toutes les findings
    int ValidRowCount,
    int InvalidRowCount);

public sealed record ValidationFinding(
    string RuleId,              // "VAL-003", "VAL-013"...
    string FieldName,
    string? FieldValue,
    ValidationSeverity Severity, // Error | Warning | Info
    string ErrorMessage,
    int RowNumber,
    string SheetName);
```

### 7.5 Flux de décision

```
HasBlockingErrors = true ?
  ├─ Toutes les findings sont persistées en DB (ValidationError)
  ├─ IngestionFile.Status → Failed
  ├─ IngestionJob.Status → Failed
  └─ Return UploadParrFilesResult(Success: false)

HasBlockingErrors = false ?
  ├─ Les lignes avec ERROR individuel sont exclues du mapping
  ├─ Les lignes valides sont mappées et insérées
  ├─ file.ValidationStatus → PassedWithWarnings (s'il y a des warnings) ou Passed
  └─ Return UploadParrFilesResult(Success: true)
```

---

## 8. Mapping et persistance en base

### 8.1 MetricValueMapper

**Fichier** : `src/PAFA.Extraction/Mappers/MetricValueMapper.cs`

Transforme un `RawDataRow` en **N `MetricValue`** — une par colonne numérique détectée.

**Colonnes mappées** (30 métriques) :

| Alias dans le header Excel | MetricKey en DB |
|---------------------------|----------------|
| `readperformancepct` | `read_performance_pct` |
| `estimatedreadpct` | `estimated_read_pct` |
| `transferreadsucc` | `transfer_read_succ_pct` |
| `class4aqreadpct` | `class4_aq_read_pct` |
| `class23mprpct` | `class23_mpr_pct` |
| `totalsitecount` | `total_site_count` |
| `checkreadcount` | `check_read_count` |
| `nometerspr` | `no_meter_spr_count` |
| `dataflowsreceived` | `data_flows_received` |
| `noreadcount1yr` .. `4yr` | `no_read_count_1yr` .. `4yr` |
| `aqcorrectioncount` | `aq_correction_count` |
| `energytheftcount` | `energy_theft_count` |
| `comrrejections` | `comr_rejections` |
| ... (30 au total) | ... |

**Logique** :
- Un `RawDataRow` pour le shipper "SSE" avec 10 colonnes numériques non-vides produit 10 `MetricValue`
- Les colonnes absentes, vides, ou non-numériques sont ignorées silencieusement
- Les fractions Excel (0.975) sont converties en pourcentages (97.5) et arrondies à 4 décimales

### 8.2 Flux de persistance (UploadParrFilesHandler)

**Fichier** : `src/PAFA.Extraction/Handlers/ImportFile/UploadParrFilesHandler.cs`

```
1. FileNameValidator.Validate → si ERROR → Return(Success: false) sans toucher la DB

2. Créer IngestionJob (Status = Processing)
   Créer IngestionFile (Status = Validating)
   SaveChangesAsync

3. FileParserFactory.GetParser(fileName) → parser.ParseAsync()
   → FileParseResult (List<RawDataRow>)

4. ImportValidationService.Validate(parseResult)
   → FileValidationResult

5. Persister ValidationError[] en DB (toutes les findings, ERROR + WARNING + INFO)
   → uow.IngestionFiles.AddValidationErrorsAsync(fileId, errors)

6. Si HasBlockingErrors → Fail(job, file, summary) → SaveChanges → Return

7. Mapper les lignes valides → MetricValue[]
   (exclut les lignes individuellement rejetées par VAL-xxx)
   → uow.MetricValues.AddRangeAsync(metrics)

8. Mettre à jour les statuts :
   IngestionFile.Status = Loaded
   IngestionFile.ValidationStatus = Passed | PassedWithWarnings
   IngestionFile.RowsRead / RowsValid / RowsRejected
   IngestionJob.Status = Completed
   IngestionJob.RecordsLoaded = metrics.Count

9. SaveChangesAsync → Return UploadParrFilesResult(Success: true)
```

### 8.3 Modèle MetricValue

```csharp
public class MetricValue : BaseEntity
{
    public Guid Id { get; set; }
    public DateOnly ReportingPeriod { get; set; }       // Ex: 2025-07-01
    public string ShipperShortCode { get; set; }        // Ex: "SSE"
    public string MetricKey { get; set; }               // Ex: "read_performance_pct"
    public decimal Value { get; set; }                  // Ex: 97.82
    public string? TextValue { get; set; }
    public string? ProductClassCode { get; set; }
    public Guid IngestionFileId { get; set; }           // FK → IngestionFile
}
```

---

## 9. Gestion des erreurs et archivage

### 9.1 Trois niveaux d'erreur

| Niveau | Quand | Effet |
|--------|-------|-------|
| **Fichier skippé** (FOLD/NAME rules) | Avant download ou parsing | Fichier déplacé vers `/Failed/`. Aucun enregistrement DB créé. |
| **Parsing échoué** | Format Excel/CSV/XML illisible | `IngestionFile.Status = Failed`, `IngestionJob.Status = Failed` |
| **Validation bloquante** | VAL-002/003/004/005/013 avec severity ERROR | Erreurs persistées en DB. `IngestionFile.Status = Failed`. Aucun MetricValue inséré pour ce fichier. |

### 9.2 Archivage physique sur SharePoint

Après traitement de chaque fichier :

```csharp
// Succès → déplacer vers /Processed
await _fileSource.MoveFileAsync(
    file.FullRemotePath,
    $"{ProcessedPath}/{year}/{month:D2}/{file.FileName}");

// Échec → déplacer vers /Failed (via SafeMoveFailed)
await _fileSource.MoveFileAsync(
    file.FullRemotePath,
    $"{FailedPath}/{year}/{month:D2}/{file.FileName}");
```

`SafeMoveFailed` est un wrapper try/catch — si le déplacement échoue (ex: dossier de destination inexistant), l'erreur est loggée mais ne fait **pas** crasher le pipeline pour les fichiers suivants.

### 9.3 Fonction `Fail` (Helper)

```csharp
async Task<UploadParrFilesResult> Fail(
    IngestionJob job, IngestionFile file, string err,
    int total, int valid, int rejected, CancellationToken ct)
{
    file.Status = IngestionFileStatus.Failed;
    file.ValidationStatus = ValidationStatus.Failed;
    file.RowsRead = total; file.RowsValid = valid; file.RowsRejected = rejected;
    job.Status = IngestionJobStatus.Failed;
    job.ErrorSummary = err;
    job.CompletedAt = DateTime.UtcNow;
    await _uow.SaveChangesAsync(ct);
    return new UploadParrFilesResult(false, ...);
}
```

### 9.4 Codes de retour HTTP

Le `IngestionController` retourne des codes HTTP différentiés :

| Résultat | HTTP Status |
|----------|-------------|
| Tout réussi | `200 OK` |
| Certains fichiers en erreur / skippés | `207 Multi-Status` |
| Tout échoué (`FilesImported == 0`) | `500 Internal Server Error` |

### 9.5 Résultat détaillé

```csharp
public sealed record DownloadParrFilesResult(
    bool Success,
    int FilesDownloaded,
    int FilesImported,
    int FilesFailed,
    List<string> ImportedFiles,
    List<FileError> Errors,           // [{FileName, ErrorMessage}]
    List<SkippedFileRecord> SkippedFiles,  // [{FileName, RuleId, Reason, SkippedAt}]
    string TriggerSource,             // "CRON_AUTO" | "MANUAL_API" | "MANUAL_REPROCESS"
    string TriggerMode);              // "Automatic" | "Manual"
```

### 9.6 Reprocess (retry après correction)

Via `POST /api/ingest/reprocess` :

```csharp
// 1. Cherche le dernier job pour cette période
var previousJob = await _jobRepo.GetLatestByPeriodAsync(year, month, ct);

// 2. Crée un nouveau job lié
job.ParentJobId = previousJob.Id;
job.RetryCount  = previousJob.RetryCount + 1;
job.TriggeredBy = JobTrigger.Retry;
```

### 9.7 Événements (Messaging)

**Fichier** : `src/PAFA.Messaging/Events/`

| Événement | Publié quand | Consommé par |
|-----------|-------------|-------------|
| `FileReadyEvent` | Fichier uploadé en blob | Processing service |
| `FileIngestedEvent` | Processing terminé (succès ou échec) | Reporting / notifications |
| `FileProcessedEvent` | Processing réussi | Power BI refresh, notifications |
| `ValidationFailedEvent` | Validation échouée | SignalR (alerte temps réel) |

### 9.8 SignalR (notifications temps réel)

**Fichier** : `src/PAFA.Api/Hubs/IngestionHub.cs`

Le hub expose les événements au frontend :
- `"FileDownloaded"` — fichier téléchargé et sauvegardé en blob
- `"ProcessingComplete"` — fichier traité avec succès
- `"ValidationError"` — fichier en échec de validation (alerte)

### 9.9 API de consultation des erreurs

```
GET /api/validation/{fileId}        → Erreurs de validation pour un fichier
GET /api/validation/job/{jobId}     → Résumé des erreurs pour tous les fichiers d'un job
GET /api/import/{fileId}/errors     → Alias pour les erreurs d'un fichier
```

---

## 10. Entités Domain

### 10.1 BaseEntity

```csharp
public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "SYSTEM";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;     // Soft delete
    public byte[]? RowVersion { get; set; }            // Optimistic concurrency
}
```

### 10.2 IngestionJob

```csharp
public class IngestionJob : BaseEntity
{
    public Guid Id { get; set; }
    public string JobName { get; set; }            // "PARR_2025_07"
    public DateOnly ReportingPeriod { get; set; }   // 2025-07-01
    public IngestionJobStatus Status { get; set; }  // Started → Processing → Completed/Failed
    public int? FilesExpected { get; set; }
    public int FilesDownloaded { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesFailed { get; set; }
    public long RecordsLoaded { get; set; }
    public string? ErrorSummary { get; set; }       // JSON
    public int RetryCount { get; set; }
    public JobTrigger TriggeredBy { get; set; }     // Scheduler | Manual | Api | Retry
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? ParentJobId { get; set; }          // FK pour retry

    // Navigation
    public ICollection<IngestionFile> IngestionFiles { get; set; }
    public ICollection<IngestionJob> RetryJobs { get; set; }
}
```

### 10.3 IngestionFile

```csharp
public class IngestionFile : BaseEntity
{
    public Guid Id { get; set; }
    public Guid IngestionJobId { get; set; }        // FK → IngestionJob
    public string FileName { get; set; }
    public string SourceSystem { get; set; }         // "CDSP" | "DDP" | "AD_HOC"
    public FileType FileType { get; set; }           // Xlsx | Xls | Csv | Xml
    public long? FileSizeBytes { get; set; }
    public string? BlobPath { get; set; }            // "landing-zone/2025/07/file.xlsx"
    public string? FileHash { get; set; }
    public IngestionFileStatus Status { get; set; }  // Downloaded → Validating → Loaded/Failed
    public ValidationStatus ValidationStatus { get; set; }
    public int? RowsRead { get; set; }
    public int? RowsValid { get; set; }
    public int? RowsRejected { get; set; }
    public int ErrorCount { get; set; }
    public DateTime? DownloadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // Navigation
    public ICollection<ValidationError> ValidationErrors { get; set; }
    public ICollection<MetricValue> MetricValues { get; set; }
}
```

### 10.4 ValidationError

```csharp
public class ValidationError : BaseEntity
{
    public Guid Id { get; set; }
    public Guid IngestionFileId { get; set; }        // FK → IngestionFile
    public int? LineNumber { get; set; }             // null = erreur fichier global
    public string? ColumnName { get; set; }
    public string ErrorCode { get; set; }            // "VAL-003", "VAL-013", etc.
    public string ErrorMessage { get; set; }
    public string? OriginalValue { get; set; }
    public string Severity { get; set; }             // "ERROR" | "WARNING" | "INFO"
}
```

### 10.5 Enums

```csharp
enum IngestionJobStatus   { Started, Processing, Completed, Failed, PartiallyCompleted, Cancelled }
enum IngestionFileStatus  { Downloaded, Validating, Valid, Invalid, Loaded, Failed }
enum ValidationStatus     { Pending, Passed, PassedWithWarnings, Failed }
enum FileType             { Xlsx, Xls, Csv, Xml }
enum JobTrigger           { Scheduler, Manual, Api, Retry }
enum TriggerMode          { Automatic, Manual }
```

---

## 11. Infrastructure Docker / Kubernetes

### 11.1 Docker Compose (développement)

```yaml
# docker-compose.psql.dev.yml — PostgreSQL
db:
  image: postgres:16-alpine
  ports: ["5432:5432"]
  environment:
    POSTGRES_USER: postgres
    POSTGRES_PASSWORD: postgres
    POSTGRES_DB: pafadb

# docker-compose.yml — MinIO + Batch
minio:
  image: minio/minio:latest
  ports: ["9000:9000", "9001:9001"]

pafa-batch:
  profiles: ["batch"]
  environment:
    PAFA_BlobStorage__Provider: MinIO
    PAFA_BlobStorage__Endpoint: pafa_minio:9000
```

**Lancement** :

```bash
docker network create pafa_network
docker compose up -d                               # PostgreSQL + MinIO
docker compose run --rm pafa-batch --ingest         # Ingestion manuelle
docker compose run --rm pafa-batch --year 2025 --month 7 --ingest
```

### 11.2 Kubernetes CronJob (production)

**Fichier** : `src/PAFA.BatchReports/kubernetes-cronjob.yaml`

```yaml
schedule: "0 2 1 * *"            # 1er du mois à 02:00 UTC
concurrencyPolicy: Forbid
backoffLimit: 2
activeDeadlineSeconds: 1800      # Timeout 30 min
args: ["--ingest"]
```

Les secrets (DB, SharePoint, MinIO) sont dans un Kubernetes Secret `pafa-secrets`.

### 11.3 Kubernetes CronJob (local/test)

**Fichier** : `src/PAFA.BatchReports/cronjob-local.yaml`

```yaml
schedule: "*/2 * * * *"          # Toutes les 2 minutes (test)
# schedule: "0 2 18-21 * *"      # Production PAFA
```

### 11.4 Dockerfile

**Fichier** : `src/PAFA.BatchReports/Dockerfile`

- **Build** : `mcr.microsoft.com/dotnet/sdk:9.0`
- **Runtime** : `mcr.microsoft.com/dotnet/runtime:9.0` (+ `libgdiplus` pour les rapports)
- Tourne sous l'utilisateur non-root `pafa` (uid 1000)
- `ENTRYPOINT ["dotnet", "PAFA.BatchReports.dll"]`, `CMD ["--ingest"]`

---

## 12. Diagramme de séquence complet

```
┌──────────┐   ┌──────────────┐   ┌──────────────┐   ┌───────────┐   ┌──────────┐   ┌──────┐
│Cron/API  │   │DownloadParr  │   │UploadParr    │   │IFileParser│   │Validation│   │  DB  │
│Trigger   │   │FilesHandler  │   │FilesHandler  │   │(factory)  │   │Service   │   │      │
└────┬─────┘   └──────┬───────┘   └──────┬───────┘   └─────┬─────┘   └────┬─────┘   └──┬───┘
     │                │                   │                 │              │             │
     │─ Command ─────>│                   │                 │              │             │
     │                │                   │                 │              │             │
     │                │─ FOLD-002 check ──│                 │              │             │
     │                │                   │                 │              │             │
     │                │─ TestConnection() │                 │              │             │
     │                │─ ListFilesAsync() │                 │              │             │
     │                │                   │                 │              │             │
     │                │ foreach file:     │                 │              │             │
     │                │                   │                 │              │             │
     │                │─ FOLD-001 check   │                 │              │             │
     │                │─ NAME-001..004    │                 │              │             │
     │                │  (skip si ERROR)  │                 │              │             │
     │                │                   │                 │              │             │
     │                │─ DownloadFile()   │                 │              │             │
     │                │─ UploadBlob()     │                 │              │             │
     │                │                   │                 │              │             │
     │                │─ Send(UploadParr) │                 │              │             │
     │                │                  ─┤                 │              │             │
     │                │                   │─ NAME check ───>│              │             │
     │                │                   │                 │              │             │
     │                │                   │─ Create Job ────│──────────────│────────────>│
     │                │                   │─ Create File ───│──────────────│────────────>│
     │                │                   │                 │              │             │
     │                │                   │─ GetParser() ──>│              │             │
     │                │                   │<─ parser ───────│              │             │
     │                │                   │─ ParseAsync() ─>│              │             │
     │                │                   │<─ FileParseResult              │             │
     │                │                   │                 │              │             │
     │                │                   │─ Validate() ────│─────────────>│             │
     │                │                   │<─ FileValidationResult ────────│             │
     │                │                   │                 │              │             │
     │                │                   │─ Persist ValidationError[] ────│────────────>│
     │                │                   │                 │              │             │
     │                │                   │─ (if valid) Map → MetricValue[]│             │
     │                │                   │─ Persist MetricValue[] ────────│────────────>│
     │                │                   │                 │              │             │
     │                │                   │─ Update Job/File status ───────│────────────>│
     │                │<──────────────────│                 │              │             │
     │                │                   │                 │              │             │
     │                │─ MoveFile()       │                 │              │             │
     │                │  (Processed/Failed)                 │              │             │
     │                │                   │                 │              │             │
     │<───────────────│ DownloadParrFilesResult             │              │             │
     │                │                   │                 │              │             │
```

---

## Annexe : fichiers clés

| Fichier | Rôle |
|---------|------|
| `src/PAFA.Extraction/Commands/SharePoint/Downloadparrfilescommand.cs` | Command MediatR pour l'orchestration |
| `src/PAFA.Extraction/Handlers/SharePoint_Online/DownloadParrFilesCommandHandler.cs` | Orchestrateur principal du pipeline |
| `src/PAFA.Extraction/Commands/Import/UploadParrFilesCommand.cs` | Command pour le parsing/validation/insert d'un fichier |
| `src/PAFA.Extraction/Handlers/ImportFile/UploadParrFilesHandler.cs` | Handler : parse → validate → map → persist |
| `src/PAFA.Extraction/Validations/FileNameValidator.cs` | Validation du nom de fichier (NAME-001..004) |
| `src/PAFA.Extraction/Validations/FolderPathValidator.cs` | Validation du dossier (FOLD-001, FOLD-002) |
| `src/PAFA.Extraction/Validations/ImportValidationService.cs` | Validation métier (VAL-002..013) |
| `src/PAFA.Extraction/Mappers/MetricValueMapper.cs` | RawDataRow → MetricValue[] |
| `src/PAFA.Infrastructure/Parsing/FileParserFactory.cs` | Résolution du parser par extension |
| `src/PAFA.Infrastructure/Parsing/ExcelFileParser.cs` | Parser Excel via ClosedXML |
| `src/PAFA.Infrastructure/Parsing/CsvFileParser.cs` | Parser CSV via CsvHelper |
| `src/PAFA.Infrastructure/Parsing/XmlFileParser.cs` | Parser XML via XDocument |
| `src/PAFA.Infrastructure/SharePoint/SharePointFileSource.cs` | Implémentation SharePoint Online (Graph API) |
| `src/PAFA.Infrastructure/Storage/MinioBlobStorageService.cs` | Blob storage MinIO (dev) |
| `src/PAFA.Infrastructure/Storage/LocalBlobStorageService.cs` | Blob storage filesystem (fallback) |
| `src/PAFA.Api/Controllers/IngestionController.cs` | API REST ingestion/reprocess/schedule |
| `src/PAFA.Api/Controllers/ImportController.cs` | API REST upload direct |
| `src/PAFA.Api/Controllers/ValidationController.cs` | API REST consultation erreurs |
| `src/PAFA.BatchReports/Program.cs` | Entry point du job batch |
| `src/PAFA.BatchReports/kubernetes-cronjob.yaml` | CronJob Kubernetes production |
