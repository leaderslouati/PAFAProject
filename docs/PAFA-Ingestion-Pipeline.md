# PAFA — Pipeline d'ingestion SharePoint ? MinIO ? BDD

## Architecture du flux

```
SharePoint Drive
??? {BaseInboundPath}/
    ??? {YYYY}/
        ??? {MM}/                   ? fichiers déposés ici par les shippers
            ??? MOD520A_Jul25.xlsx
            ??? RPT_1364_Jul25.xlsx
            ??? ...
            ??? Processed/          ? déplacés ici après succès
            ?   ??? MOD520A_Jul25.xlsx
            ??? Failed/             ? déplacés ici après échec
                ??? MOD700_Jul25.xlsx
```

---

## Structure des chemins (SharePointFileHelper)

| État           | Chemin SharePoint                                          |
|----------------|------------------------------------------------------------|
| **Inbound**    | `{BaseInboundPath}/{YYYY}/{MM}/`                           |
| **Processed**  | `{BaseInboundPath}/{YYYY}/{MM}/Processed/{fichier}`        |
| **Failed**     | `{BaseInboundPath}/{YYYY}/{MM}/Failed/{fichier}`           |

**Règle :**  
- Si le traitement est `success` ? `MoveFileAsync` vers `…/{YYYY}/{MM}/Processed/`  
- Si le traitement est `failed`  ? `MoveFileAsync` vers `…/{YYYY}/{MM}/Failed/`

---

## Étapes du pipeline

```
POST /api/sharepoint/start
?
?? 1. InitiateSharePointFilesCommandHandler
?      ?? TestConnectionAsync (SharePoint)
?      ?? ListFilesAsync(inboundPath)          ? {BaseInboundPath}/{YYYY}/{MM}/
?      ?? [Si ReprocessFailed=true]
?      ?      ?? ListFilesAsync(Processed/)  ? MoveFileAsync ? inbound
?      ?      ?? ListFilesAsync(Failed/)     ? MoveFileAsync ? inbound
?      ?? Filtre : exclure fichiers déjà "Loaded" en base
?      ?? Validation nom (NAME-001..004)
?      ?      ?? [Invalide] SafeMoveFailedAsync ? Failed/
?      ?? DownloadFileAsync + BlobStorageService.UploadAsync ? MinIO landing-zone
?      ?? Crée IngestionJob + IngestionFile en base (Status=Downloaded)
?
?? 2. SignalR ? PendingFilesDiscovered (front-end)
?
?? 3. IngestionPipelineQueue.EnqueueAsync(fileId)
?
?? 4. IngestionPipelineWorker (Background Service)
       ?? ParseFileCommandHandler
       ?? ValidateFileCommandHandler
       ?? PersistFileCommandHandler
       ?? [Succès] MoveFileAsync ? {YYYY}/{MM}/Processed/
       ?? [Échec]  MoveFileAsync ? {YYYY}/{MM}/Failed/
                   ?? SendValidationFailureNotificationHandler (email/ServiceBus)
```

---

## Pré-requis

### Infrastructure locale

```bash
# 1. Démarrer PostgreSQL + MinIO
docker compose up -d

# 2. Appliquer les migrations EF
dotnet ef database update --project src/PAFA.Infrastructure --startup-project src/PAFA.Api

# 3. Démarrer l'API
dotnet run --project src/PAFA.Api
```

### Variables d'environnement (ou appsettings.Development.json)

```json
"SharePoint": {
  "TenantId":       "<GUID-tenant>",
  "ClientId":       "<GUID-app-registration>",
  "ClientSecret":   "<secret>",
  "SiteUrl":        "https://talan0.sharepoint.com/sites/PAFA-POC",
  "SiteId":         "<talan0.sharepoint.com,siteId,webId>",
  "DriveId":        "",
  "BaseInboundPath": "",
  "FilePattern":    "*.xlsx",
  "AllowedFilePrefixesList": ["MOD520A","RPT_1364","MOD700","EUC09","TRANSFER","CLASS4AQ"],
  "AllowedExtensionsList":   [".xlsx",".xls",".csv",".xml"],
  "EnforceYearMonthFolderStructure": true
}
```

