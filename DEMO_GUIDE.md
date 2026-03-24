# ?? DÉMO PAFA — Pipeline d'Ingestion Automatisé
## Présentation pour Manager

---

## 1. Ce qu'on démontre (lien avec le MVP)

| User Story | Ce qu'on montre | Statut |
|---|---|---|
| **PAFA_US_01** — Extraction SFTP | CronJob K8s ? téléchargement automatique | ? Fait |
| **PAFA_US_01** — Stockage Data Landing | Archivage fichier brut ? MinIO | ? Fait |
| **PAFA_US_01** — Parse Excel | Lecture colonnes ? mapping métriques | ? Fait |
| **PAFA_US_01** — Insert DB | MetricValues ? PostgreSQL | ? Fait |
| **PAFA_US_0a** — Validation & erreurs | 5 règles (VAL-002?013), erreurs tracées en DB | ? Fait |
| **PAFA_US_0a** — Notification échec | Fichier ? /failed + logs + validation_errors en DB | ? Fait |
| **PAFA_US_01** — Health check infra | `GET /api/health/full` (DB + SFTP + MinIO) | ? Fait |

---

## 2. Architecture démontrée

```
???????????????????    ????????????????    ????????????????    ????????????????
?  Xoserve SFTP   ????>?    MinIO      ????>?    Parse &   ????>?  PostgreSQL  ?
?  (Docker local) ?    ? (Data Landing)?    ?   Validate   ?    ?   (pafadb)   ?
???????????????????    ????????????????    ????????????????    ????????????????
        ?                                                              ?
        ?               ????????????????                               ?
  Kubernetes CronJob    ?  Health API  ?                      Dashboard / API
  (automatique)         ? /api/health  ?                      (Swagger REST)
                        ????????????????
```

---

## 3. Script de démo — Commandes exactes

> **Pré-requis** : Docker Desktop + Kubernetes activé + `docker compose up -d` déjà lancé.
> **Durée totale** : ~10 minutes.

---

### PARTIE A — Vérifier que l'infra tourne (1 min)

```powershell
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

**Dire** : *"Voici nos 3 composants d'infrastructure qui simulent l'environnement de production : PostgreSQL (notre base Azure), un serveur SFTP (simule Xoserve), et MinIO (simule Azure Blob Storage)."*

---

### PARTIE B — Health Check API (2 min)

```powershell
cd src/PAFA.Api
dotnet run
```

**Ouvrir** : http://localhost:5000/swagger

1. Trouver `GET /api/health/full`
2. Cliquer "Try it out" ? "Execute"

**Résultat attendu** :
```json
{
  "status": "healthy",
  "timestamp": "2026-03-23T...",
  "checks": {
    "database": true,
    "sftp": true,
    "minio": true
  }
}
```

**Dire** : *"Avant toute ingestion, on vérifie que les 3 composants sont accessibles. En production, Kubernetes utilise ce endpoint pour la liveness probe. Si le SFTP Xoserve est down, on le sait immédiatement."*

> Arrêter l'API avec Ctrl+C après cette étape.

---

### PARTIE C — Préparer les fichiers Xoserve (1 min)

```powershell
cd C:\Users\hlouati\Desktop\PAFAProject

# Vider les anciens fichiers
Remove-Item xoserve\upload\* -Force -ErrorAction SilentlyContinue
Remove-Item xoserve\processed\* -Force -ErrorAction SilentlyContinue
Remove-Item xoserve\failed\* -Force -ErrorAction SilentlyContinue

# Copier les fichiers de test ? SFTP
Copy-Item "Test\sftp-fixtures\*.xlsx" "xoserve\upload\" -Force

# Montrer les fichiers
docker exec pafa_sftp ls /home/xoserve/upload/
```

**Dire** : *"Voici les fichiers Excel que Xoserve dépose chaque mois sur son SFTP. On simule ça localement avec un conteneur Docker. En production, ce sera le vrai serveur Xoserve."*

---

### PARTIE D — Démo CronJob Kubernetes (4 min)

```powershell
# 1. Builder l'image
docker build -t pafa-batch:local -f src/PAFA.BatchReports/Dockerfile .

# 2. Déployer le CronJob
kubectl apply -f src/PAFA.BatchReports/cronjob-local.yaml

