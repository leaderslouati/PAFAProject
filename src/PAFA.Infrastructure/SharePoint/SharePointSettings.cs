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

    /// <summary>URL du site SharePoint (ex: https://talan0.sharepoint.com/sites/PAFA-POC).</summary>
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
}