> `BaseInboundPath` vide = racine du drive SharePoint.  
> Les dossiers `Processed` et `Failed` sont créés automatiquement comme **sous-dossiers** du dossier de période.

---

## Instructions d'exécution

### Démarrage complet (local dev)

```bash
# Terminal 1 — Infrastructure
docker compose up -d

# Terminal 2 — API
cd src/PAFA.Api
dotnet run

# L'API écoute sur https://localhost:7001 / http://localhost:5001
```

### Déclenchement manuel via Swagger / curl

#### 1. Lister les fichiers disponibles (lecture seule)

```bash
curl -X GET "https://localhost:7001/api/sharepoint/pending-files?year=2025&month=7"
```

**Réponse attendue :**
```json
{
  "success": true,
  "year": 2025,
  "month": 7,
  "pendingFiles": [
    { "fileName": "MOD520A_Jul25.xlsx", "sizeBytes": 45678, "lastModified": "..." }
  ],
  "alreadyLoadedFiles": []
}
```

#### 2. Démarrer l'ingestion pour la période courante

```bash
curl -X POST "https://localhost:7001/api/sharepoint/start" \
  -H "Content-Type: application/json" \
  -d '{}'
```

#### 3. Démarrer l'ingestion pour une période spécifique

```bash
curl -X POST "https://localhost:7001/api/sharepoint/start" \
  -H "Content-Type: application/json" \
  -d '{ "year": 2025, "month": 7 }'
```

#### 4. Retraiter les fichiers Failed (reprocessFailed=true)

```bash
curl -X POST "https://localhost:7001/api/sharepoint/start" \
  -H "Content-Type: application/json" \
  -d '{ "year": 2025, "month": 7, "reprocessFailed": true }'
```

#### 5. Filtrer sur des fichiers spécifiques

```bash
curl -X POST "https://localhost:7001/api/sharepoint/start" \
  -H "Content-Type: application/json" \
  -d '{
    "year": 2025,
    "month": 7,
    "fileNameFilter": ["MOD520A_Jul25.xlsx", "RPT_1364_Jul25.xlsx"]
  }'
```

**Réponse attendue (202 Accepted) :**
```json
{
  "success": true,
  "year": 2025,
  "month": 7,
  "enqueuedCount": 2,
  "skippedCount": 0,
  "enqueuedFileIds": ["<guid1>", "<guid2>"],
  "skippedFiles": [],
  "errorMessage": null
}
```

#### 6. Suivi du statut d'un fichier

```bash
curl -X GET "https://localhost:7001/api/files/<fileId>/status"
```

#### 7. Suivi d'un job

```bash
curl -X GET "https://localhost:7001/api/ingestion/job/<jobId>"
```

---

## Déclenchement CRON (automatique)

Le `IngestionPipelineWorker` est un `BackgroundService` actif en continu.  
Le cron tourne les jours **18–21 de chaque mois** à **02h00 UTC**.

```bash
# Simulation d'un déclenchement CRON via l'API
curl -X POST "https://localhost:7001/api/sharepoint/start" \
  -H "Content-Type: application/json" \
  -d '{ "triggerMode": "Automatic" }'
```

---

## Déclenchement batch (via Docker Compose)

```bash
# Ingestion seule
docker compose run --rm pafa-batch --ingest

# Rapports seuls
docker compose run --rm pafa-batch --reports

# Ingestion + Rapports pour une période précise
docker compose run --rm pafa-batch --year 2025 --month 7 --once
```

---

## Étapes de test

### Test 1 — Connexion SharePoint

```bash
curl -X GET "https://localhost:7001/api/health"
# Vérifie SharePoint, MinIO, PostgreSQL
```

**Critère de succès :** `"status": "Healthy"`

---

### Test 2 — Listing sans traitement

1. Déposer un fichier `MOD520A_Jul25.xlsx` dans `{BaseInboundPath}/2025/07/` sur SharePoint.
2. Appeler `GET /api/sharepoint/pending-files?year=2025&month=7`
3. **Attendu :** le fichier apparaît dans `pendingFiles`, pas dans `alreadyLoadedFiles`.

---

### Test 3 — Ingestion nominale (fichier valide)