# 3. Montrer qu'il est enregistré
kubectl get cronjobs
```

**Dire** : *"Le CronJob est configuré pour s'exécuter toutes les 2 minutes pour la démo. En production, ce sera le 1er du mois à 02:00 UTC."*

```powershell
# 4. Attendre ~2 min, puis vérifier
kubectl get jobs
```

**Quand le job apparaît** :
```powershell
# 5. Lire les logs
kubectl logs -l app=pafa-batch
```

**Points à commenter dans les logs** :
- `SFTP connexion OK` ? *"Le système vérifie d'abord que le SFTP est disponible"*
- `Period for MOD520A_..._Mar25.xlsx: 2025-03` ? *"La période est détectée automatiquement depuis le nom du fichier, pas de config manuelle"*
- `Downloaded X bytes from SFTP` ? *"Téléchargement en mémoire"*
- `?? Saved to blob` ? *"Archivage dans le Data Landing (MinIO/Azure Blob)"*
- `Import démarré` ? *"Parsing Excel puis validation"*
- `?` ou `?` ? *"Résultat par fichier avec détails"*

```powershell
# 6. Supprimer le CronJob
kubectl delete -f src/PAFA.BatchReports/cronjob-local.yaml
```

---

### PARTIE E — Vérifier les résultats (2 min)

```powershell
# SFTP : fichiers déplacés ?
docker exec pafa_sftp ls /home/xoserve/upload/       # ? vide (traités)
docker exec pafa_sftp ls /home/xoserve/processed/    # ? fichiers réussis
docker exec pafa_sftp ls /home/xoserve/failed/       # ? fichiers en erreur
```

**Dire** : *"Les fichiers traités sont automatiquement déplacés. Succès ? /processed, Échec ? /failed. Le SFTP /upload est vidé."*

```powershell
# Base de données : données insérées ?
docker exec pafa_postgres psql -U postgres -d pafadb -c "SELECT COUNT(*) as total FROM metric_values;"
docker exec pafa_postgres psql -U postgres -d pafadb -c "SELECT COUNT(*) as total FROM validation_errors;"
```

**Dire** : *"Les métriques sont en base, prêtes pour les dashboards PowerBI. Les erreurs de validation sont aussi tracées pour l'audit."*

**Ouvrir MinIO** : http://localhost:9001 (minioadmin / minioadmin)
? Montrer le bucket `landing-zone` avec les fichiers bruts.

**Dire** : *"Les fichiers bruts sont archivés dans le Data Landing. Même si on doit rejouer une ingestion, on a toujours l'original."*

---

## 4. Points clés à communiquer

| Aspect | Détail |
|---|---|
| **Automatisation** | CronJob K8s — aucune intervention humaine |
| **Détection de période** | Lue depuis le nom du fichier (pas de config manuelle) |
| **Validation** | 5 règles (VAL-002 à VAL-013), erreurs tracées en DB |
| **Health Check** | `GET /api/health/full` vérifie DB + SFTP + MinIO avant ingestion |
| **Traçabilité** | Chaque fichier = 1 IngestionJob + 1 IngestionFile en DB |
| **Archivage** | Fichier brut ? MinIO, fichier traité ? /processed sur SFTP |
| **Gestion d'erreurs** | Fichier en erreur ? /failed, retry automatique K8s (2 tentatives) |
| **Portabilité** | Même code tourne en local (Docker Desktop) et en prod (AKS) |

---

## 5. Questions anticipées du Manager

**Q: "Et si le SFTP Xoserve est down ?"**
R: Le health check le détecte. Le CronJob échoue proprement, K8s relance 2 fois. On le voit via `GET /api/health/full` ? `sftp: false`.

**Q: "Et si un fichier est corrompu ?"**
R: Le parser détecte l'erreur, le fichier va dans /failed, les autres continuent. L'erreur est tracée dans `validation_errors`.

**Q: "Peut-on rejouer un mois ancien ?"**
R: Oui — via Swagger `POST /api/sftp/ingest?year=2025&month=1` ou via un Job K8s manuel.

**Q: "Combien de temps ça prend ?"**
R: ~30 secondes pour 5 fichiers. Timeout : 10 min (local) / 30 min (prod).

**Q: "Où en est-on par rapport au MVP ?"**
R: L'ingestion automatisée (US_01 extraction + US_0a validation) est complète. Reste : rapports PDF/Excel (2 MD) et dashboards PowerBI (15 MD).
