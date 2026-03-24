# ?? Étude de Faisabilité — SharePoint Online comme Source de Fichiers PARR

> **Projet :** PAFA (.NET 9)  
> **Objet :** Remplacement du connecteur SFTP par SharePoint Online (API Microsoft Graph)  
> **Demandeur :** Manager (étude / essai de connectivité — réponse à Simon)  
> **Branche :** `feature/Dashboard-and-Visualisation`

---

## 1. Synthèse Décisionnelle

L'objectif est de valider le remplacement du flux actuel de récupération des fichiers PARR/Xoserve.
Le système repose actuellement sur un serveur SFTP (atmoz/sftp Docker en POC, Xoserve en production).

Nous proposons de migrer vers **SharePoint Online**, ce qui permettrait :
- D'aligner le projet sur les standards sécurité de l'entreprise (Azure AD / M365)
- De simplifier la gestion opérationnelle pour les équipes métier (drag & drop dans SharePoint)
- D'éliminer l'infrastructure serveur SFTP (Docker, VM, port 22)

---

## 2. Flux Cible — 5 Phases

```
SharePoint Online (Microsoft 365)
?  Structure: /Shared Documents/PARR/{Année}/{Mois}/
?
?  Phase 1 — EXTRACTION
?  ?
[SharePointFileSource]  ??? Microsoft.Graph v5.x + Azure.Identity
  implémente IRemoteFileSource (anciennement ISftpFileSource)
?  bytes[]
?  
?  Phase 2 — LANDING ZONE
?  ?
[IBlobStorageService]  ?  MinIO (POC) / Azure Blob Storage (prod)
?  copie immédiate du fichier brut
?
?  Phase 3 — VALIDATION
?  ?
[ImportValidationService]  ?  règles métier (à discuter)
?  - Format fichier (xlsx, csv, xml)
?  - Structure colonnes attendues
?  - ShortCode Shipper existant
?  - Valeurs numériques cohérentes
?  - Doublons de période
?
?  Phase 4 — INGESTION
?  ?
[UploadParrFilesCommandHandler]  ?  PostgreSQL (metric_values)
?  stockage EAV des données validées
?
?  Phase 5 — ARCHIVAGE
?  ?
[SharePointFileSource.MoveFileAsync]
   ? Succès ? /Processed/{Année}/{Mois}/
   ? Échec  ? /Inbound/ (ou /Failed/)
```

---

## 3. Analyse du Code Existant — Points de Couplage Identifiés

### ? Ce qui est déjà bien découplé

| Composant | Localisation | Verdict |
|---|---|---|
| `ISftpFileSource` (interface) | `PAFA.Domain/Interfaces/` | ? **Aucune dépendance SSH.NET** — contrat propre dans le Domain |
| `UploadParrFilesCommandHandler` | `PAFA.Extraction/Handlers/ImportFile/` | ? **Zéro référence SFTP** — ne dépend que de MediatR |
| `IBlobStorageService` | `PAFA.Domain/Interfaces/` | ? Déjà abstrait — MinIO/Local switchable |
| Pipeline Parse ? Validate ? Insert | `PAFA.Extraction/` | ? Totalement indépendant de la source |

### ?? Points de couplage à traiter (CRITIQUE)

| # | Problème | Fichier | Impact |
|---|---|---|---|
| **1** | `DownloadParrFilesCommandHandler` dépend de `IOptions<SftpSettings>` | `Handlers/SFTP/DownloadParrFilesCommandHandler.cs` | Utilise `_settings.RemotePath`, `_settings.ProcessedPath`, `_settings.FailedPath`, `_settings.FilePattern`, `_settings.Host`, `_settings.Port` |
| **2** | `HealthController` dépend de `ISftpFileSource` directement | `Controllers/HealthController.cs` | Appelle `_sftp.TestConnectionAsync()` — OK mais le nom "sftp" dans `/api/health/full` |
| **3** | `SftpController` nommé "sftp" | `Controllers/SftpController.cs` | Route `api/sftp/ingest` — à renommer `api/ingest` |
| **4** | **2 points d'enregistrement DI** distincts | `PAFA.Api/Program.cs` **ET** `PAFA.BatchReports/Program.cs` | Les deux enregistrent `ISftpFileSource` ? SFTP — les deux doivent être modifiés |
| **5** | `PAFA.Extraction.csproj` a une référence directe à `PAFA.Infrastructure` | `PAFA.Extraction.csproj` | Le handler importe `using PAFA.Infrastructure.Sftp;` pour `SftpSettings` |
| **6** | L'interface s'appelle `ISftpFileSource` | `PAFA.Domain/Interfaces/` | Nom couplé au transport — devrait être `IRemoteFileSource` |