```bash
curl -X POST "https://localhost:7001/api/sharepoint/start" \
  -d '{ "year": 2025, "month": 7 }'
```

**Contrôles post-exécution :**

```sql
-- Le fichier doit être en status "Loaded"
SELECT "FileName", "Status", "ValidationStatus", "BlobPath"
FROM "IngestionFiles"
WHERE "FileName" = 'MOD520A_Jul25.xlsx';
```

Sur SharePoint, le fichier doit avoir été **déplacé** vers :
```
{BaseInboundPath}/2025/07/Processed/MOD520A_Jul25.xlsx
```

---

### Test 4 — Fichier avec nom invalide (NAME-002)

1. Déposer un fichier `INCONNU_Jul25.xlsx` dans le dossier inbound.
2. Lancer l'ingestion.
3. **Attendu :**
   - Le fichier est dans `skippedFiles` avec `ruleId: "NAME-002"`.
   - Le fichier est déplacé vers `{BaseInboundPath}/2025/07/Failed/INCONNU_Jul25.xlsx`.

---

### Test 5 — Retraitement des fichiers Failed

```bash
curl -X POST "https://localhost:7001/api/sharepoint/start" \
  -d '{ "year": 2025, "month": 7, "reprocessFailed": true }'
```

**Attendu :** Les fichiers dans `Failed/` sont déplacés vers l'inbound, puis retraités.

---

### Test 6 — Idempotence (pas de doublon)

Lancer deux fois l'ingestion pour la même période.

**Attendu :** La deuxième exécution retourne `skippedFiles` avec `ALREADY_LOADED` pour chaque fichier déjà traité — aucune duplication en base.

---

### Test 7 — Notification d'échec

1. Configurer un fichier Excel corrompu dans le dossier inbound.
2. Lancer l'ingestion.
3. **Attendu :**
   - Le fichier est dans `Failed/`.
   - Un email de notification est envoyé (configurer `Notifications.IngestionFailureRecipients` dans `appsettings.Development.json`).

---

## Règles de validation des noms de fichiers

| Règle     | Condition                                        | Sévérité |
|-----------|--------------------------------------------------|----------|
| NAME-001  | Nom vide ou null                                 | ERROR    |
| NAME-002  | Préfixe non autorisé                             | ERROR    |
| NAME-003  | Nom trop court (< 5 caractères)                  | ERROR    |
| NAME-004  | Extension non autorisée                          | ERROR    |

Préfixes autorisés : `MOD520A`, `RPT_1364`, `MOD700`, `EUC09`, `TRANSFER`, `CLASS4AQ`  
Extensions autorisées : `.xlsx`, `.xls`, `.csv`, `.xml`

---

## Statuts des entités

### IngestionFile.Status

| Statut        | Description                              |
|---------------|------------------------------------------|
| `Downloaded`  | Fichier transféré dans MinIO             |
| `Processing`  | Parse/validate en cours                  |
| `Loaded`      | Persisté en base avec succès             |
| `Failed`      | Erreur lors du traitement                |

### IngestionFile.ValidationStatus

| Statut    | Description                    |
|-----------|--------------------------------|
| `Pending` | Pas encore validé              |
| `Valid`   | Validation OK                  |
| `Invalid` | Erreurs de validation détectées|

---

## Résolution de problèmes

| Symptôme                                    | Cause probable                              | Solution                                                   |
|---------------------------------------------|---------------------------------------------|------------------------------------------------------------|
| `Connexion SharePoint impossible`            | Mauvais ClientId/Secret/TenantId            | Vérifier l'App Registration Azure AD                       |
| Fichier skippé `NAME-002`                   | Préfixe non autorisé                        | Ajouter le préfixe dans `AllowedFilePrefixesList`          |
| Fichier non trouvé dans le dossier inbound  | Mauvais `BaseInboundPath` ou période        | Vérifier l'URL SharePoint avec Graph Explorer              |
| `ALREADY_LOADED` sur tous les fichiers      | Fichiers déjà traités                       | Utiliser `reprocessFailed: true` ou changer la période     |
| MinIO inaccessible                          | Container non démarré                       | `docker compose up -d minio`                               |
| PostgreSQL inaccessible                     | Container non démarré                       | `docker compose up -d db`                                  |
