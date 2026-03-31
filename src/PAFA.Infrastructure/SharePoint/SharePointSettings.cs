using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.SharePoint;

/// <summary>
/// Configuration pour la connexion SharePoint Online via Microsoft Graph.
/// Implémente IFileSourceSettings pour être injecté dans le handler d'ingestion.
///
/// Structure SharePoint attendue :
///   {BaseInboundPath}/{Année}/{Mois}/   ? ex: /2025/07/MOD520A_Jul25.xlsx
///   {ProcessedPath}/{Année}/{Mois}/     ? ex: /Processed/2025/07/
///   {FailedPath}/                       ? ex: /Failed/
/// </summary>
public class SharePointSettings : IFileSourceSettings
{
    public const string SectionName = "SharePoint";

    /// <summary>GUID du tenant Azure AD.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>GUID de l'App Registration Azure AD.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Secret client (utiliser Azure Key Vault en production).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>URL du site SharePoint </summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Site ID SharePoint au format "{hostname},{siteId},{webId}".
    /// Récupérable via Graph Explorer: GET /sites/{hostname}:/{path}
    /// </summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>Drive ID (bibliothèque de documents). Vide = drive par défaut du site.</summary>
    public string DriveId { get; set; } = string.Empty;

    /// <summary>
    /// Chemin de base des fichiers entrants (racine de la structure Année/Mois).
    /// Vide = à la racine du drive. Ex: "/PARR" ? fichiers dans /PARR/2025/07/
    /// </summary>
    public string BaseInboundPath { get; set; } = string.Empty;

    /// <summary>Dossier racine destination après traitement réussi.</summary>
    public string ProcessedPath { get; set; } = "/Processed";

    /// <summary>Dossier pour les fichiers en erreur.</summary>
    public string FailedPath { get; set; } = "/Failed";

    /// <summary>Pattern de fichiers à traiter.</summary>
    public string FilePattern { get; set; } = "*.xlsx";

    /// <summary>
    /// Authorised PARR file name prefixes — mapped from appsettings.json.
    /// Backed by a List<string> for JSON binding; exposed as IReadOnlyList via the interface.
    /// </summary>
    public List<string> AllowedFilePrefixesList { get; set; } =
    [
        "MOD520A", "RPT_1364", "MOD700", "EUC09", "TRANSFER", "CLASS4AQ"
    ];

    /// <summary>
    /// Authorised file extensions (lower-case, with dot) — mapped from appsettings.json.
    /// </summary>
    public List<string> AllowedExtensionsList { get; set; } =
    [
        ".xlsx", ".xls", ".csv", ".xml"
    ];

    /// <summary>
    /// When true (default), enforces strict {BaseInboundPath}/{YYYY}/{MM} folder structure.
    /// </summary>
    public bool EnforceYearMonthFolderStructure { get; set; } = true;

    // ?? IFileSourceSettings explicit implementations ??????????????
    IReadOnlyList<string> IFileSourceSettings.AllowedFilePrefixes => AllowedFilePrefixesList;
    IReadOnlyList<string> IFileSourceSettings.AllowedExtensions   => AllowedExtensionsList;
}