### ?? Refactoring recommandé

```
Avant (SFTP-couplé)                    Après (agnostique)
?????????????????????                  ?????????????????????
ISftpFileSource                   ?    IRemoteFileSource
SftpFileEntry                     ?    RemoteFileEntry
SftpSettings (dans Infrastructure)?    IFileSourceSettings (dans Domain)
api/sftp/ingest                   ?    api/ingest
SftpController                    ?    IngestionController
```

---

## 4. Prérequis Techniques

### 4.1 Côté Azure AD / IT (à demander)

| # | Prérequis | Responsable | Détail |
|---|---|---|---|
| 1 | **Azure AD App Registration** | IT / Tenant Admin | Créer une Application dans le portail Azure AD |
| 2 | **Client ID** (Application ID) | IT | GUID de l'app enregistrée |
| 3 | **Client Secret** ou **Certificat** | IT | Préférer certificat X.509 en production |
| 4 | **Tenant ID** | IT | GUID du tenant Azure AD |
| 5 | **Permissions Graph API** | IT | `Sites.ReadWrite.All` + `Files.ReadWrite.All` (Application, pas Delegated) |
| 6 | **Admin Consent** | Tenant Admin | "Grant admin consent" dans le portail Azure |
| 7 | **URL du site SharePoint** | Métier | Ex: `https://contoso.sharepoint.com/sites/PAFA-PARR` |
| 8 | **Site ID** SharePoint | IT | Récupérable via Graph Explorer : `GET /sites/{hostname}:/{path}` |
| 9 | **Drive ID** (bibliothèque de documents) | IT | Souvent le drive par défaut du site |
| 10 | **Structure des dossiers** | Métier | Proposition : `/{Année}/{Mois}/` (ex: `/2025/07/`) |

