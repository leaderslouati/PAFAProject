# Opérations — Export mensuel des rapports Power BI (PAFA)

Ce document décrit comment opérer le mécanisme d'export mensuel des 41 rapports Power BI : gestion des cron, exécution manuelle, vérifications et bonnes pratiques.

Important : par défaut dans ce dépôt, le HostedService intégré est désactivé (`PowerBiBatchExport:IsEnabled = false`) et la planification se fait via Kubernetes CronJobs (recommandé en production).

## Prérequis
- Accès réseau vers `https://api.powerbi.com`
- Secrets Power BI (`PowerBi:ClientSecret`) disponibles via Key Vault ou variables d'environnement
- Accès au cluster Kubernetes (namespace `pafa`) si vous utilisez les CronJobs
- Accès à la base Postgres et au Blob Storage (MinIO/Azure)

## Fichiers clés
- `src/PAFA.Api/BackgroundServices/MonthlyReportExportWorker.cs` — HostedService (désactivé par défaut)
- `src/PAFA.Api/appsettings.json` — configuration `PowerBiBatchExport` (clé `IsEnabled`)
- `src/PAFA.BatchReports/kubernetes-cronjob.yaml` — CronJob Kubernetes pour exécution planifiée

## Vérifier l'état actuel

- Vérifier la configuration du worker dans l'API :
```powershell
# Affiche la valeur effective dans appsettings (local)
Get-Content src/PAFA.Api/appsettings.json | Select-String -Pattern 'PowerBiBatchExport' -Context 0,5
```

- Vérifier si le HostedService est enregistré (logs API) :
```powershell
dotnet run --project src/PAFA.Api/PAFA.Api.csproj
# Chercher dans les logs : "MonthlyReportExportWorker started" ou "Next batch export scheduled"
```

- Vérifier le CronJob Kubernetes :
```bash
kubectl get cronjob -n pafa
kubectl describe cronjob <cronjob-name> -n pafa
```

## Désactiver / Réactiver le HostedService (API)

- Désactiver (recommandé en prod si K8s gère la planif) :
  - `PowerBiBatchExport:IsEnabled = false` (déjà en place).
  - En déploiement, préférer la variable d'environnement :
```text
PAFA_PowerBiBatchExport__IsEnabled=false
```

- Réactiver (dev/test uniquement) :
  - Définir `PAFA_PowerBiBatchExport__IsEnabled=true` dans les variables d'env du host et redémarrer l'API.

## Gérer les CronJobs Kubernetes (conserver les 2 cronjobs)

- Appliquer / mettre à jour le CronJob (existant dans `src/PAFA.BatchReports/kubernetes-cronjob.yaml`) :
```bash
kubectl apply -f src/PAFA.BatchReports/kubernetes-cronjob.yaml -n pafa
```

- Suspendre temporairement le CronJob (ne supprime pas la configuration) :
```bash
kubectl patch cronjob <cronjob-name> -n pafa -p '{"spec":{"suspend":true}}' --type=merge
```

- Réactiver :
```bash
kubectl patch cronjob <cronjob-name> -n pafa -p '{"spec":{"suspend":false}}' --type=merge
```

- Déclencher immédiatement une exécution du CronJob :
```bash
kubectl create job --from=cronjob/<cronjob-name> <manual-job-$(date +%s)> -n pafa
```

## Exécution manuelle locale (test)

Option A — exécuter le projet batch (one-shot) localement :
```powershell
cd src/PAFA.BatchReports
dotnet run --project PAFA.BatchReports.csproj -- --reports
```
Ce mode exécute le générateur de rapports tel que défini dans `PAFA.BatchReports` (utile pour tests hors cluster).

Option B — exécuter l'API localement puis déclencher via Cron K8s (ou endpoint admin si vous l'ajoutez).

## Vérifier les résultats

- Blob Storage : vérifier le container configuré (`reports` par défaut) pour les fichiers `PAFA_<Schedule>_<YYYY_MM>.pdf`.
  - MinIO UI ou Azure Storage Explorer selon votre configuration.

- Base de données (Postgres) : table `reports` → colonnes `FilePath_PDF`, `GeneratedAt`, `Status`.

- Logs :
  - API container logs : `kubectl logs deployment/<api-deployment> -n pafa --follow`
  - Job pod logs : `kubectl logs job/<job-name> -n pafa --follow`

## Forcer une relance / dépannage

- Si un dataset Power BI a besoin d'être rechargé manuellement, utilisez l'endpoint Power BI ou relancer le job CronJob après correction.
- Si le HostedService était activé par erreur et que vous voyez des duplications : désactivez `IsEnabled` et suspendre le CronJob, vérifier les fichiers déjà uploadés et nettoyer si nécessaire.

## Variables d'environnement utiles

- `PAFA_PowerBiBatchExport__IsEnabled` (bool) — active/désactive HostedService
- `PAFA_PowerBi__ClientSecret` (string) — secret Power BI (préférer Key Vault)
- `PAFA_BlobStorage__Provider`, `PAFA_BlobStorage__Endpoint`, `PAFA_BlobStorage__AccessKey`, `PAFA_BlobStorage__SecretKey`

## Bonnes pratiques

- En production, utiliser exclusivement le CronJob Kubernetes pour éviter la duplication.
- Stocker les secrets dans Key Vault ou un mécanisme secret manager et injecter via variables d'environnement.
- Monitorer les durées de refresh Power BI et ajuster `DatasetRefreshTimeoutMinutes` et `BatchSize` selon la charge observée.

---

Si tu veux, je peux :
- générer un `tools/RunPowerBiBatch` one-shot prêt à l'emploi pour lancer localement le batch ; ou
- ajouter un petit endpoint admin `POST /api/admin/reports/run-monthly` (sécurisé) pour déclencher le batch depuis l'API.
