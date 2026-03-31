

namespace PAFA.Domain.Interfaces;  

/// <summary>
/// Contrat de configuration pour une source de fichiers distante.
/// Défini dans Domain — implémenté par SharePointSettings
/// 
/// Structure SharePoint cible :
///   {BaseInboundPath}/{Année}/{Mois}/   → ex: /PARR/2025/07/
///   {ProcessedPath}/{Année}/{Mois}/     → ex: /Processed/2025/07/
///   {FailedPath}/                       → ex: /Failed/
/// </summary>
public interface IFileSourceSettings
{
    /// <summary>
    /// Chemin de base des fichiers entrants.
    /// Ex: "" (racine) ou "/PARR".
    /// Le handler construit le chemin complet : {BaseInboundPath}/{year}/{month:D2}
    /// </summary>
    string BaseInboundPath { get; }

    /// <summary>Dossier racine de destination après traitement réussi.</summary>
    string ProcessedPath { get; }

    /// <summary>Dossier pour les fichiers en erreur.</summary>
    string FailedPath { get; }

    /// <summary>Pattern de fichiers à traiter.</summary>
    string FilePattern { get; }

    /// <summary>
    /// Authorised PARR file name prefixes.
    /// E.g. ["MOD520A", "RPT_1364", "MOD700", "EUC09", "TRANSFER", "CLASS4AQ"]
    /// Files whose prefix does not match are skipped with rule NAME-002.
    /// </summary>
    IReadOnlyList<string> AllowedFilePrefixes { get; }

    /// <summary>
    /// Authorised file extensions (lower-case, with dot).
    /// E.g. [".xlsx", ".xls", ".csv", ".xml"]
    /// Files with other extensions are skipped with rule NAME-004.
    /// </summary>
    IReadOnlyList<string> AllowedExtensions { get; }

    /// <summary>
    /// When true (default), the system enforces that files are read only from
    /// the strict {BaseInboundPath}/{YYYY}/{MM} folder hierarchy.
    /// Set to false in development/testing to relax the constraint.
    /// </summary>
    bool EnforceYearMonthFolderStructure { get; }
}
