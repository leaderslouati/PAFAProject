namespace PAFA.Domain.Enums;

// ── Statut d'un rapport PARR ──────────────────────────────────────────────
public enum ReportStatus
{
    Pending    = 0,
    Generating = 1,
    Completed  = 2,
    Failed     = 3,
    Published  = 4
}

// ── Audience d'un type de rapport ────────────────────────────────────────
public enum AudienceType
{
    Industry = 0,   // Schedule 2A — anonymisé
    PAC      = 1    // Schedule 2B — non-anonymisé
}

// ── Type de seuil de performance ─────────────────────────────────────────
public enum ThresholdType
{
    Min = 0,   // Valeur doit être >= seuil (ex: PC1 Read Performance >= 97.5%)
    Max = 1    // Valeur doit être <= seuil
}

// ── Source de données d'une métrique ─────────────────────────────────────
public enum DataSource
{
    CDSP   = 0,   // Central Data Service Provider (Xoserve)
    DDP    = 1,   // Data Discovery Platform (Corella)
    AdHoc  = 2
}

// ── Statut d'un job d'ingestion ───────────────────────────────────────────
public enum IngestionJobStatus
{
    Started    = 0,
    Downloading = 1,
    Validating = 2,
    Loading    = 3,
    Completed  = 4,
    Failed     = 5,
    Partial    = 6
}

// ── Statut d'un fichier d'ingestion ──────────────────────────────────────
public enum IngestionFileStatus
{
    Downloaded = 0,
    Validating = 1,
    Valid      = 2,
    Invalid    = 3,
    Loaded     = 4,
    Failed     = 5
}

// ── Statut de validation d'un fichier ────────────────────────────────────
public enum ValidationStatus
{
    Pending = 0,
    Passed  = 1,
    Failed  = 2,
    Warning = 3
}

// ── Type de fichier source ────────────────────────────────────────────────
public enum FileType
{
    Xlsx = 0,
    Xml  = 1,
    Csv  = 2
}

// ── Rôle applicatif ──────────────────────────────────────────────────────
public enum AppRoleCode
{
    PafaAdmin  = 0,
    PafaUser   = 1,
    PacMember  = 2,
    Shipper    = 3,
    ReadOnly   = 4
}

// ── Type d'action dans l'audit log ───────────────────────────────────────
public enum AuditAction
{
    Insert   = 0,
    Update   = 1,
    Delete   = 2,
    Publish  = 3,
    Generate = 4,
    Send     = 5,
    Login    = 6
}

// ── Déclencheur d'un job ─────────────────────────────────────────────────
public enum JobTrigger
{
    Scheduler = 0,
    Manual    = 1,
    Retry     = 2
}