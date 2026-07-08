# ═══════════════════════════════════════════════════════════════════════════════
# Import-LocalSourceFiles.ps1
# 
# Insère directement les fichiers XLSX locaux (Files/Source Files/)
# dans le pipeline PAFA sans passer par SharePoint.
#
# Usage :
#   .\tools\Import-LocalSourceFiles.ps1 -Year 2026 -Month 4
#   .\tools\Import-LocalSourceFiles.ps1 -Year 2026 -Month 4 -DryRun
#   .\tools\Import-LocalSourceFiles.ps1 -SourceFolder "C:\autre\path" -Year 2026 -Month 4
#
# Prérequis :
#   - PostgreSQL accessible (variable d'env ou paramètre -ConnectionString)
#   - MinIO ou dossier local Blob configuré dans appsettings.Development.json
#   - dotnet CLI installé
# ═══════════════════════════════════════════════════════════════════════════════
[CmdletBinding()]
param(
    [string]$SourceFolder   = "$PSScriptRoot\..\Files\Source Files",
    [int]$Year              = (Get-Date).Year,
    [int]$Month             = (Get-Date).Month,
    [string]$ConnectionString = $env:PAFA_DB_CONN,
    [string]$BlobLocalPath  = "$PSScriptRoot\..\docker\blob-data",
    [switch]$DryRun,
    [switch]$Verbose
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Couleurs ──────────────────────────────────────────────────────────────────
function Write-Step  { param($msg) Write-Host "[STEP ] $msg" -ForegroundColor Cyan   }
function Write-Ok    { param($msg) Write-Host "[ OK  ] $msg" -ForegroundColor Green  }
function Write-Warn  { param($msg) Write-Host "[ WARN] $msg" -ForegroundColor Yellow }
function Write-Err   { param($msg) Write-Host "[ ERR ] $msg" -ForegroundColor Red    }
function Write-Info  { param($msg) Write-Host "[INFO ] $msg" -ForegroundColor Gray   }

# ── Mapping officiel (source : Files/PARR Reports - Mapping.xlsx) ─────────────
# Clé   = préfixe du nom de fichier (insensible à la casse)
# Valeur = @{ ReportCodes = string[]; Source = "CDSP"|"DDP" }
$FileReportMapping = @{
    # Source principale — 2A.1-2A.10 et 2B.1-2B.10 (CDSP/SharePoint)
    "MOD520A"         = @{ ReportCodes = @("2A.1","2A.2","2A.3","2A.4","2A.5","2A.6","2A.7","2A.8","2A.9","2A.10",
                                           "2B.1","2B.2","2B.3","2B.4","2B.5","2B.6","2B.7","2B.8","2B.9","2B.10");
                            Source = "CDSP" }
    # Supply Point Counts — complément pour 2A.2 (calcul des %)
    "202604"          = @{ ReportCodes = @("2A.2_COUNTS"); Source = "CDSP" }
    "SupplyPointCount"= @{ ReportCodes = @("2A.2_COUNTS"); Source = "CDSP" }
    # EUC09 — 2A.11a/b et 2B.14a/b
    "EUC09"           = @{ ReportCodes = @("2A.11a","2A.11b","2B.14a","2B.14b"); Source = "CDSP" }
    # RPT_1364 — 2B.11a à 2B.11h (AQ Portfolio)
    "RPT_1364"        = @{ ReportCodes = @("2B.11a","2B.11b","2B.11c","2B.11d","2B.11e","2B.11f","2B.11g","2B.11h");
                            Source = "CDSP" }
    "Rpt_1364"        = @{ ReportCodes = @("2B.11a","2B.11b","2B.11c","2B.11d","2B.11e","2B.11f","2B.11g","2B.11h");
                            Source = "CDSP" }
    # AQ at Risk — 2A.13 et 2B.16
    "AQ at Risk"      = @{ ReportCodes = @("2A.13","2B.16"); Source = "CDSP" }
    # Shipper Transfer Read — complément 2A.4/2B.4 (DDP)
    "Shipper Transfer Read" = @{ ReportCodes = @("2A.4","2B.4"); Source = "DDP" }
    "Transfer Read"   = @{ ReportCodes = @("2A.4","2B.4"); Source = "DDP" }
    # Read Performance — 2A.12a/b/c, 2B.15a/b/c (Class 4, DDP)
    "Read Performance by Shipper" = @{ ReportCodes = @("2A.12a","2A.12b","2A.12c","2B.15a","2B.15b","2B.15c"); Source = "DDP" }
    "Class 4 Read"    = @{ ReportCodes = @("2A.12a","2A.12b","2A.12c","2B.15a","2B.15b","2B.15c"); Source = "DDP" }
    # Energy Theft — 2A.14, 2B.17
    "Confirmed Energy Theft" = @{ ReportCodes = @("2A.14","2B.17"); Source = "CDSP" }
    # Supply Points Min — 2A.16, 2B.19 (DDP)
    "Supply Points and AQ with Minimum" = @{ ReportCodes = @("2A.16","2B.19"); Source = "DDP" }
    "Supply Points with Minimum"        = @{ ReportCodes = @("2A.15","2B.18"); Source = "DDP" }
    # IGT Must Read — 2A.17, 2B.20 (DDP)
    "Report 1 -"      = @{ ReportCodes = @("2A.17","2B.20"); Source = "DDP" }
    "Report 2 -"      = @{ ReportCodes = @("2A.17","2B.20"); Source = "DDP" }
    "Report 3 -"      = @{ ReportCodes = @("2A.17","2B.20"); Source = "DDP" }
    "Report 1A -"     = @{ ReportCodes = @("2A.19","2B.22"); Source = "DDP" }
    "Report 1B -"     = @{ ReportCodes = @("2A.19","2B.22"); Source = "DDP" }
    # COMR Rejections — 2A.18, 2B.21
    "2B.21 Corrective" = @{ ReportCodes = @("2A.18","2B.21"); Source = "CDSP" }
    "Corrective Opening" = @{ ReportCodes = @("2A.18","2B.21"); Source = "CDSP" }
    # Class 3 conversion — contexte 2A.15/2B.18
    "Class 3 conversion" = @{ ReportCodes = @("2A.15","2B.18"); Source = "DDP" }
}

function Get-ReportMapping {
    param([string]$FileName)
    foreach ($key in ($FileReportMapping.Keys | Sort-Object { $_.Length } -Descending)) {
        if ($FileName -like "*$key*") {
            return $FileReportMapping[$key]
        }
    }
    return $null
}

# ── Validation ────────────────────────────────────────────────────────────────
Write-Step "Validation des paramètres"

$sourceFolder = Resolve-Path $SourceFolder -ErrorAction SilentlyContinue
if (-not $sourceFolder) {
    Write-Err "Dossier source introuvable : $SourceFolder"
    exit 1
}

$files = Get-ChildItem -Path $sourceFolder -Filter "*.xlsx" -File |
         Where-Object { $_.Name -notmatch "^\~" }  # skip temp files

Write-Ok "Dossier source     : $sourceFolder"
Write-Ok "Période            : $Year-$('{0:D2}' -f $Month)"
Write-Ok "Fichiers XLSX      : $($files.Count)"
if ($DryRun) { Write-Warn "MODE DRY-RUN — aucune écriture en base ou blob" }

# ── Créer le dossier blob local si nécessaire ─────────────────────────────────
$blobInboundPath = Join-Path $BlobLocalPath "inbound\$Year\$('{0:D2}' -f $Month)"
if (-not $DryRun) {
    New-Item -ItemType Directory -Path $blobInboundPath -Force | Out-Null
    Write-Ok "Blob inbound       : $blobInboundPath"
}

# ── Traitement de chaque fichier ─────────────────────────────────────────────
Write-Step "Copie des fichiers vers Blob local /inbound/$Year/$('{0:D2}' -f $Month)/"

$summary = @()
foreach ($file in $files) {
    $mapping = Get-ReportMapping -FileName $file.Name
    
    if (-not $mapping) {
        Write-Warn "  Pas de mapping trouvé pour : $($file.Name)"
        $summary += [PSCustomObject]@{
            FileName    = $file.Name
            Status      = "NO_MAPPING"
            ReportCodes = ""
            Source      = ""
        }
        continue
    }
    
    $codes = $mapping.ReportCodes -join ", "
    Write-Info "  $($file.Name)"
    Write-Info "    → Report codes : $codes  [Source: $($mapping.Source)]"
    
    if (-not $DryRun) {
        $destPath = Join-Path $blobInboundPath $file.Name
        Copy-Item -Path $file.FullName -Destination $destPath -Force
    }
    
    $summary += [PSCustomObject]@{
        FileName    = $file.Name
        Status      = if ($DryRun) { "DRY_RUN" } else { "COPIED" }
        ReportCodes = $codes
        Source      = $mapping.Source
    }
}

# ── Résumé ────────────────────────────────────────────────────────────────────
Write-Step "Résumé du mapping fichiers → reports"
$summary | Format-Table FileName, Status, ReportCodes, Source -AutoSize

$copied   = ($summary | Where-Object Status -eq "COPIED").Count
$noMap    = ($summary | Where-Object Status -eq "NO_MAPPING").Count
$dryCount = ($summary | Where-Object Status -eq "DRY_RUN").Count

Write-Ok "Fichiers copiés    : $copied"
if ($dryCount -gt 0) { Write-Info "Dry-run simulés    : $dryCount" }
if ($noMap   -gt 0) { Write-Warn "Sans mapping       : $noMap (seront ignorés par le pipeline)" }

# ── Déclenchement du pipeline (si pas dry-run) ───────────────────────────────
if (-not $DryRun -and $copied -gt 0) {
    Write-Step "Déclenchement du pipeline PAFA --ingest"
    
    $dotnetArgs = @(
        "run",
        "--project", (Join-Path $PSScriptRoot "..\src\PAFA.BatchReports"),
        "--",
        "--ingest",
        "--year", $Year,
        "--month", $Month
    )
    
    Write-Info "Commande : dotnet $($dotnetArgs -join ' ')"
    
    try {
        & dotnet @dotnetArgs
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "Pipeline terminé avec succès"
        } else {
            Write-Err "Pipeline terminé avec le code d'erreur : $LASTEXITCODE"
        }
    }
    catch {
        Write-Err "Erreur lors du lancement du pipeline : $_"
        Write-Warn "Les fichiers sont copiés dans $blobInboundPath"
        Write-Warn "Vous pouvez relancer manuellement : dotnet run --project src/PAFA.BatchReports -- --ingest --year $Year --month $Month"
    }
}
elseif ($DryRun) {
    Write-Warn ""
    Write-Warn "Pour exécuter réellement :"
    Write-Warn "  .\tools\Import-LocalSourceFiles.ps1 -Year $Year -Month $Month"
}
else {
    Write-Warn "Aucun fichier copié — pipeline non déclenché."
}

Write-Ok "Import-LocalSourceFiles.ps1 terminé."