> **?? Point important pour Simon :**  
> L'appli PAFA a besoin que l'admin IT lui autorise deux choses :
> - **Voir le site** (`Sites.Read.All` ou `Sites.Selected`)
> - **Lire/Déplacer les fichiers** (`Files.ReadWrite.All`)
>
> C'est le réglage de sécurité standard pour un service batch automatique.
> Flux utilisé : **Client Credentials** (pas d'utilisateur interactif).

### 4.2 Côté Projet PAFA (.NET 9)

| # | Action | Fichier(s) | Effort |
|---|---|---|---|
| 1 | `dotnet add package Microsoft.Graph --version 5.68.0` | `PAFA.Infrastructure.csproj` | 5 min |
| 2 | `dotnet add package Azure.Identity --version 1.14.1` | `PAFA.Infrastructure.csproj` | 5 min |
| 3 | Créer `SharePointSettings.cs` | `Infrastructure/SharePoint/` | 15 min |
| 4 | Créer `SharePointFileSource.cs` (implémente `ISftpFileSource`) | `Infrastructure/SharePoint/` | 2h |
| 5 | Ajouter section `SharePoint` dans config | `appsettings.json` (Api + BatchReports) | 10 min |
| 6 | Modifier DI conditionnel dans `Program.cs` | **Api** ET **BatchReports** | 30 min |
| 7 | *(Optionnel)* Renommer `ISftpFileSource` ? `IRemoteFileSource` | Refactoring global | 1h |
| 8 | Extraire `IFileSourceSettings` (interface pour RemotePath/ProcessedPath) | `PAFA.Domain` | 30 min |
| 9 | Adapter `DownloadParrFilesCommandHandler` pour ne plus dépendre de `SftpSettings` | `PAFA.Extraction` | 30 min |

---

## 5. Configuration (`appsettings.json`)

### Nouvelle section à ajouter

```json
{
  "FileSource": {
    "Provider": "SharePoint"
  },
  "SharePoint": {
    "TenantId":        "<GUID-tenant>",
    "ClientId":        "<GUID-app-registration>",
    "ClientSecret":    "",
    "SiteUrl":         "https://contoso.sharepoint.com/sites/PAFA-PARR",
    "SiteId":          "<hostname>,<siteId>,<webId>",
    "DriveId":         "",
    "UploadFolder":    "/2025/07",
    "ProcessedFolder": "/Processed/2025/07",
    "FailedFolder":    "/Failed",
    "FilePattern":     "*.xlsx"
  }
}
```

> **?? Sécurité :**
> - **JAMAIS** de `ClientSecret` dans Git
> - Dev : `dotnet user-secrets set "SharePoint:ClientSecret" "xxx"`
> - Prod : Azure Key Vault ou variables d'environnement `PAFA_SharePoint__ClientSecret`

### Section de routage DI

```json
"FileSource": {
  "Provider": "SharePoint"    // "SharePoint" | "SFTP"
}
```

---

## 6. Implémentation — Squelette Complet

### 6.1 `SharePointSettings.cs`

```csharp
// src/PAFA.Infrastructure/SharePoint/SharePointSettings.cs
namespace PAFA.Infrastructure.SharePoint;

public class SharePointSettings
{
    public const string SectionName = "SharePoint";

    public string TenantId     { get; set; } = string.Empty;
    public string ClientId     { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SiteUrl      { get; set; } = string.Empty;
    public string SiteId       { get; set; } = string.Empty;
    public string DriveId      { get; set; } = string.Empty;

    public string UploadFolder    { get; set; } = "/upload";
    public string ProcessedFolder { get; set; } = "/processed";
    public string FailedFolder    { get; set; } = "/failed";
    public string FilePattern     { get; set; } = "*.xlsx";
}
```

### 6.2 `SharePointFileSource.cs`

```csharp
// src/PAFA.Infrastructure/SharePoint/SharePointFileSource.cs
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.SharePoint;

public sealed class SharePointFileSource : ISftpFileSource
{
    private readonly SharePointSettings _cfg;
    private readonly ILogger<SharePointFileSource> _log;

    public SharePointFileSource(
        IOptions<SharePointSettings> cfg,
        ILogger<SharePointFileSource> log)
    {
        _cfg = cfg.Value;
        _log = log;
    }

    private GraphServiceClient BuildClient()
    {
        var credential = new ClientSecretCredential(
            _cfg.TenantId, _cfg.ClientId, _cfg.ClientSecret);
        return new GraphServiceClient(credential);
    }

    public async Task<IReadOnlyList<SftpFileEntry>> ListFilesAsync(
        string remotePath, string filePattern = "*.xlsx", CancellationToken ct = default)
    {
        var graph = BuildClient();

        // GET /sites/{siteId}/drive/root:/{remotePath}:/children
        var children = await graph.Sites[_cfg.SiteId]
            .Drive.Root
            .ItemWithPath(remotePath)
            .Children
            .GetAsync(cancellationToken: ct);

        // ?? PAGINATION — Graph retourne max 200 items par page
        var allItems = new List<DriveItem>();
        if (children?.Value != null)
            allItems.AddRange(children.Value);

        while (children?.OdataNextLink != null)
        {
            children = await graph.Sites[_cfg.SiteId]
                .Drive.Root
                .ItemWithPath(remotePath)
                .Children
                .WithUrl(children.OdataNextLink)
                .GetAsync(cancellationToken: ct);

            if (children?.Value != null)
                allItems.AddRange(children.Value);
        }

        var result = allItems
            .Where(i => i.File != null && MatchesPattern(i.Name!, filePattern))
            .Select(i => new SftpFileEntry(
                FileName:       i.Name!,
                FullRemotePath: $"{remotePath}/{i.Name}",
                SizeBytes:      i.Size ?? 0,
                LastModified:   i.LastModifiedDateTime?.DateTime ?? DateTime.UtcNow))
            .ToList();

        _log.LogDebug("{Count} fichiers listés dans SharePoint {Path}", result.Count, remotePath);
        return result;
    }

    public async Task<byte[]> DownloadFileAsync(
        string remotePath, CancellationToken ct = default)
    {
        var graph = BuildClient();

        // Graph API : limite 4 Mo pour download direct, au-delà utiliser download URL
        var item = await graph.Sites[_cfg.SiteId]
            .Drive.Root
            .ItemWithPath(remotePath)
            .GetAsync(cancellationToken: ct);

        if (item?.Size > 4 * 1024 * 1024) // > 4 Mo
        {
            // Utiliser @microsoft.graph.downloadUrl pour gros fichiers
            var downloadUrl = item.AdditionalData
                .TryGetValue("@microsoft.graph.downloadUrl", out var url)
                ? url?.ToString() : null;

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                using var http = new HttpClient();
                var bytes = await http.GetByteArrayAsync(downloadUrl, ct);
                _log.LogDebug("Téléchargé (large) : {Path} ({Size:N0} bytes)", remotePath, bytes.Length);
                return bytes;
            }
        }

        // Fichier < 4 Mo — download direct
        var stream = await graph.Sites[_cfg.SiteId]
            .Drive.Root
            .ItemWithPath(remotePath)
            .Content
            .GetAsync(cancellationToken: ct);

        using var ms = new MemoryStream();
        await stream!.CopyToAsync(ms, ct);
        var result = ms.ToArray();

        _log.LogDebug("Téléchargé : {Path} ({Size:N0} bytes)", remotePath, result.Length);
        return result;
    }

    public async Task MoveFileAsync(
        string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        var graph = BuildClient();
        var destFolder = Path.GetDirectoryName(destinationPath)!.Replace('\\', '/');
        var destName   = Path.GetFileName(destinationPath);

        // S'assurer que le dossier destination existe (Graph le crée automatiquement
        // si on utilise le bon endpoint, mais MoveItem nécessite un ID parent)
        var destItem = await graph.Sites[_cfg.SiteId]
            .Drive.Root
            .ItemWithPath(destFolder)
            .GetAsync(cancellationToken: ct);

        await graph.Sites[_cfg.SiteId]
            .Drive.Root
            .ItemWithPath(sourcePath)
            .PatchAsync(new DriveItem
            {
                ParentReference = new ItemReference { Id = destItem!.Id },
                Name = destName
            }, cancellationToken: ct);

        _log.LogDebug("Déplacé : {Src} ? {Dst}", sourcePath, destinationPath);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var graph = BuildClient();
            var site = await graph.Sites[_cfg.SiteId].GetAsync(cancellationToken: ct);
            _log.LogInformation("SharePoint connexion OK — Site: {Name} ({Url})",
                site?.DisplayName, site?.WebUrl);
            return site != null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SharePoint connexion échouée — TenantId: {TenantId}", _cfg.TenantId);
            return false;
        }
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (fileName.StartsWith('.')) return false;
        if (pattern == "*")
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".xlsx" or ".xls" or ".csv" or ".xml";
        }
        if (pattern.StartsWith("*"))
            return fileName.EndsWith(pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase);
        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
```

### 6.3 Enregistrement DI — `Program.cs` (à appliquer dans Api ET BatchReports)

```csharp
// Remplace la section SFTP actuelle dans les deux Program.cs :

var fileSourceProvider = builder.Configuration["FileSource:Provider"] ?? "SFTP";

if (fileSourceProvider.Equals("SharePoint", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<SharePointSettings>(
        builder.Configuration.GetSection(SharePointSettings.SectionName));
    builder.Services.AddScoped<ISftpFileSource, SharePointFileSource>();
}
else
{
    builder.Services.Configure<SftpSettings>(
        builder.Configuration.GetSection(SftpSettings.SectionName));
    builder.Services.AddScoped<ISftpFileSource, SftpFileDownloader>();
}
```

---

## 7. ?? Points Manquants Identifiés — Non couverts par le brief initial

Ce sont les éléments que l'analyse du code a révélés et qui **doivent être traités** :

### 7.1 Couplage `DownloadParrFilesCommandHandler` ? `SftpSettings`

**Problème :** Le handler `DownloadParrFilesCommandHandler` dans `PAFA.Extraction` injecte directement `IOptions<SftpSettings>` pour accéder à `RemotePath`, `ProcessedPath`, `FailedPath`, `FilePattern`, `Host`, `Port`.

```csharp
// Ligne problématique dans DownloadParrFilesCommandHandler.cs :
private readonly SftpSettings _settings;
// ...
public DownloadParrFilesCommandHandler(
    // ...
    IOptions<SftpSettings> settings,    // ? couplage direct à SftpSettings
```

**Solution :** Extraire une interface `IFileSourceSettings` dans `PAFA.Domain` :

```csharp
// PAFA.Domain/Interfaces/IFileSourceSettings.cs
public interface IFileSourceSettings
{
    string RemotePath     { get; }
    string ProcessedPath  { get; }
    string FailedPath     { get; }
    string FilePattern    { get; }
}
```

`SftpSettings` et `SharePointSettings` implémentent cette interface.
Le handler injecte `IFileSourceSettings` au lieu de `SftpSettings`.

### 7.2 Double point d'enregistrement DI

Le basculement SFTP ? SharePoint doit être fait dans **les deux** :
- `src/PAFA.Api/Program.cs` (API web)
- `src/PAFA.BatchReports/Program.cs` (CronJob batch)

### 7.3 `HealthController` — check SharePoint

Le `GET /api/health/full` appelle `_sftp.TestConnectionAsync()`. Ça fonctionnera car l'interface est la même, mais le résultat affiché dit `sftp = true/false` ? à renommer en `fileSource`.

### 7.4 Pagination Graph API

L'API Microsoft Graph retourne **maximum 200 items par page**. Si le dossier SharePoint contient plus de 200 fichiers, il faut gérer `@odata.nextLink` (intégré dans le squelette §6.2).

### 7.5 Limite de taille des fichiers

- **Download direct Graph API** : limité à **4 Mo**
- Au-delà : utiliser `@microsoft.graph.downloadUrl` (URL pré-signée temporaire)
- Intégré dans le squelette §6.2

### 7.6 Structure des dossiers SharePoint (Année/Mois)

Le brief mentionne `/{Année}/{Mois}/`. Cela impacte le `remotePath` passé à `ListFilesAsync`.
Le `DownloadParrFilesCommandHandler` passe `_settings.RemotePath` comme chemin fixe.

**Options :**
1. Rendre `RemotePath` dynamique (calculé selon la période courante)
2. Lister récursivement
3. Configurer le chemin avec placeholders : `"/PARR/{Year}/{Month}"`

### 7.7 Delta Query — Optimisation future

Pour éviter de re-lister tous les fichiers à chaque exécution, Microsoft Graph propose le **Delta Query** :
```
GET /sites/{siteId}/drive/root/delta
```
Retourne uniquement les fichiers modifiés depuis le dernier appel. Utile si le volume augmente.

### 7.8 Throttling / Rate Limiting

Microsoft Graph applique un throttling (HTTP 429). Recommandé :
- Utiliser les retry-handlers intégrés au Graph SDK (activés par défaut)
- Ou ajouter `Polly` pour retry exponentiel

### 7.9 SignalR — Notifications temps réel

Le hub `IngestionHub` envoie des notifications en temps réel. Le passage SharePoint est **transparent** car les notifications sont émises par le handler d'import, pas par la source.

---

## 8. Comparaison SFTP vs SharePoint

| Critère | SFTP (SSH.NET) | SharePoint Online (Graph) |
|---|---|---|
| **Protocole** | SSH / SFTP (port 22) | HTTPS / REST (port 443) |
| **Auth** | Username + Password | OAuth 2.0 (Azure AD) — Client Credentials |
| **Firewall** | Port 22 à ouvrir | Port 443 (standard, déjà ouvert) |
| **Infra serveur** | Docker atmoz/sftp ou VM | Microsoft 365 (existant) |
| **Maintenance** | Gestion serveur SFTP | Zéro infra additionnelle |
| **Upload par métier** | Client SFTP (WinSCP, etc.) | Drag & drop dans SharePoint ? |
| **Audit / Historique** | Logs serveur | Versioning natif SharePoint + audit M365 |
| **Taille max fichier** | Illimité | 250 Go (SharePoint Online) |
| **API async .NET** | `Task.Run` + SSH.NET sync | Natif async dans Graph SDK ? |
| **Throttling** | N/A | 429 avec retry automatique |
| **Support .NET 9** | ? SSH.NET 2025.1.0 | ? Microsoft.Graph v5.x |
| **Effort migration** | N/A | ~4-5 jours (dev + IT + test) |

---

## 9. Risques & Mitigations

| # | Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|---|
| 1 | App Registration refusée par IT | Moyen | Bloquant | Anticiper la demande, fournir permissions minimales requises |
| 2 | `Sites.ReadWrite.All` jugée trop permissive | Moyen | Retardant | Proposer `Sites.Selected` (permet ciblage par site) |
| 3 | Throttling Graph API (429) | Faible | Ralentissement | Retry intégré au Graph SDK + Polly si nécessaire |
| 4 | Fichiers > 4 Mo bloquent le download direct | Moyen | Bug silencieux | Géré dans le squelette avec `@microsoft.graph.downloadUrl` |
| 5 | Client Secret committé dans Git | Élevé | Sécurité | `dotnet user-secrets` (dev) + Azure Key Vault (prod) |
| 6 | Pagination non gérée (> 200 fichiers) | Faible | Données manquantes | `@odata.nextLink` géré dans le squelette |
| 7 | Couplage `SftpSettings` dans le handler | Certain | Compile error | Extraire `IFileSourceSettings` (voir §7.1) |
| 8 | Structure dossiers différente entre SFTP et SP | Moyen | Config | Paramétrable via `appsettings.json` |

---

## 10. Packages NuGet à Ajouter

```xml
<!-- src/PAFA.Infrastructure/PAFA.Infrastructure.csproj — 2 packages à ajouter -->
<PackageReference Include="Microsoft.Graph"   Version="5.68.0" />
<PackageReference Include="Azure.Identity"    Version="1.14.1" />
```

Commandes :
```bash
cd src/PAFA.Infrastructure
dotnet add package Microsoft.Graph --version 5.68.0
dotnet add package Azure.Identity --version 1.14.1
```

---

## 11. Plan de Test End-to-End

### 11.1 Étape 0 — Test de connectivité isolé (PRIORITÉ pour répondre à Simon)

```powershell
# 1. Obtenir un token OAuth2 (Client Credentials)
$body = @{
    grant_type    = "client_credentials"
    client_id     = $env:SP_CLIENT_ID
    client_secret = $env:SP_CLIENT_SECRET
    scope         = "https://graph.microsoft.com/.default"
}
$token = Invoke-RestMethod `
    -Uri "https://login.microsoftonline.com/$env:SP_TENANT_ID/oauth2/v2.0/token" `
    -Method POST -Body $body

Write-Host "? Token obtenu — expiration: $($token.expires_in)s"

# 2. Vérifier l'accès au site SharePoint
$headers = @{ Authorization = "Bearer $($token.access_token)" }
$site = Invoke-RestMethod `
    -Uri "https://graph.microsoft.com/v1.0/sites/$env:SP_SITE_ID" `
    -Headers $headers
Write-Host "? Site accessible — $($site.displayName)"

# 3. Lister les fichiers dans le dossier upload
$files = Invoke-RestMethod `
    -Uri "https://graph.microsoft.com/v1.0/sites/$env:SP_SITE_ID/drive/root:/upload:/children" `
    -Headers $headers
$files.value | Select-Object name, size, lastModifiedDateTime | Format-Table
Write-Host "? $($files.value.Count) fichier(s) trouvé(s)"

# 4. Télécharger le premier fichier
if ($files.value.Count -gt 0) {
    $fileId = $files.value[0].id
    $content = Invoke-RestMethod `
        -Uri "https://graph.microsoft.com/v1.0/sites/$env:SP_SITE_ID/drive/items/$fileId/content" `
        -Headers $headers -OutFile "test_download.xlsx"
    Write-Host "? Fichier téléchargé: test_download.xlsx"
}
```

### 11.2 Tests unitaires (sans réseau)

| Test | Cible | Outil |
|---|---|---|
| `MatchesPattern("MOD520A.xlsx", "*.xlsx")` ? `true` | Statique | xUnit |
| `MatchesPattern(".hidden", "*.xlsx")` ? `false` | Statique | xUnit |
| `ListFilesAsync` retourne les bons fichiers filtrés | Mock GraphServiceClient | xUnit + NSubstitute |
| `DownloadFileAsync` < 4 Mo utilise Content | Mock | xUnit |
| `DownloadFileAsync` > 4 Mo utilise downloadUrl | Mock | xUnit |
| `MoveFileAsync` appelle PatchAsync avec bon parent | Mock | xUnit |
| Handler inchangé — `ISftpFileSource` mocké | `DownloadParrFilesCommandHandler` | xUnit |

### 11.3 Tests d'intégration E2E (avec Graph API réelle)

| Étape | Action | Résultat attendu |
|---|---|---|
| 1 | Configurer `appsettings.json` avec `FileSource:Provider = "SharePoint"` | App démarre sans erreur |
| 2 | `GET /api/health/full` | `{ fileSource: true }` |
| 3 | Déposer 2-3 `.xlsx` dans SharePoint `/upload` | Fichiers visibles |
| 4 | `POST /api/ingest` (ou `api/sftp/ingest`) | Fichiers listés, téléchargés, importés |
| 5 | Vérifier DB PostgreSQL `metric_values` | Lignes insérées |
| 6 | Vérifier SharePoint `/Processed/` | Fichiers déplacés |
| 7 | `POST /api/ingest` (2e appel) | `0 fichiers` — idempotent |
| 8 | Test avec fichier > 4 Mo | Download OK via downloadUrl |
| 9 | Basculer `Provider: "SFTP"` | Retour au flux SFTP — aucune régression |
| 10 | Test via `PAFA.BatchReports --ingest` (CronJob) | Ingestion réussie en batch |
| 11 | Vérifier `/Failed` après envoi de fichier corrompu | Fichier déplacé dans Failed |
| 12 | Vérifier logs SharePoint pour audit | Logs des accès et modifications |
| 13 | Vérifier historique des versions SharePoint | Versions antérieures disponibles |
| 14 | Vérifier notification SignalR temps réel | Notification à l'upload |
| 15 | Vérifier réception du email de fin de traitement | Email envoyé à l'admin |
| 16 | Vérifier entry dans l'historique des traitements (table dédiée) | Entry avec timestamp et statut |
| 17 | Vérifier clé d'audit dans les fichiers traités | Clé présente et correcte |
| 18 | Vérifier règles de rétention appliquées | Fichiers archivée selon la politique |
| 19 | Vérifier performance sous charge (500 fichiers en 10s) | Pas de dégradation significative |
| 20 | Vérifier récupération après échec (redémarrage PAFA) | Pas de fichiers en double, intégrité des données |

---

## 12. Résumé — Ce qu'il manquait dans le brief initial

| Sujet | Dans le brief | Ajouté par cette étude |
|---|---|---|
| Flux cible 5 phases | ? Décrit | ? Mappé sur les classes existantes |
| Prérequis Azure AD | ? Décrit | ? Complété (Site ID, Drive ID, structure dossiers) |
| Auth Client Credentials | ? Décrit | ? Confirmé |
| **Couplage `SftpSettings` dans le handler** | ? Non mentionné | ?? **CRITIQUE** — nécessite refactoring |
| **Double DI (Api + BatchReports)** | ? Non mentionné | ?? **CRITIQUE** — deux `Program.cs` à modifier |
| **Pagination Graph (> 200 fichiers)** | ? Non mentionné | ? Géré dans le squelette |
| **Limite 4 Mo download direct** | ? Non mentionné | ? Géré dans le squelette |
| **Structure dossiers Année/Mois** | ? Mentionné | ?? Impact sur `RemotePath` dynamique à clarifier |
| **HealthController** | ? Non mentionné | ? Fonctionne mais nom à adapter |
| **SignalR / IngestionHub** | ? Non mentionné | ? Transparent — aucun impact |
| **Delta Query (optimisation)** | ? Non mentionné | ?? Recommandé en phase 2 |
| **Throttling Graph** | ? Non mentionné | ? Géré par Graph SDK nativement |
| Script test PowerShell | ? Non mentionné | ? Fourni — **priorité pour répondre à Simon** |
| Renommage interface `ISftpFileSource` | ? Non mentionné | ?? Recommandé (optionnel) |

---

## 13. Checklist de Livraison

### Phase 1 — POC Connectivité (réponse à Simon)
- [ ] App Registration Azure AD obtenue
- [ ] Client ID + Secret reçus
- [ ] Permissions Graph accordées + admin consent
- [ ] URL site SharePoint confirmée
- [ ] Script PowerShell §11.1 exécuté avec succès
- [ ] **Réponse à Simon : GO / NO-GO**

### Phase 2 — Implémentation
- [ ] Packages NuGet ajoutés (`Microsoft.Graph`, `Azure.Identity`)
- [ ] `SharePointSettings.cs` créé
- [ ] `SharePointFileSource.cs` créé et compilé
- [ ] `IFileSourceSettings` extrait dans `PAFA.Domain`
- [ ] `DownloadParrFilesCommandHandler` refactoré (utilise `IFileSourceSettings`)
- [ ] DI conditionnel dans `PAFA.Api/Program.cs`
- [ ] DI conditionnel dans `PAFA.BatchReports/Program.cs`
- [ ] `appsettings.json` mis à jour (Api + BatchReports)
- [ ] Secrets via `dotnet user-secrets`
- [ ] `.gitignore` vérifié — pas de secrets

### Phase 3 — Tests
- [ ] Tests unitaires (mock)
- [ ] Test E2E avec SharePoint réel
- [ ] Test idempotence (double appel)
- [ ] Test gros fichier (> 4 Mo)
- [ ] Test basculement retour SFTP (régression)
- [ ] Test via `PAFA.BatchReports --ingest` (CronJob)

### Phase 4 — Documentation
- [ ] `SYNTHESE_CABLAGE.md` mis à jour
- [ ] `DEMO_GUIDE.md` mis à jour
- [ ] Ce document finalisé

---

## 14. Estimation Effort Révisée

| Tâche | Estimation |
|---|---|
| Configuration Azure AD (IT) | 0.5 jour |
| Script PowerShell connectivité (réponse à Simon) | 0.5 jour |
| Développement `SharePointFileSource` | 1 jour |
| Refactoring `IFileSourceSettings` + handler | 0.5 jour |
| Configuration DI + appsettings (Api + BatchReports) | 0.5 jour |
| Tests unitaires | 0.5 jour |
| Tests d'intégration end-to-end | 1 jour |
| Documentation | 0.5 jour |
| **Total** | **~5 jours** |

---

*Document rédigé pour présentation au manager — étude complète de faisabilité SharePoint Online, incluant l'analyse des couplages du code existant.*
